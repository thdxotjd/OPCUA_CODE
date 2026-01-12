using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeviceConnector.Events;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace DeviceConnector.Services
{
    /// <summary>
    /// OPC UA 클라이언트 서비스 구현
    /// KEPServerEX를 통해 ESP32 Modbus TCP 데이터 읽기/쓰기
    /// </summary>
    public class OpcUaClientService : IOpcUaClientService
    {
        #region Private Fields

        private Session? _session;
        private Subscription? _subscription;
        private readonly OpcUaConnectionInfo _connectionInfo;
        private readonly DeviceTagConfig _deviceConfig;
        private readonly object _lockObject = new();
        private bool _disposed;
        private CancellationTokenSource? _reconnectCts;

        // 구독 데이터 버퍼
        private readonly Dictionary<string, object> _subscriptionBuffer = new();
        private readonly object _bufferLock = new();

        #endregion

        #region Properties

        public ConnectionStatus ConnectionStatus { get; private set; } = new();
        public bool IsConnected => _session?.Connected ?? false;
        public bool IsSubscribed => _subscription != null;
        public ESP32Data? LastData { get; private set; }

        #endregion

        #region Events

        public event EventHandler<DataChangedEventArgs>? DataChanged;
        public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;
        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

        #endregion

        #region Constructor

        public OpcUaClientService(OpcUaConnectionInfo connectionInfo, DeviceTagConfig? deviceConfig = null)
        {
            _connectionInfo = connectionInfo;
            _deviceConfig = deviceConfig ?? new DeviceTagConfig();
        }

        #endregion

        #region 연결 관리

        public async Task<bool> ConnectAsync()
        {
            try
            {
                UpdateConnectionState(ConnectionState.Connecting);

                // 1. 애플리케이션 설정
                var config = new ApplicationConfiguration
                {
                    ApplicationName = "DeviceConnector",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier(),
                        AutoAcceptUntrustedCertificates = true
                    },
                    ClientConfiguration = new ClientConfiguration
                    {
                        DefaultSessionTimeout = _connectionInfo.SessionTimeoutMinutes * 60000
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas { OperationTimeout = 15000 }
                };

                await config.Validate(ApplicationType.Client);

                // 2. 엔드포인트 직접 구성 (CoreClientUtils.SelectEndpoint 대신)
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

                var endpointConfig = EndpointConfiguration.Create(config);
                var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfig);

                // 3. 사용자 인증
                UserIdentity userIdentity;
                if (_connectionInfo.Credentials != null)
                {
                    userIdentity = new UserIdentity(
                        _connectionInfo.Credentials.Username,
                        _connectionInfo.Credentials.Password);
                }
                else
                {
                    // Anonymous 인증 사용
                    userIdentity = new UserIdentity();
                }

                // 4. 세션 생성
                _session = await Session.Create(
                    config,
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
                OnErrorOccurred(new ErrorOccurredEventArgs($"연결 실패: {ex.Message}", ex));
                UpdateConnectionState(ConnectionState.Error, ex.Message);

                if (_connectionInfo.AutoReconnect)
                {
                    StartReconnectTimer();
                }
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            _reconnectCts?.Cancel();

            await StopSubscriptionAsync();

            if (_session != null)
            {
                _session.KeepAlive -= OnSessionKeepAlive;
                _session.Close();
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
                    await Task.Delay(_connectionInfo.ReconnectIntervalSeconds * 1000, _reconnectCts.Token);

                    UpdateConnectionState(ConnectionState.Reconnecting);
                    ConnectionStatus.ReconnectAttempts++;

                    if (await ConnectAsync())
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
            ConnectionStatus.LastErrorMessage = errorMessage;

            if (newState == ConnectionState.Connected)
            {
                ConnectionStatus.ConnectedSince = DateTime.UtcNow;
                ConnectionStatus.ReconnectAttempts = 0;
            }
            else if (newState == ConnectionState.Connecting)
            {
                ConnectionStatus.LastConnectAttempt = DateTime.UtcNow;
            }

            OnConnectionChanged(new ConnectionChangedEventArgs(ConnectionStatus, previousState));
        }

        #endregion

        #region 데이터 읽기/쓰기

        public async Task<ESP32Data?> ReadDataAsync(string deviceId = "ESP32_01")
        {
            if (_session == null || !_session.Connected)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs("연결되지 않은 상태에서 읽기 시도"));
                return null;
            }

            try
            {
                // 읽을 노드 ID 목록 생성
                var nodesToRead = new ReadValueIdCollection
                {
                    new ReadValueId { NodeId = new NodeId(_deviceConfig.GetNodeId(DeviceTagConfig.TAG_POS_X_LOW)), AttributeId = Attributes.Value },
                    new ReadValueId { NodeId = new NodeId(_deviceConfig.GetNodeId(DeviceTagConfig.TAG_POS_X_HIGH)), AttributeId = Attributes.Value },
                    new ReadValueId { NodeId = new NodeId(_deviceConfig.GetNodeId(DeviceTagConfig.TAG_SPEED)), AttributeId = Attributes.Value },
                    new ReadValueId { NodeId = new NodeId(_deviceConfig.GetNodeId(DeviceTagConfig.TAG_STATUS)), AttributeId = Attributes.Value }
                };

                // 읽기 실행
                _session.Read(
                    null,
                    0,
                    TimestampsToReturn.Both,
                    nodesToRead,
                    out DataValueCollection results,
                    out DiagnosticInfoCollection diagnostics);

                // 결과 파싱
                var data = ParseReadResults(results);
                LastData = data;

                return await Task.FromResult(data);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"데이터 읽기 실패: {ex.Message}", ex));
                return null;
            }
        }

        private ESP32Data ParseReadResults(DataValueCollection results)
        {
            var data = new ESP32Data
            {
                Timestamp = DateTime.UtcNow,
                IsGoodQuality = true
            };

            // Position X: 2개 레지스터 → float 변환
            if (results[0].StatusCode == StatusCodes.Good && results[1].StatusCode == StatusCodes.Good)
            {
                ushort low = Convert.ToUInt16(results[0].Value);
                ushort high = Convert.ToUInt16(results[1].Value);
                data.PositionX = CombineRegistersToFloat(low, high);
            }
            else
            {
                data.IsGoodQuality = false;
            }

            // Speed
            if (results[2].StatusCode == StatusCodes.Good)
            {
                data.Speed = Convert.ToUInt16(results[2].Value);
            }
            else
            {
                data.IsGoodQuality = false;
            }

            // Status
            if (results[3].StatusCode == StatusCodes.Good)
            {
                data.StatusCode = Convert.ToUInt16(results[3].Value);
            }
            else
            {
                data.IsGoodQuality = false;
            }

            return data;
        }

        /// <summary>
        /// 2개의 16비트 레지스터를 float로 변환
        /// </summary>
        private float CombineRegistersToFloat(ushort low, ushort high)
        {
            uint combined = ((uint)high << 16) | low;
            byte[] bytes = BitConverter.GetBytes(combined);
            return BitConverter.ToSingle(bytes, 0);
        }

        public async Task<bool> WriteCommandAsync(string deviceId, string tagName, object value)
        {
            if (_session == null || !_session.Connected)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs("연결되지 않은 상태에서 쓰기 시도"));
                return false;
            }

            try
            {
                var nodeId = new NodeId(_deviceConfig.GetNodeId(tagName));
                var writeValue = new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                };

                var writeValues = new WriteValueCollection { writeValue };

                _session.Write(
                    null,
                    writeValues,
                    out StatusCodeCollection results,
                    out DiagnosticInfoCollection diagnostics);

                return await Task.FromResult(StatusCode.IsGood(results[0]));
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"명령 쓰기 실패: {ex.Message}", ex));
                return false;
            }
        }

        #endregion

        #region 구독 (실시간 데이터)

        public async Task StartSubscriptionAsync(int samplingIntervalMs = 100, string deviceId = "ESP32_01")
        {
            if (_session == null || !_session.Connected)
            {
                throw new InvalidOperationException("OPC UA 서버에 연결되지 않았습니다.");
            }

            // 기존 구독 정리
            await StopSubscriptionAsync();

            // 새 구독 생성
            _subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = samplingIntervalMs,
                PublishingEnabled = true,
                Priority = 100
            };

            // 모니터링 항목 추가
            var monitoredItems = new List<MonitoredItem>
            {
                CreateMonitoredItem(DeviceTagConfig.TAG_POS_X_LOW, samplingIntervalMs),
                CreateMonitoredItem(DeviceTagConfig.TAG_POS_X_HIGH, samplingIntervalMs),
                CreateMonitoredItem(DeviceTagConfig.TAG_SPEED, samplingIntervalMs),
                CreateMonitoredItem(DeviceTagConfig.TAG_STATUS, samplingIntervalMs)
            };

            _subscription.AddItems(monitoredItems);
            _session.AddSubscription(_subscription);

            await Task.Run(() => _subscription.Create());
        }

        private MonitoredItem CreateMonitoredItem(string tagName, int samplingInterval)
        {
            var item = new MonitoredItem(_subscription!.DefaultItem)
            {
                StartNodeId = new NodeId(_deviceConfig.GetNodeId(tagName)),
                AttributeId = Attributes.Value,
                DisplayName = tagName,
                SamplingInterval = samplingInterval,
                QueueSize = 10,
                DiscardOldest = true
            };

            item.Notification += OnMonitoredItemNotification;
            return item;
        }

        private void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            if (e.NotificationValue is MonitoredItemNotification notification)
            {
                lock (_bufferLock)
                {
                    _subscriptionBuffer[item.DisplayName] = notification.Value.Value;
                }

                // 모든 태그 데이터가 수집되면 이벤트 발생
                if (_subscriptionBuffer.Count >= 4)
                {
                    var previousData = LastData;
                    var newData = BuildDataFromBuffer();
                    LastData = newData;

                    OnDataChanged(new DataChangedEventArgs(_deviceConfig.DeviceId, newData, previousData));
                }
            }
        }

        private ESP32Data BuildDataFromBuffer()
        {
            lock (_bufferLock)
            {
                var data = new ESP32Data
                {
                    Timestamp = DateTime.UtcNow,
                    IsGoodQuality = true
                };

                if (_subscriptionBuffer.TryGetValue(DeviceTagConfig.TAG_POS_X_LOW, out var lowVal) &&
                    _subscriptionBuffer.TryGetValue(DeviceTagConfig.TAG_POS_X_HIGH, out var highVal))
                {
                    data.PositionX = CombineRegistersToFloat(
                        Convert.ToUInt16(lowVal),
                        Convert.ToUInt16(highVal));
                }

                if (_subscriptionBuffer.TryGetValue(DeviceTagConfig.TAG_SPEED, out var speedVal))
                {
                    data.Speed = Convert.ToUInt16(speedVal);
                }

                if (_subscriptionBuffer.TryGetValue(DeviceTagConfig.TAG_STATUS, out var statusVal))
                {
                    data.StatusCode = Convert.ToUInt16(statusVal);
                }

                return data;
            }
        }

        public async Task StopSubscriptionAsync()
        {
            if (_subscription != null)
            {
                foreach (var item in _subscription.MonitoredItems)
                {
                    item.Notification -= OnMonitoredItemNotification;
                }

                await Task.Run(() => _subscription.Delete(true));
                _session?.RemoveSubscription(_subscription);
                _subscription = null;
            }

            lock (_bufferLock)
            {
                _subscriptionBuffer.Clear();
            }
        }

        #endregion

        #region 이벤트 발생

        protected virtual void OnDataChanged(DataChangedEventArgs e)
        {
            DataChanged?.Invoke(this, e);
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
