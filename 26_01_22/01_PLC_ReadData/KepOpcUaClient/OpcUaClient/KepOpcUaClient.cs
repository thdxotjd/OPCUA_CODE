using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using KepOpcUaClient.Models;

namespace KepOpcUaClient.OpcUaClient
{
    /// <summary>
    /// KEPServerEX OPC UA 클라이언트
    /// 사용 기한: 2026년 2월 28일까지
    /// </summary>
    public class KepOpcUaClient : IDisposable
    {
        private readonly KepServerConfig _config;
        private readonly DeviceConfig _deviceConfig;
        private ApplicationConfiguration? _appConfig;
        private Session? _session;
        private Subscription? _subscription;
        private bool _disposed;

        /// <summary>
        /// 연결 상태
        /// </summary>
        public bool IsConnected => _session?.Connected ?? false;

        /// <summary>
        /// 데이터 변경 이벤트
        /// </summary>
        public event EventHandler<DataChangedEventArgs>? DataChanged;

        public KepOpcUaClient(KepServerConfig config, DeviceConfig deviceConfig)
        {
            _config = config;
            _deviceConfig = deviceConfig;
        }

        #region 연결 관리

        /// <summary>
        /// KEPServerEX OPC UA 서버에 연결
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                Console.WriteLine($"[INFO] KEPServerEX 연결 시도 중... {_config.EndpointUrl}");

                // 애플리케이션 구성 생성
                _appConfig = CreateApplicationConfiguration();
                await _appConfig.Validate(ApplicationType.Client);

                // 인증서 설정
                if (_appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    _appConfig.CertificateValidator.CertificateValidation += (s, e) =>
                    {
                        e.Accept = true;
                    };
                }

                // 엔드포인트 선택
                var endpoint = CoreClientUtils.SelectEndpoint(
                    _config.EndpointUrl,
                    useSecurity: _config.SecurityMode != "None");

                Console.WriteLine($"[INFO] 엔드포인트 선택: {endpoint.EndpointUrl}");

                // 세션 생성
                _session = await Session.Create(
                    _appConfig,
                    new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(_appConfig)),
                    updateBeforeConnect: false,
                    sessionName: "KepOpcUaClient",
                    sessionTimeout: 60000,
                    identity: new UserIdentity(new AnonymousIdentityToken()),
                    preferredLocales: null);

                _session.KeepAlive += Session_KeepAlive;

                Console.WriteLine($"[SUCCESS] KEPServerEX 연결 성공!");
                Console.WriteLine($"[INFO] Session ID: {_session.SessionId}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 연결 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 연결 해제
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (_subscription != null)
                {
                    _session?.RemoveSubscription(_subscription);
                    _subscription = null;
                }

                if (_session != null)
                {
                    await _session.CloseAsync();
                    _session.Dispose();
                    _session = null;
                }

                Console.WriteLine("[INFO] 연결 해제됨");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 연결 해제 실패: {ex.Message}");
            }
        }

        private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
        {
            if (e.Status != null && ServiceResult.IsNotGood(e.Status))
            {
                Console.WriteLine($"[WARNING] KeepAlive 실패: {e.Status}");
            }
        }

        #endregion

        #region Read 기능

        /// <summary>
        /// 단일 태그 읽기
        /// </summary>
        public async Task<object?> ReadTagAsync(string nodeId)
        {
            if (_session == null || !_session.Connected)
            {
                throw new InvalidOperationException("세션이 연결되지 않았습니다.");
            }

            try
            {
                var nodesToRead = new ReadValueIdCollection
                {
                    new ReadValueId
                    {
                        NodeId = new NodeId(nodeId),
                        AttributeId = Attributes.Value
                    }
                };

                _session.Read(
                    null,
                    0,
                    TimestampsToReturn.Both,
                    nodesToRead,
                    out DataValueCollection results,
                    out DiagnosticInfoCollection diagnosticInfos);

                if (results != null && results.Count > 0)
                {
                    var result = results[0];
                    if (StatusCode.IsGood(result.StatusCode))
                    {
                        return result.Value;
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] 읽기 상태 코드: {result.StatusCode}");
                        return null;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 태그 읽기 실패 ({nodeId}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 여러 태그 일괄 읽기
        /// </summary>
        public async Task<Dictionary<string, object?>> ReadTagsAsync(IEnumerable<string> nodeIds)
        {
            var results = new Dictionary<string, object?>();

            if (_session == null || !_session.Connected)
            {
                throw new InvalidOperationException("세션이 연결되지 않았습니다.");
            }

            try
            {
                var nodesToRead = new ReadValueIdCollection();
                var nodeIdList = nodeIds.ToList();

                foreach (var nodeId in nodeIdList)
                {
                    nodesToRead.Add(new ReadValueId
                    {
                        NodeId = new NodeId(nodeId),
                        AttributeId = Attributes.Value
                    });
                }

                _session.Read(
                    null,
                    0,
                    TimestampsToReturn.Both,
                    nodesToRead,
                    out DataValueCollection dataValues,
                    out DiagnosticInfoCollection diagnosticInfos);

                for (int i = 0; i < nodeIdList.Count; i++)
                {
                    if (i < dataValues.Count)
                    {
                        var dataValue = dataValues[i];
                        results[nodeIdList[i]] = StatusCode.IsGood(dataValue.StatusCode) 
                            ? dataValue.Value 
                            : null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 일괄 읽기 실패: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// 모든 설정된 태그 읽기 (DeviceData 반환)
        /// </summary>
        public async Task<DeviceData> ReadAllTagsAsync()
        {
            var deviceData = new DeviceData
            {
                ChannelName = _deviceConfig.ChannelName,
                DeviceName = _deviceConfig.DeviceName,
                Timestamp = DateTime.UtcNow,
                IsConnected = IsConnected
            };

            if (!IsConnected)
            {
                return deviceData;
            }

            try
            {
                var nodeIds = _deviceConfig.Tags.Select(t => t.NodeId).ToList();
                var values = await ReadTagsAsync(nodeIds);

                foreach (var tag in _deviceConfig.Tags)
                {
                    if (values.TryGetValue(tag.NodeId, out var value) && value != null)
                    {
                        deviceData.Tags[tag.Name] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 전체 태그 읽기 실패: {ex.Message}");
                deviceData.ErrorCode = -1;
            }

            return deviceData;
        }

        #endregion

        #region Write 기능

        /// <summary>
        /// 단일 태그 쓰기
        /// </summary>
        public async Task<bool> WriteTagAsync(string nodeId, object value)
        {
            if (_session == null || !_session.Connected)
            {
                throw new InvalidOperationException("세션이 연결되지 않았습니다.");
            }

            try
            {
                var nodesToWrite = new WriteValueCollection
                {
                    new WriteValue
                    {
                        NodeId = new NodeId(nodeId),
                        AttributeId = Attributes.Value,
                        Value = new DataValue(new Variant(value))
                    }
                };

                _session.Write(
                    null,
                    nodesToWrite,
                    out StatusCodeCollection results,
                    out DiagnosticInfoCollection diagnosticInfos);

                if (results != null && results.Count > 0)
                {
                    if (StatusCode.IsGood(results[0]))
                    {
                        Console.WriteLine($"[SUCCESS] 쓰기 성공: {nodeId} = {value}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] 쓰기 실패: {results[0]}");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 태그 쓰기 실패 ({nodeId}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 태그명으로 쓰기 (설정된 태그 사용)
        /// </summary>
        public async Task<bool> WriteTagByNameAsync(string tagName, object value)
        {
            var tag = _deviceConfig.Tags.FirstOrDefault(t => t.Name == tagName);
            if (tag == null)
            {
                Console.WriteLine($"[ERROR] 태그를 찾을 수 없음: {tagName}");
                return false;
            }

            return await WriteTagAsync(tag.NodeId, value);
        }

        /// <summary>
        /// 여러 태그 일괄 쓰기
        /// </summary>
        public async Task<Dictionary<string, bool>> WriteTagsAsync(Dictionary<string, object> tagsToWrite)
        {
            var results = new Dictionary<string, bool>();

            if (_session == null || !_session.Connected)
            {
                throw new InvalidOperationException("세션이 연결되지 않았습니다.");
            }

            try
            {
                var nodesToWrite = new WriteValueCollection();
                var nodeIdList = tagsToWrite.Keys.ToList();

                foreach (var kvp in tagsToWrite)
                {
                    nodesToWrite.Add(new WriteValue
                    {
                        NodeId = new NodeId(kvp.Key),
                        AttributeId = Attributes.Value,
                        Value = new DataValue(new Variant(kvp.Value))
                    });
                }

                _session.Write(
                    null,
                    nodesToWrite,
                    out StatusCodeCollection statusCodes,
                    out DiagnosticInfoCollection diagnosticInfos);

                for (int i = 0; i < nodeIdList.Count; i++)
                {
                    if (i < statusCodes.Count)
                    {
                        results[nodeIdList[i]] = StatusCode.IsGood(statusCodes[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 일괄 쓰기 실패: {ex.Message}");
            }

            return results;
        }

        #endregion

        #region Subscription (실시간 모니터링)

        /// <summary>
        /// 데이터 변경 구독 시작
        /// </summary>
        public bool Subscribe(int publishingInterval = 100)
        {
            if (_session == null || !_session.Connected)
            {
                throw new InvalidOperationException("세션이 연결되지 않았습니다.");
            }

            try
            {
                // 기존 구독 제거
                if (_subscription != null)
                {
                    _session.RemoveSubscription(_subscription);
                }

                // 새 구독 생성
                _subscription = new Subscription(_session.DefaultSubscription)
                {
                    DisplayName = "KepOpcUaClient_Subscription",
                    PublishingEnabled = true,
                    PublishingInterval = publishingInterval,
                    LifetimeCount = 1000,
                    KeepAliveCount = 10,
                    MaxNotificationsPerPublish = 1000,
                    Priority = 100
                };

                // 모니터링 아이템 추가
                foreach (var tag in _deviceConfig.Tags)
                {
                    var monitoredItem = new MonitoredItem(_subscription.DefaultItem)
                    {
                        DisplayName = tag.Name,
                        StartNodeId = new NodeId(tag.NodeId),
                        AttributeId = Attributes.Value,
                        SamplingInterval = publishingInterval,
                        QueueSize = 10,
                        DiscardOldest = true
                    };

                    monitoredItem.Notification += MonitoredItem_Notification;
                    _subscription.AddItem(monitoredItem);
                }

                // 세션에 구독 추가
                _session.AddSubscription(_subscription);
                _subscription.Create();

                Console.WriteLine($"[SUCCESS] 구독 시작 (태그 수: {_deviceConfig.Tags.Count})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 구독 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 구독 중지
        /// </summary>
        public void Unsubscribe()
        {
            if (_subscription != null && _session != null)
            {
                _session.RemoveSubscription(_subscription);
                _subscription = null;
                Console.WriteLine("[INFO] 구독 중지됨");
            }
        }

        private void MonitoredItem_Notification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            try
            {
                if (e.NotificationValue is MonitoredItemNotification notification)
                {
                    var eventArgs = new DataChangedEventArgs
                    {
                        TagName = item.DisplayName,
                        NodeId = item.StartNodeId.ToString(),
                        Value = notification.Value.Value,
                        StatusCode = notification.Value.StatusCode,
                        Timestamp = notification.Value.SourceTimestamp
                    };

                    DataChanged?.Invoke(this, eventArgs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 알림 처리 오류: {ex.Message}");
            }
        }

        #endregion

        #region Browse 기능

        /// <summary>
        /// 노드 탐색
        /// </summary>
        public async Task<List<ReferenceDescription>> BrowseAsync(string nodeId = "ns=0;i=85")
        {
            var results = new List<ReferenceDescription>();

            if (_session == null || !_session.Connected)
            {
                throw new InvalidOperationException("세션이 연결되지 않았습니다.");
            }

            try
            {
                _session.Browse(
                    null,
                    null,
                    new NodeId(nodeId),
                    0,
                    BrowseDirection.Forward,
                    ReferenceTypeIds.HierarchicalReferences,
                    true,
                    (uint)NodeClass.Variable | (uint)NodeClass.Object,
                    out byte[] continuationPoint,
                    out ReferenceDescriptionCollection references);

                if (references != null)
                {
                    results.AddRange(references);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Browse 실패: {ex.Message}");
            }

            return results;
        }

        #endregion

        #region Helper Methods

        private ApplicationConfiguration CreateApplicationConfiguration()
        {
            var config = new ApplicationConfiguration
            {
                ApplicationName = "KepOpcUaClient",
                ApplicationUri = "urn:localhost:KepOpcUaClient",
                ApplicationType = ApplicationType.Client,
                ProductUri = "http://kepware.opcua.client",

                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "./pki/own",
                        SubjectName = "KepOpcUaClient"
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "./pki/issuer"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "./pki/trusted"
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "./pki/rejected"
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },

                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 15000,
                    MaxStringLength = 1048576,
                    MaxByteStringLength = 4194304,
                    MaxArrayLength = 65535,
                    MaxMessageSize = 4194304,
                    MaxBufferSize = 65535,
                    ChannelLifetime = 300000,
                    SecurityTokenLifetime = 3600000
                },

                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = 60000,
                    WellKnownDiscoveryUrls = new StringCollection
                    {
                        "opc.tcp://{0}:4840"
                    }
                },

                TraceConfiguration = new TraceConfiguration()
            };

            return config;
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                DisconnectAsync().Wait();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 데이터 변경 이벤트 인자
    /// </summary>
    public class DataChangedEventArgs : EventArgs
    {
        public string TagName { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public object? Value { get; set; }
        public StatusCode StatusCode { get; set; }
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] {TagName} = {Value} ({StatusCode})";
        }
    }
}
