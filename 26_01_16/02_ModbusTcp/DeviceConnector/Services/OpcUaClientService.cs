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
    /// KEPServerEX와 통신하여 ESP32 데이터를 읽고 쓰기
    /// </summary>
    public class OpcUaClientService : IOpcUaClientService
    {
        #region Private Fields

        private Session? _session;
        private Subscription? _subscription;
        private OpcUaConnectionInfo _connectionInfo;
        private readonly Dictionary<string, DeviceTagConfig> _deviceConfigs;
        private readonly object _lockObject = new();
        private bool _disposed;
        private CancellationTokenSource? _reconnectCts;

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

        public OpcUaClientService()
        {
            _connectionInfo = new OpcUaConnectionInfo();
            _deviceConfigs = new Dictionary<string, DeviceTagConfig>
            {
                ["ESP32_01"] = new DeviceTagConfig()
            };
        }

        public OpcUaClientService(OpcUaConnectionInfo connectionInfo) : this()
        {
            _connectionInfo = connectionInfo;
        }

        #endregion

        #region Configuration

        public void Configure(OpcUaConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
        }

        public void AddDeviceConfig(DeviceTagConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _deviceConfigs[config.DeviceId] = config;
        }

        #endregion

        #region Connection Management

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                UpdateConnectionState(ConnectionState.Connecting);

                // OPC UA 애플리케이션 설정
                var config = new ApplicationConfiguration
                {
                    ApplicationName = "DeviceConnector",
                    ApplicationUri = Utils.Format(@"urn:{0}:DeviceConnector", System.Net.Dns.GetHostName()),
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier
                        {
                            StoreType = @"Directory",
                            StorePath = @"./OPC Foundation/CertificateStores/MachineDefault",
                            SubjectName = "DeviceConnector"
                        },
                        TrustedIssuerCertificates = new CertificateTrustList
                        {
                            StoreType = @"Directory",
                            StorePath = @"./OPC Foundation/CertificateStores/UA Certificate Authorities"
                        },
                        TrustedPeerCertificates = new CertificateTrustList
                        {
                            StoreType = @"Directory",
                            StorePath = @"./OPC Foundation/CertificateStores/UA Applications"
                        },
                        RejectedCertificateStore = new CertificateTrustList
                        {
                            StoreType = @"Directory",
                            StorePath = @"./OPC Foundation/CertificateStores/RejectedCertificates"
                        },
                        AutoAcceptUntrustedCertificates = true,
                        AddAppCertToTrustedStore = true
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                    ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
                };

                await config.Validate(ApplicationType.Client).ConfigureAwait(false);

                // 엔드포인트 선택
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(
                _connectionInfo.EndpointUrl,
                useSecurity: false);

                // 세션 생성
                var endpointConfig = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

                UserIdentity userIdentity;
                if (_connectionInfo.Credentials != null)
                {
                    userIdentity = new UserIdentity(
                        _connectionInfo.Credentials.Username,
                        _connectionInfo.Credentials.Password);
                }
                else
                {
                    userIdentity = new UserIdentity(new AnonymousIdentityToken());
                }

                _session = await Session.Create(
                    config,
                    endpoint,
                    false,
                    _connectionInfo.SessionName,
                    (uint)(_connectionInfo.SessionTimeoutMinutes * 60 * 1000),
                    userIdentity,
                    null).ConfigureAwait(false);

                // 연결 끊김 이벤트 핸들러
                _session.KeepAlive += Session_KeepAlive;

                UpdateConnectionState(ConnectionState.Connected);
                ConnectionStatus.ConnectedSince = DateTime.UtcNow;
                ConnectionStatus.ReconnectAttempts = 0;

                return true;
            }
            catch (Exception ex)
            {
                ConnectionStatus.LastError = ex.Message;
                UpdateConnectionState(ConnectionState.Error);
                OnErrorOccurred(new ErrorOccurredEventArgs($"연결 실패: {ex.Message}", ex));

                if (_connectionInfo.AutoReconnect)
                {
                    _ = StartAutoReconnectAsync(cancellationToken);
                }

                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _reconnectCts?.Cancel();
                
                await StopSubscriptionAsync().ConfigureAwait(false);

                if (_session != null)
                {
                    _session.KeepAlive -= Session_KeepAlive;
                    _session.Close();
                    _session.Dispose();
                    _session = null;
                }

                UpdateConnectionState(ConnectionState.Disconnected);
                ConnectionStatus.ConnectedSince = null;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"연결 해제 오류: {ex.Message}", ex, ErrorSeverity.Warning));
            }
        }

        private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
        {
            if (e.Status != null && ServiceResult.IsNotGood(e.Status))
            {
                UpdateConnectionState(ConnectionState.Reconnecting);
                OnErrorOccurred(new ErrorOccurredEventArgs(
                    $"연결 끊김 감지: {e.Status}", null, ErrorSeverity.Warning));

                if (_connectionInfo.AutoReconnect)
                {
                    _ = StartAutoReconnectAsync(CancellationToken.None);
                }
            }
        }

        private async Task StartAutoReconnectAsync(CancellationToken cancellationToken)
        {
            _reconnectCts?.Cancel();
            _reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _reconnectCts.Token;

            while (!token.IsCancellationRequested && !IsConnected)
            {
                ConnectionStatus.ReconnectAttempts++;
                UpdateConnectionState(ConnectionState.Reconnecting);

                await Task.Delay(
                    TimeSpan.FromSeconds(_connectionInfo.ReconnectIntervalSeconds), 
                    token).ConfigureAwait(false);

                if (await ConnectAsync(token).ConfigureAwait(false))
                {
                    break;
                }
            }
        }

        #endregion

        #region Data Reading

        public async Task<ESP32Data?> ReadDataAsync(string deviceId = "ESP32_01")
        {
            if (!IsConnected || _session == null)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs("OPC UA 서버에 연결되지 않음", null, ErrorSeverity.Warning));
                return null;
            }

            if (!_deviceConfigs.TryGetValue(deviceId, out var config))
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"디바이스 설정을 찾을 수 없음: {deviceId}", null, ErrorSeverity.Error));
                return null;
            }

            try
            {
                // 읽을 노드 ID 목록 (KEPServerEX 태그 기준)
                var nodesToRead = new ReadValueIdCollection
                {
                    new ReadValueId { NodeId = new NodeId(config.GetNodeId(DeviceTagConfig.TAG_POS_X)), AttributeId = Attributes.Value },
                    new ReadValueId { NodeId = new NodeId(config.GetNodeId(DeviceTagConfig.TAG_POS_Y)), AttributeId = Attributes.Value },
                    new ReadValueId { NodeId = new NodeId(config.GetNodeId(DeviceTagConfig.TAG_STATE)), AttributeId = Attributes.Value },
                    new ReadValueId { NodeId = new NodeId(config.GetNodeId(DeviceTagConfig.TAG_TO)), AttributeId = Attributes.Value }
                };

                // 비동기 읽기
                _session.Read(
                    null,
                    0,
                    TimestampsToReturn.Both,
                    nodesToRead,
                    out DataValueCollection results,
                    out DiagnosticInfoCollection diagnosticInfos);

                // 결과 파싱
                var data = new ESP32Data
                {
                    Timestamp = DateTime.UtcNow,
                    IsGoodQuality = results.All(r => StatusCode.IsGood(r.StatusCode))
                };

                if (results.Count >= 4)
                {
                    // POS_X (Word)
                    data.PositionX = Convert.ToInt16(results[0].Value);

                    // POS_Y (Word)
                    data.PositionY = Convert.ToInt16(results[1].Value);

                    // State (Boolean)
                    data.State = Convert.ToBoolean(results[2].Value);

                    // To (Boolean)
                    data.To = Convert.ToBoolean(results[3].Value);
                }

                // 이전 데이터와 비교하여 이벤트 발생
                var previousData = LastData;
                LastData = data;

                OnDataChanged(new DataChangedEventArgs(deviceId, data, previousData));

                return data;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"데이터 읽기 오류: {ex.Message}", ex));
                return null;
            }
        }

        public async Task<T?> ReadTagAsync<T>(string nodeId)
        {
            if (!IsConnected || _session == null)
            {
                return default;
            }

            try
            {
                var value = _session.ReadValue(new NodeId(nodeId));
                if (StatusCode.IsGood(value.StatusCode))
                {
                    return (T)Convert.ChangeType(value.Value, typeof(T));
                }
                return default;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"태그 읽기 오류: {ex.Message}", ex));
                return default;
            }
        }

        #endregion

        #region Data Writing

        public async Task<bool> WriteTagAsync<T>(string nodeId, T value)
        {
            if (!IsConnected || _session == null)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs("OPC UA 서버에 연결되지 않음", null, ErrorSeverity.Warning));
                return false;
            }

            try
            {
                var writeValue = new WriteValue
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                };

                var nodesToWrite = new WriteValueCollection { writeValue };

                _session.Write(null, nodesToWrite, out StatusCodeCollection results, out DiagnosticInfoCollection diagnosticInfos);

                return results.Count > 0 && StatusCode.IsGood(results[0]);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"태그 쓰기 오류: {ex.Message}", ex));
                return false;
            }
        }

        public async Task<bool> WritePosXAsync(short value, string deviceId = "ESP32_01")
        {
            if (!_deviceConfigs.TryGetValue(deviceId, out var config))
            {
                return false;
            }

            var nodeId = config.GetNodeId(DeviceTagConfig.TAG_POS_X);
            return await WriteTagAsync(nodeId, value).ConfigureAwait(false);
        }

        public async Task<bool> WritePosYAsync(short value, string deviceId = "ESP32_01")
        {
            if (!_deviceConfigs.TryGetValue(deviceId, out var config))
            {
                return false;
            }

            var nodeId = config.GetNodeId(DeviceTagConfig.TAG_POS_Y);
            return await WriteTagAsync(nodeId, value).ConfigureAwait(false);
        }

        public async Task<bool> WriteStateAsync(bool value, string deviceId = "ESP32_01")
        {
            if (!_deviceConfigs.TryGetValue(deviceId, out var config))
            {
                return false;
            }

            var nodeId = config.GetNodeId(DeviceTagConfig.TAG_STATE);
            return await WriteTagAsync(nodeId, value).ConfigureAwait(false);
        }

        public async Task<bool> WriteToAsync(bool value, string deviceId = "ESP32_01")
        {
            if (!_deviceConfigs.TryGetValue(deviceId, out var config))
            {
                return false;
            }

            var nodeId = config.GetNodeId(DeviceTagConfig.TAG_TO);
            return await WriteTagAsync(nodeId, value).ConfigureAwait(false);
        }

        #endregion

        #region Subscription

        public async Task StartSubscriptionAsync(int samplingIntervalMs = 100, string deviceId = "ESP32_01")
        {
            if (!IsConnected || _session == null)
            {
                throw new InvalidOperationException("OPC UA 서버에 연결되지 않음");
            }

            if (!_deviceConfigs.TryGetValue(deviceId, out var config))
            {
                throw new ArgumentException($"디바이스 설정을 찾을 수 없음: {deviceId}");
            }

            // 기존 구독 중지
            await StopSubscriptionAsync().ConfigureAwait(false);

            try
            {
                // 구독 생성
                _subscription = new Subscription(_session.DefaultSubscription)
                {
                    DisplayName = $"ESP32_Subscription_{deviceId}",
                    PublishingInterval = samplingIntervalMs,
                    KeepAliveCount = 10,
                    LifetimeCount = 100,
                    MaxNotificationsPerPublish = 1000,
                    Priority = 100
                };

                _session.AddSubscription(_subscription);
                _subscription.Create();

                // 모니터링 아이템 추가 (KEPServerEX 태그 기준)
                var monitoredItems = new List<MonitoredItem>
                {
                    CreateMonitoredItem(config.GetNodeId(DeviceTagConfig.TAG_POS_X), samplingIntervalMs),
                    CreateMonitoredItem(config.GetNodeId(DeviceTagConfig.TAG_POS_Y), samplingIntervalMs),
                    CreateMonitoredItem(config.GetNodeId(DeviceTagConfig.TAG_STATE), samplingIntervalMs),
                    CreateMonitoredItem(config.GetNodeId(DeviceTagConfig.TAG_TO), samplingIntervalMs)
                };

                _subscription.AddItems(monitoredItems);
                _subscription.ApplyChanges();
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"구독 시작 오류: {ex.Message}", ex));
                throw;
            }
        }

        public async Task StopSubscriptionAsync()
        {
            if (_subscription != null)
            {
                try
                {
                    _subscription.Delete(true);
                    _session?.RemoveSubscription(_subscription);
                    _subscription.Dispose();
                    _subscription = null;
                }
                catch (Exception ex)
                {
                    OnErrorOccurred(new ErrorOccurredEventArgs($"구독 중지 오류: {ex.Message}", ex, ErrorSeverity.Warning));
                }
            }
        }

        private MonitoredItem CreateMonitoredItem(string nodeId, int samplingInterval)
        {
            var item = new MonitoredItem(_subscription.DefaultItem)
            {
                DisplayName = nodeId,
                StartNodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value,
                SamplingInterval = samplingInterval,
                QueueSize = 10,
                DiscardOldest = true
            };

            item.Notification += MonitoredItem_Notification;
            return item;
        }

        private readonly Dictionary<string, object> _subscriptionBuffer = new();
        private readonly object _bufferLock = new();

        private void MonitoredItem_Notification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            try
            {
                var notification = e.NotificationValue as MonitoredItemNotification;
                if (notification == null) return;

                lock (_bufferLock)
                {
                    _subscriptionBuffer[item.DisplayName] = notification.Value.Value;

                    // 모든 값이 수신되면 데이터 객체 생성 (4개 태그)
                    if (_subscriptionBuffer.Count >= 4)
                    {
                        var config = _deviceConfigs.Values.First();
                        
                        var data = new ESP32Data
                        {
                            Timestamp = DateTime.UtcNow,
                            IsGoodQuality = true
                        };

                        // POS_X (Word)
                        if (_subscriptionBuffer.TryGetValue(config.GetNodeId(DeviceTagConfig.TAG_POS_X), out var posX))
                        {
                            data.PositionX = Convert.ToInt16(posX);
                        }

                        // POS_Y (Word)
                        if (_subscriptionBuffer.TryGetValue(config.GetNodeId(DeviceTagConfig.TAG_POS_Y), out var posY))
                        {
                            data.PositionY = Convert.ToInt16(posY);
                        }

                        // State (Boolean)
                        if (_subscriptionBuffer.TryGetValue(config.GetNodeId(DeviceTagConfig.TAG_STATE), out var state))
                        {
                            data.State = Convert.ToBoolean(state);
                        }

                        // To (Boolean)
                        if (_subscriptionBuffer.TryGetValue(config.GetNodeId(DeviceTagConfig.TAG_TO), out var to))
                        {
                            data.To = Convert.ToBoolean(to);
                        }

                        var previousData = LastData;
                        LastData = data;
                        OnDataChanged(new DataChangedEventArgs(config.DeviceId, data, previousData));
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorOccurredEventArgs($"구독 알림 처리 오류: {ex.Message}", ex, ErrorSeverity.Warning));
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 2개의 16비트 레지스터를 float로 변환
        /// </summary>
        private static float ConvertRegistersToFloat(ushort low, ushort high)
        {
            byte[] bytes = new byte[4];
            BitConverter.GetBytes(low).CopyTo(bytes, 0);
            BitConverter.GetBytes(high).CopyTo(bytes, 2);
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// float를 2개의 16비트 레지스터로 변환
        /// </summary>
        private static (ushort low, ushort high) ConvertFloatToRegisters(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            ushort low = BitConverter.ToUInt16(bytes, 0);
            ushort high = BitConverter.ToUInt16(bytes, 2);
            return (low, high);
        }

        private void UpdateConnectionState(ConnectionState newState)
        {
            var previousState = ConnectionStatus.State;
            ConnectionStatus.State = newState;
            ConnectionStatus.LastConnectAttempt = DateTime.UtcNow;

            if (previousState != newState)
            {
                OnConnectionChanged(new ConnectionChangedEventArgs(ConnectionStatus, previousState));
            }
        }

        #endregion

        #region Event Handlers

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
