using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeviceConnector.Events;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using Opc.Ua;
using Opc.Ua.Client;

#pragma warning disable CS0618

namespace DeviceConnector.Services
{
    /// <summary>
    /// RoboDK OPC UA 서버와 통신하는 서비스
    /// OPC UA Method 호출을 통해 로봇 Joint 값 읽기/쓰기
    /// </summary>
    public class RoboDkOpcUaService : IRoboDkOpcUaService
    {
        #region Private Fields

        private Session? _session;
        private ApplicationConfiguration? _appConfig;
        private readonly RoboDkConnectionInfo _connectionInfo;
        private bool _disposed;
        private CancellationTokenSource? _reconnectCts;

        // RoboDK OPC UA Method NodeId 정의
        private static class RoboDkNodeIds
        {
            // Objects 폴더 NodeId (RoboDK 서버 기준)
            public static readonly NodeId ObjectsFolder = new NodeId(85); // Objects folder

            // Method NodeIds (ns=1 기준)
            public static NodeId GetJointsStr => new NodeId(1002, 1);     // getJointsStr
            public static NodeId SetJointsStr => new NodeId(2002, 1);     // setJointsStr
            public static NodeId GetJoints => new NodeId(1001, 1);        // getJoints
            public static NodeId SetJoints => new NodeId(2001, 1);        // setJoints
            public static NodeId GetItem => new NodeId(1000, 1);          // getItem
        }

        #endregion

        #region Properties

        public ConnectionStatus ConnectionStatus { get; private set; } = new();
        public bool IsConnected => _session?.Connected ?? false;
        public RobotJointData? LastJointData { get; private set; }

        #endregion

        #region Events

        public event EventHandler<RobotJointChangedEventArgs>? JointDataChanged;
        public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;
        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

        #endregion

        #region Constructor

        public RoboDkOpcUaService(RoboDkConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
        }

        /// <summary>
        /// 기본 연결 설정으로 생성 (localhost:4840)
        /// </summary>
        public RoboDkOpcUaService() : this(new RoboDkConnectionInfo())
        {
        }

        #endregion

        #region 연결 관리

        public async Task<bool> ConnectAsync()
        {
            try
            {
                UpdateConnectionState(ConnectionState.Connecting);

                // 1. 애플리케이션 설정 생성
                _appConfig = CreateApplicationConfiguration();

                // 2. 엔드포인트 직접 구성 (보안 없음)
                var endpoint = new EndpointDescription
                {
                    EndpointUrl = _connectionInfo.EndpointUrl,
                    SecurityMode = MessageSecurityMode.None,
                    SecurityPolicyUri = SecurityPolicies.None,
                    UserIdentityTokens = new UserTokenPolicyCollection
                    {
                        new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
                    },
                    TransportProfileUri = Profiles.UaTcpTransport
                };

                var endpointConfig = EndpointConfiguration.Create(_appConfig);
                var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfig);

                // 3. 익명 인증
                var userIdentity = new UserIdentity(new AnonymousIdentityToken());

                // 4. 세션 생성
                _session = await Session.Create(
                    _appConfig,
                    configuredEndpoint,
                    false,
                    _connectionInfo.SessionName,
                    (uint)(_connectionInfo.SessionTimeoutMinutes * 60000),
                    userIdentity,
                    null);

                // 5. KeepAlive 이벤트 등록
                _session.KeepAlive += OnSessionKeepAlive;

                UpdateConnectionState(ConnectionState.Connected);
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"RoboDK 연결 실패: {ex.Message}", ex));
                UpdateConnectionState(ConnectionState.Error, ex.Message);

                if (_connectionInfo.AutoReconnect)
                {
                    StartReconnectTimer();
                }
                return false;
            }
        }

        private ApplicationConfiguration CreateApplicationConfiguration()
        {
            var config = new ApplicationConfiguration
            {
                ApplicationName = "DeviceConnector_RoboDK",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier(),
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = _connectionInfo.SessionTimeoutMinutes * 60000
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = _connectionInfo.ConnectionTimeoutSeconds * 1000
                }
            };

            config.Validate(ApplicationType.Client).GetAwaiter().GetResult();

            config.CertificateValidator.CertificateValidation += (s, e) =>
            {
                if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted ||
                    e.Error.StatusCode == StatusCodes.BadCertificateChainIncomplete ||
                    e.Error.StatusCode == StatusCodes.BadCertificateInvalid)
                {
                    e.Accept = true;
                }
            };

            return config;
        }

        public async Task DisconnectAsync()
        {
            _reconnectCts?.Cancel();

            if (_session != null)
            {
                _session.KeepAlive -= OnSessionKeepAlive;
                try
                {
                    if (_session.Connected)
                    {
                        await _session.CloseAsync();
                    }
                }
                catch { }
                _session.Dispose();
                _session = null;
            }

            UpdateConnectionState(ConnectionState.Disconnected);
        }

        private void OnSessionKeepAlive(ISession session, KeepAliveEventArgs e)
        {
            if (e.Status != null && ServiceResult.IsBad(e.Status))
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"KeepAlive 실패: {e.Status}"));
                UpdateConnectionState(ConnectionState.Error, e.Status.ToString());

                if (_connectionInfo.AutoReconnect)
                {
                    StartReconnectTimer();
                }
            }
        }

        private void StartReconnectTimer()
        {
            _reconnectCts?.Cancel();
            _reconnectCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (!_reconnectCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(_connectionInfo.ReconnectIntervalSeconds * 1000, _reconnectCts.Token);
                        UpdateConnectionState(ConnectionState.Reconnecting);
                        ConnectionStatus.ReconnectAttempts++;

                        if (await ConnectAsync())
                        {
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, _reconnectCts.Token);
        }

        private void UpdateConnectionState(ConnectionState newState, string? errorMessage = null)
        {
            var previousState = ConnectionStatus.State;
            ConnectionStatus.State = newState;
            ConnectionStatus.EndpointUrl = _connectionInfo.EndpointUrl;

            if (newState == ConnectionState.Connected)
            {
                ConnectionStatus.ConnectedAt = DateTime.UtcNow;
                ConnectionStatus.LastErrorMessage = null;
                ConnectionStatus.ReconnectAttempts = 0;
            }
            else if (newState == ConnectionState.Error)
            {
                ConnectionStatus.LastErrorMessage = errorMessage;
            }

            OnConnectionChanged(new ConnectionChangedEventArgs(previousState, newState, errorMessage));
        }

        #endregion

        #region Robot Joint 읽기/쓰기 (OPC UA Method 호출)

        /// <summary>
        /// 로봇 Joint 값을 문자열로 읽기 (getJointsStr Method 호출)
        /// </summary>
        /// <param name="robotName">로봇 이름 (예: "ABB CRB 1300-7/1.4")</param>
        /// <returns>Joint 값 문자열 (예: "0,0,0,0,0,0")</returns>
        public async Task<string?> GetJointsStrAsync(string robotName)
        {
            if (_session == null || !_session.Connected)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs("연결되지 않은 상태에서 읽기 시도"));
                return null;
            }

            try
            {
                var result = await CallMethodAsync(
                    RoboDkNodeIds.ObjectsFolder,
                    RoboDkNodeIds.GetJointsStr,
                    robotName);

                if (result != null && result.Count > 0)
                {
                    var jointsStr = result[0]?.ToString();
                    
                    // 데이터 업데이트
                    var previousData = LastJointData;
                    LastJointData = RobotJointData.FromString(robotName, jointsStr);
                    
                    OnJointDataChanged(new RobotJointChangedEventArgs(robotName, LastJointData, previousData));
                    
                    return jointsStr;
                }

                return null;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"Joint 읽기 실패: {ex.Message}", ex));
                return null;
            }
        }

        /// <summary>
        /// 로봇 Joint 값을 배열로 읽기 (getJoints Method 호출)
        /// </summary>
        /// <param name="robotName">로봇 이름</param>
        /// <returns>Joint 값 배열</returns>
        public async Task<double[]?> GetJointsAsync(string robotName)
        {
            if (_session == null || !_session.Connected)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs("연결되지 않은 상태에서 읽기 시도"));
                return null;
            }

            try
            {
                var result = await CallMethodAsync(
                    RoboDkNodeIds.ObjectsFolder,
                    RoboDkNodeIds.GetJoints,
                    robotName);

                if (result != null && result.Count > 0)
                {
                    // 결과가 double[] 형태로 반환됨
                    if (result[0] is double[] joints)
                    {
                        var previousData = LastJointData;
                        LastJointData = new RobotJointData
                        {
                            RobotName = robotName,
                            Joints = joints,
                            Timestamp = DateTime.UtcNow,
                            IsGoodQuality = true
                        };
                        
                        OnJointDataChanged(new RobotJointChangedEventArgs(robotName, LastJointData, previousData));
                        
                        return joints;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"Joint 배열 읽기 실패: {ex.Message}", ex));
                return null;
            }
        }

        /// <summary>
        /// 로봇 Joint 값을 문자열로 설정 (setJointsStr Method 호출)
        /// </summary>
        /// <param name="robotName">로봇 이름</param>
        /// <param name="jointsStr">Joint 값 문자열 (예: "0,0,0,0,0,0")</param>
        public async Task<bool> SetJointsStrAsync(string robotName, string jointsStr)
        {
            if (_session == null || !_session.Connected)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs("연결되지 않은 상태에서 쓰기 시도"));
                return false;
            }

            try
            {
                var result = await CallMethodAsync(
                    RoboDkNodeIds.ObjectsFolder,
                    RoboDkNodeIds.SetJointsStr,
                    robotName,
                    jointsStr);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"Joint 설정 실패: {ex.Message}", ex));
                return false;
            }
        }

        /// <summary>
        /// 로봇 Joint 값을 배열로 설정 (setJoints Method 호출)
        /// </summary>
        /// <param name="robotName">로봇 이름</param>
        /// <param name="joints">Joint 값 배열</param>
        public async Task<bool> SetJointsAsync(string robotName, double[] joints)
        {
            if (_session == null || !_session.Connected)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs("연결되지 않은 상태에서 쓰기 시도"));
                return false;
            }

            try
            {
                var result = await CallMethodAsync(
                    RoboDkNodeIds.ObjectsFolder,
                    RoboDkNodeIds.SetJoints,
                    robotName,
                    joints);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"Joint 배열 설정 실패: {ex.Message}", ex));
                return false;
            }
        }

        /// <summary>
        /// RoboDK 아이템 정보 가져오기
        /// </summary>
        public async Task<string?> GetItemAsync(string itemName)
        {
            if (_session == null || !_session.Connected)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs("연결되지 않은 상태에서 읽기 시도"));
                return null;
            }

            try
            {
                var result = await CallMethodAsync(
                    RoboDkNodeIds.ObjectsFolder,
                    RoboDkNodeIds.GetItem,
                    itemName);

                if (result != null && result.Count > 0)
                {
                    return result[0]?.ToString();
                }

                return null;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"Item 정보 읽기 실패: {ex.Message}", ex));
                return null;
            }
        }

        #endregion

        #region OPC UA Method 호출 핵심 로직

        /// <summary>
        /// OPC UA Method 호출
        /// </summary>
        private async Task<IList<object>?> CallMethodAsync(NodeId objectId, NodeId methodId, params object[] inputArguments)
        {
            if (_session == null || !_session.Connected)
            {
                throw new InvalidOperationException("OPC UA 서버에 연결되지 않았습니다.");
            }

            // Method 호출 요청 생성
            var callMethodRequest = new CallMethodRequest
            {
                ObjectId = objectId,
                MethodId = methodId,
                InputArguments = new VariantCollection()
            };

            // 입력 인자 추가
            foreach (var arg in inputArguments)
            {
                callMethodRequest.InputArguments.Add(new Variant(arg));
            }

            var callMethodRequests = new CallMethodRequestCollection { callMethodRequest };

            // Method 호출 실행
            var response = await _session.CallAsync(
                null,
                callMethodRequests,
                CancellationToken.None);

            // 결과 확인
            if (response.Results != null && response.Results.Count > 0)
            {
                var callResult = response.Results[0];

                if (StatusCode.IsBad(callResult.StatusCode))
                {
                    throw new Exception($"Method 호출 실패: {callResult.StatusCode}");
                }

                // 출력 인자 반환
                if (callResult.OutputArguments != null && callResult.OutputArguments.Count > 0)
                {
                    var outputs = new List<object>();
                    foreach (var output in callResult.OutputArguments)
                    {
                        outputs.Add(output.Value);
                    }
                    return outputs;
                }
            }

            return null;
        }

        #endregion

        #region Variable 읽기 (보조 기능)

        /// <summary>
        /// RoboDK 변수 읽기 (Variable 노드)
        /// </summary>
        public async Task<T?> ReadVariableAsync<T>(string nodeIdString)
        {
            if (_session == null || !_session.Connected)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs("연결되지 않은 상태에서 읽기 시도"));
                return default;
            }

            try
            {
                var nodeId = new NodeId(nodeIdString);
                var readValue = await _session.ReadValueAsync(nodeId);

                if (StatusCode.IsGood(readValue.StatusCode))
                {
                    return (T)Convert.ChangeType(readValue.Value, typeof(T));
                }

                return default;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"변수 읽기 실패: {ex.Message}", ex));
                return default;
            }
        }

        /// <summary>
        /// 시뮬레이션 속도 읽기
        /// </summary>
        public async Task<double?> GetSimulationSpeedAsync()
        {
            return await ReadVariableAsync<double>("ns=1;s=SimulationSpeed");
        }

        /// <summary>
        /// Station 이름 읽기
        /// </summary>
        public async Task<string?> GetStationNameAsync()
        {
            return await ReadVariableAsync<string>("ns=1;s=Station");
        }

        /// <summary>
        /// RoboDK 버전 정보 읽기
        /// </summary>
        public async Task<string?> GetRoboDkVersionAsync()
        {
            return await ReadVariableAsync<string>("ns=1;s=RoboDK");
        }

        #endregion

        #region 이벤트 발생

        protected virtual void OnJointDataChanged(RobotJointChangedEventArgs e)
        {
            JointDataChanged?.Invoke(this, e);
        }

        protected virtual void OnConnectionChanged(ConnectionChangedEventArgs e)
        {
            ConnectionChanged?.Invoke(this, e);
        }

        protected virtual void OnErrorOccurred(ErrorOccurredEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _reconnectCts?.Cancel();
                _reconnectCts?.Dispose();
                DisconnectAsync().GetAwaiter().GetResult();
            }

            _disposed = true;
        }

        #endregion
    }
}

#pragma warning restore CS0618
