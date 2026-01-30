namespace DeviceConnector.Services;

using DeviceConnector.Events;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using System.Collections.Concurrent;

/// <summary>
/// OPC UA 클라이언트 서비스 구현
/// KEPServerEX를 통한 ESP32 ModbusTCP 통신
/// 
/// ┌─────────────────────────────────────────────────────────────────────┐
/// │ v2.2 변경사항                                                       │
/// │ - TargetA 태그: Holding Register Bit(40007.0) → Coil(00007)        │
/// │ - Modbus FC05 (Write Single Coil) 사용                             │
/// │ - Boolean Write 안정성 개선                                        │
/// └─────────────────────────────────────────────────────────────────────┘
/// </summary>
public class OpcUaClientService : IOpcUaClientService
{
    #region Private Fields

    private readonly OpcUaConnectionInfo _connectionInfo;
    private readonly ILogger<OpcUaClientService>? _logger;
    private readonly ConcurrentDictionary<string, DeviceTagConfig> _deviceConfigs = new();
    private readonly ConcurrentDictionary<string, ESP32Data> _deviceDataCache = new();
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();

    private Session? _session;
    private bool _disposed;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private CancellationTokenSource? _reconnectCts;

    #endregion

    #region Events

    public event EventHandler<DataChangedEventArgs>? DataChanged;
    public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;
    public event EventHandler<WriteCompletedEventArgs>? WriteCompleted;
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    #endregion

    #region Properties

    public bool IsConnected => _session?.Connected ?? false;
    public ConnectionStatus Status { get; private set; } = new();

    #endregion

    #region Constructor

    public OpcUaClientService(OpcUaConnectionInfo connectionInfo, ILogger<OpcUaClientService>? logger = null)
    {
        _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
        _logger = logger;
    }

    #endregion

    #region 연결 관리

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                _logger?.LogInformation("Already connected to OPC UA server");
                return true;
            }

            UpdateConnectionState(ConnectionState.Connecting);

            // 애플리케이션 설정
            var config = new ApplicationConfiguration
            {
                ApplicationName = _connectionInfo.ApplicationName,
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier(),
                    AutoAcceptUntrustedCertificates = true
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = _connectionInfo.SessionTimeout
                }
            };

            await config.Validate(ApplicationType.Client);

            // 엔드포인트 선택
            var endpoint = CoreClientUtils.SelectEndpoint(
                _connectionInfo.EndpointUrl,
                useSecurity: _connectionInfo.SecurityPolicy != "None");

            var endpointConfig = EndpointConfiguration.Create(config);
            var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfig);

            // 사용자 인증
            UserIdentity userIdentity;
            if (!string.IsNullOrEmpty(_connectionInfo.Username))
            {
                userIdentity = new UserIdentity(_connectionInfo.Username, _connectionInfo.Password);
            }
            else
            {
                userIdentity = new UserIdentity(new AnonymousIdentityToken());
            }

            // 세션 생성
            _session = await Session.Create(
                config,
                configuredEndpoint,
                false,
                _connectionInfo.ApplicationName,
                (uint)_connectionInfo.SessionTimeout,
                userIdentity,
                null);

            _session.KeepAlive += Session_KeepAlive;

            UpdateConnectionState(ConnectionState.Connected);
            _logger?.LogInformation("Connected to OPC UA server: {Url}", _connectionInfo.EndpointUrl);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to OPC UA server");
            UpdateConnectionState(ConnectionState.Error, ex.Message);
            OnErrorOccurred(new ErrorOccurredEventArgs($"Connection failed: {ex.Message}", ex));
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            _reconnectCts?.Cancel();

            if (_session != null)
            {
                _session.KeepAlive -= Session_KeepAlive;

                // 모든 구독 제거
                foreach (var sub in _subscriptions.Values)
                {
                    try { _session.RemoveSubscription(sub); } catch { }
                }
                _subscriptions.Clear();

                await _session.CloseAsync();
                _session.Dispose();
                _session = null;
            }

            UpdateConnectionState(ConnectionState.Disconnected);
            _logger?.LogInformation("Disconnected from OPC UA server");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (e.Status != null && ServiceResult.IsBad(e.Status))
        {
            _logger?.LogWarning("KeepAlive failed: {Status}", e.Status);
            UpdateConnectionState(ConnectionState.Reconnecting);

            if (_connectionInfo.AutoReconnect)
            {
                _ = ReconnectAsync();
            }
        }
    }

    private async Task ReconnectAsync()
    {
        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;

        while (!token.IsCancellationRequested && !IsConnected)
        {
            Status.ReconnectAttempts++;
            _logger?.LogInformation("Reconnect attempt #{Attempt}", Status.ReconnectAttempts);

            try
            {
                await Task.Delay(_connectionInfo.ReconnectInterval, token);
                if (await ConnectAsync(token))
                {
                    Status.ReconnectAttempts = 0;
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Reconnect attempt failed");
            }
        }
    }

    private void UpdateConnectionState(ConnectionState newState, string? error = null)
    {
        var previousState = Status.State;
        Status.State = newState;
        Status.ServerUrl = _connectionInfo.EndpointUrl;
        Status.LastError = error;

        if (newState == ConnectionState.Connected)
        {
            Status.LastConnectedTime = DateTime.UtcNow;
        }

        ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(Status, previousState));
    }

    #endregion

    #region 디바이스 관리

    public void AddDeviceConfig(DeviceTagConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        _deviceConfigs[config.DeviceId] = config;
        _logger?.LogInformation("Added device config: {DeviceId}", config.DeviceId);
    }

    public void RemoveDeviceConfig(string deviceId)
    {
        _deviceConfigs.TryRemove(deviceId, out _);
        _deviceDataCache.TryRemove(deviceId, out _);
        _logger?.LogInformation("Removed device config: {DeviceId}", deviceId);
    }

    public IReadOnlyList<string> GetRegisteredDevices() =>
        _deviceConfigs.Keys.ToList().AsReadOnly();

    #endregion

    #region 데이터 읽기

    public async Task<ESP32Data?> ReadDeviceDataAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return null;
        }

        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
        {
            _logger?.LogWarning("Device config not found: {DeviceId}", deviceId);
            return null;
        }

        try
        {
            var nodesToRead = new ReadValueIdCollection
            {
                new ReadValueId { NodeId = new NodeId(config.GetNodeId(config.Tags.PosX)), AttributeId = Attributes.Value },
                new ReadValueId { NodeId = new NodeId(config.GetNodeId(config.Tags.PosY)), AttributeId = Attributes.Value },
                new ReadValueId { NodeId = new NodeId(config.GetNodeId(config.Tags.PosTheta)), AttributeId = Attributes.Value },
                new ReadValueId { NodeId = new NodeId(config.GetNodeId(config.Tags.TargetA)), AttributeId = Attributes.Value },
                new ReadValueId { NodeId = new NodeId(config.GetNodeId(config.Tags.Control)), AttributeId = Attributes.Value },
                new ReadValueId { NodeId = new NodeId(config.GetNodeId(config.Tags.State)), AttributeId = Attributes.Value }
            };

            _session.Read(
                null,
                0,
                TimestampsToReturn.Both,
                nodesToRead,
                out DataValueCollection results,
                out DiagnosticInfoCollection diagnostics);

            var data = new ESP32Data
            {
                DeviceId = deviceId,
                ChannelName = config.ChannelName,
                DeviceName = config.DeviceName,
                PosX = GetFloatValue(results[0]),
                PosY = GetFloatValue(results[1]),
                PosTheta = GetFloatValue(results[2]),
                TargetA = GetBoolValue(results[3]),
                Control = GetStringValue(results[4]),
                State = GetStringValue(results[5]),
                Timestamp = DateTime.UtcNow,
                IsGoodQuality = results.All(r => StatusCode.IsGood(r.StatusCode))
            };

            _deviceDataCache[deviceId] = data;
            return data;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read device data: {DeviceId}", deviceId);
            OnErrorOccurred(new ErrorOccurredEventArgs($"Read failed for {deviceId}: {ex.Message}", ex));
            return null;
        }
    }

    public async Task<Dictionary<string, ESP32Data>> ReadAllDevicesDataAsync(CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, ESP32Data>();

        foreach (var deviceId in _deviceConfigs.Keys)
        {
            var data = await ReadDeviceDataAsync(deviceId, cancellationToken);
            if (data != null)
            {
                result[deviceId] = data;
            }
        }

        return result;
    }

    private static float GetFloatValue(DataValue dataValue)
    {
        if (StatusCode.IsGood(dataValue.StatusCode) && dataValue.Value != null)
        {
            return Convert.ToSingle(dataValue.Value);
        }
        return 0f;
    }

    private static bool GetBoolValue(DataValue dataValue)
    {
        if (StatusCode.IsGood(dataValue.StatusCode) && dataValue.Value != null)
        {
            return Convert.ToBoolean(dataValue.Value);
        }
        return false;
    }

    private static string GetStringValue(DataValue dataValue)
    {
        if (StatusCode.IsGood(dataValue.StatusCode) && dataValue.Value != null)
        {
            return dataValue.Value.ToString() ?? string.Empty;
        }
        return string.Empty;
    }

    #endregion

    #region 데이터 쓰기

    /// <summary>
    /// TargetA 태그에 Boolean 값 쓰기
    /// ※ v2.2: Coil 주소(00007) 사용 - FC05 Write Single Coil
    /// KEPServerEX에서 TargetA 태그 주소를 00007 (Coil)로 설정 필요
    /// </summary>
    public async Task<bool> WriteTargetAAsync(string deviceId, bool value, CancellationToken cancellationToken = default)
    {
        return await WriteTagAsync(deviceId, ESP32Tags.TARGET_A, value, cancellationToken);
    }

    public async Task<bool> WriteControlAsync(string deviceId, string value, CancellationToken cancellationToken = default)
    {
        return await WriteTagAsync(deviceId, ESP32Tags.CONTROL, value, cancellationToken);
    }

    public async Task<bool> WriteStateAsync(string deviceId, string value, CancellationToken cancellationToken = default)
    {
        return await WriteTagAsync(deviceId, ESP32Tags.STATE, value, cancellationToken);
    }

    public async Task<bool> WriteTagAsync(string deviceId, string tagName, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            OnWriteCompleted(deviceId, tagName, value, false, "Not connected");
            return false;
        }

        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
        {
            _logger?.LogWarning("Device config not found: {DeviceId}", deviceId);
            OnWriteCompleted(deviceId, tagName, value, false, "Device config not found");
            return false;
        }

        try
        {
            // 태그 이름으로 실제 설정된 태그 이름 가져오기
            var actualTagName = GetActualTagName(config.Tags, tagName);
            var nodeId = config.GetNodeId(actualTagName);

            var writeValue = new WriteValue
            {
                NodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };

            var writeValues = new WriteValueCollection { writeValue };

            _session.Write(
                null,
                writeValues,
                out StatusCodeCollection results,
                out DiagnosticInfoCollection diagnostics);

            var success = StatusCode.IsGood(results[0]);

            if (success)
            {
                _logger?.LogInformation("Write success: {DeviceId}.{Tag} = {Value}", deviceId, tagName, value);
            }
            else
            {
                _logger?.LogWarning("Write failed: {DeviceId}.{Tag} StatusCode={StatusCode}", 
                    deviceId, tagName, results[0]);
            }

            OnWriteCompleted(deviceId, tagName, value, success, 
                success ? null : $"StatusCode: {results[0]}");

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Write exception: {DeviceId}.{Tag}", deviceId, tagName);
            OnWriteCompleted(deviceId, tagName, value, false, ex.Message);
            OnErrorOccurred(new ErrorOccurredEventArgs($"Write failed: {ex.Message}", ex));
            return false;
        }
    }

    private static string GetActualTagName(DeviceTagNames tags, string tagName)
    {
        return tagName.ToUpperInvariant() switch
        {
            "POS_X" or "POSX" => tags.PosX,
            "POS_Y" or "POSY" => tags.PosY,
            "POS_T" or "POSTHETA" => tags.PosTheta,
            "TARGETA" or "TARGET_A" => tags.TargetA,
            "CONTROL" => tags.Control,
            "STATE" => tags.State,
            _ => tagName
        };
    }

    private void OnWriteCompleted(string deviceId, string tagName, object value, bool success, string? error)
    {
        WriteCompleted?.Invoke(this, new WriteCompletedEventArgs(deviceId, tagName, value, success, error));
    }

    #endregion

    #region 구독 관리

    public async Task StartSubscriptionAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return;
        }

        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
        {
            _logger?.LogWarning("Device config not found: {DeviceId}", deviceId);
            return;
        }

        if (_subscriptions.ContainsKey(deviceId))
        {
            _logger?.LogInformation("Subscription already exists for: {DeviceId}", deviceId);
            return;
        }

        try
        {
            var subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = _connectionInfo.PublishingInterval,
                DisplayName = $"Sub_{deviceId}"
            };

            // Read 태그만 구독 (PosX, PosY, PosTheta)
            var monitoredItems = new List<MonitoredItem>
            {
                CreateMonitoredItem(config.GetNodeId(config.Tags.PosX), $"{deviceId}.PosX"),
                CreateMonitoredItem(config.GetNodeId(config.Tags.PosY), $"{deviceId}.PosY"),
                CreateMonitoredItem(config.GetNodeId(config.Tags.PosTheta), $"{deviceId}.PosTheta"),
                // TargetA도 구독하여 Write 결과 확인
                CreateMonitoredItem(config.GetNodeId(config.Tags.TargetA), $"{deviceId}.TargetA")
            };

            subscription.AddItems(monitoredItems);
            _session.AddSubscription(subscription);
            subscription.Create();

            foreach (var item in monitoredItems)
            {
                item.Notification += (sender, e) => OnMonitoredItemNotification(deviceId, config, sender, e);
            }

            _subscriptions[deviceId] = subscription;
            _logger?.LogInformation("Subscription started for: {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start subscription: {DeviceId}", deviceId);
            OnErrorOccurred(new ErrorOccurredEventArgs($"Subscription failed: {ex.Message}", ex));
        }
    }

    private MonitoredItem CreateMonitoredItem(string nodeId, string displayName)
    {
        return new MonitoredItem
        {
            StartNodeId = new NodeId(nodeId),
            AttributeId = Attributes.Value,
            DisplayName = displayName,
            SamplingInterval = _connectionInfo.SamplingInterval,
            QueueSize = 1,
            DiscardOldest = true
        };
    }

    private void OnMonitoredItemNotification(string deviceId, DeviceTagConfig config, 
        MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            if (e.NotificationValue is not MonitoredItemNotification notification)
                return;

            var dataValue = notification.Value;
            
            // 캐시된 데이터 업데이트
            if (!_deviceDataCache.TryGetValue(deviceId, out var data))
            {
                data = new ESP32Data
                {
                    DeviceId = deviceId,
                    ChannelName = config.ChannelName,
                    DeviceName = config.DeviceName
                };
            }

            // 태그별 값 업데이트
            var tagPart = item.DisplayName.Split('.').LastOrDefault();
            switch (tagPart)
            {
                case "PosX":
                    data.PosX = GetFloatValue(dataValue);
                    break;
                case "PosY":
                    data.PosY = GetFloatValue(dataValue);
                    break;
                case "PosTheta":
                    data.PosTheta = GetFloatValue(dataValue);
                    break;
                case "TargetA":
                    data.TargetA = GetBoolValue(dataValue);
                    break;
            }

            data.Timestamp = DateTime.UtcNow;
            data.IsGoodQuality = StatusCode.IsGood(dataValue.StatusCode);
            _deviceDataCache[deviceId] = data;

            DataChanged?.Invoke(this, new DataChangedEventArgs(deviceId, data.Clone()));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing notification: {DeviceId}", deviceId);
        }
    }

    public async Task StopSubscriptionAsync(string deviceId)
    {
        if (_subscriptions.TryRemove(deviceId, out var subscription))
        {
            try
            {
                _session?.RemoveSubscription(subscription);
                _logger?.LogInformation("Subscription stopped for: {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping subscription: {DeviceId}", deviceId);
            }
        }
    }

    public async Task StartAllSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var deviceId in _deviceConfigs.Keys)
        {
            await StartSubscriptionAsync(deviceId, cancellationToken);
        }
    }

    public async Task StopAllSubscriptionsAsync()
    {
        foreach (var deviceId in _subscriptions.Keys.ToList())
        {
            await StopSubscriptionAsync(deviceId);
        }
    }

    #endregion

    #region Helper

    private void OnErrorOccurred(ErrorOccurredEventArgs e)
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
            _connectionLock.Dispose();
            DisconnectAsync().GetAwaiter().GetResult();
        }

        _disposed = true;
    }

    #endregion
}
