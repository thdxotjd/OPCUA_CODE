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
/// </summary>
public class OpcUaClientService : IOpcUaClientService
{
    private readonly OpcUaConnectionInfo _connectionInfo;
    private readonly ILogger<OpcUaClientService>? _logger;
    private readonly ConcurrentDictionary<string, DeviceTagConfig> _deviceConfigs = new();
    private readonly ConcurrentDictionary<string, ESP32Data> _deviceDataCache = new();
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();

    private Session? _session;
    private bool _disposed;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private CancellationTokenSource? _reconnectCts;

    #region Events

    public event EventHandler<DataChangedEventArgs>? DataChanged;
    public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;
    public event EventHandler<WriteCompletedEventArgs>? WriteCompleted;

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

    #region Connection

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                _logger?.LogInformation("Already connected to OPC UA server");
                return;
            }

            _logger?.LogInformation("Connecting to OPC UA server: {Endpoint}", _connectionInfo.EndpointUrl);

            // 애플리케이션 설정
            var config = new ApplicationConfiguration
            {
                ApplicationName = _connectionInfo.ApplicationName,
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier(),
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = false
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = (int)_connectionInfo.SessionTimeout }
            };

            await config.Validate(ApplicationType.Client);

            // 엔드포인트 선택
            var endpoint = CoreClientUtils.SelectEndpoint(_connectionInfo.EndpointUrl, useSecurity: false);
            var endpointConfig = EndpointConfiguration.Create(config);
            var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfig);

            // 세션 생성
            _session = await Session.Create(
                config,
                configuredEndpoint,
                false,
                _connectionInfo.ApplicationName,
                _connectionInfo.SessionTimeout,
                new UserIdentity(new AnonymousIdentityToken()),
                null
            );

            _session.KeepAlive += Session_KeepAlive;

            Status = new ConnectionStatus
            {
                IsConnected = true,
                EndpointUrl = _connectionInfo.EndpointUrl,
                ConnectedAt = DateTime.UtcNow
            };

            _logger?.LogInformation("Connected to OPC UA server successfully");
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(true, _connectionInfo.EndpointUrl));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to OPC UA server");
            Status = new ConnectionStatus
            {
                IsConnected = false,
                LastError = ex.Message,
                DisconnectedAt = DateTime.UtcNow
            };
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(false, _connectionInfo.EndpointUrl, ex.Message));
            throw;
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

                foreach (var subscription in _subscriptions.Values)
                {
                    try { subscription.Delete(true); } catch { }
                }
                _subscriptions.Clear();

                await _session.CloseAsync();
                _session.Dispose();
                _session = null;
            }

            Status = new ConnectionStatus
            {
                IsConnected = false,
                DisconnectedAt = DateTime.UtcNow
            };

            _logger?.LogInformation("Disconnected from OPC UA server");
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(false));
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
            _logger?.LogWarning("Session KeepAlive error: {Status}", e.Status);
            Status.IsConnected = false;
            Status.LastError = e.Status.ToString();
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(false, _connectionInfo.EndpointUrl, e.Status.ToString()));

            if (_connectionInfo.AutoReconnect)
            {
                _ = TryReconnectAsync();
            }
        }
    }

    private async Task TryReconnectAsync()
    {
        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();

        while (!_reconnectCts.Token.IsCancellationRequested && !IsConnected)
        {
            try
            {
                Status.ReconnectAttempts++;
                _logger?.LogInformation("Attempting to reconnect... (attempt {Attempt})", Status.ReconnectAttempts);

                await Task.Delay(_connectionInfo.ReconnectIntervalMs, _reconnectCts.Token);
                await ConnectAsync(_reconnectCts.Token);

                // 재연결 후 구독 복원
                foreach (var deviceId in _deviceConfigs.Keys)
                {
                    await StartSubscriptionAsync(deviceId, _reconnectCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Reconnect attempt failed");
            }
        }
    }

    #endregion

    #region Device Configuration

    public void AddDeviceConfig(DeviceTagConfig config)
    {
        if (string.IsNullOrEmpty(config.DeviceId))
            throw new ArgumentException("DeviceId is required", nameof(config));

        _deviceConfigs[config.DeviceId] = config;
        _deviceDataCache[config.DeviceId] = new ESP32Data
        {
            DeviceId = config.DeviceId,
            ChannelName = config.ChannelName,
            DeviceName = config.DeviceName
        };

        _logger?.LogInformation("Added device config: {DeviceId} ({Channel}.{Device})",
            config.DeviceId, config.ChannelName, config.DeviceName);
    }

    public void RemoveDeviceConfig(string deviceId)
    {
        _deviceConfigs.TryRemove(deviceId, out _);
        _deviceDataCache.TryRemove(deviceId, out _);
        _logger?.LogInformation("Removed device config: {DeviceId}", deviceId);
    }

    public IEnumerable<string> GetRegisteredDeviceIds() => _deviceConfigs.Keys;

    #endregion

    #region Read Operations

    public async Task<ESP32Data?> ReadDeviceDataAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
        {
            _logger?.LogWarning("Device not found: {DeviceId}", deviceId);
            return null;
        }

        if (_session == null || !IsConnected)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return null;
        }

        try
        {
            var nodeIds = config.GetAllNodeIds();
            var nodesToRead = new ReadValueIdCollection();

            foreach (var tagName in ESP32Tags.AllTags)
            {
                nodesToRead.Add(new ReadValueId
                {
                    NodeId = new NodeId(nodeIds[tagName]),
                    AttributeId = Attributes.Value
                });
            }

            _session.Read(null, 0, TimestampsToReturn.Both, nodesToRead,
                out DataValueCollection results, out DiagnosticInfoCollection _);

            var data = new ESP32Data
            {
                DeviceId = deviceId,
                ChannelName = config.ChannelName,
                DeviceName = config.DeviceName,
                Timestamp = DateTime.UtcNow,
                IsGoodQuality = true
            };

            // Read Only Tags
            if (results[0].StatusCode == StatusCodes.Good)
                data.PosX = Convert.ToSingle(results[0].Value);
            if (results[1].StatusCode == StatusCodes.Good)
                data.PosY = Convert.ToSingle(results[1].Value);
            if (results[2].StatusCode == StatusCodes.Good)
                data.PosT = Convert.ToSingle(results[2].Value);

            // Writable Tags
            if (results[3].StatusCode == StatusCodes.Good)
                data.TargetA = Convert.ToBoolean(results[3].Value);
            if (results[4].StatusCode == StatusCodes.Good)
                data.Control = results[4].Value?.ToString() ?? string.Empty;
            if (results[5].StatusCode == StatusCodes.Good)
                data.State = results[5].Value?.ToString() ?? string.Empty;

            _deviceDataCache[deviceId] = data;
            return data;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read device data: {DeviceId}", deviceId);
            return null;
        }
    }

    public async Task<(float PosX, float PosY, float PosT)?> ReadPositionAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
            return null;

        if (_session == null || !IsConnected)
            return null;

        try
        {
            var nodeIds = config.GetAllNodeIds();
            var nodesToRead = new ReadValueIdCollection
            {
                new ReadValueId { NodeId = new NodeId(nodeIds[ESP32Tags.POS_X]), AttributeId = Attributes.Value },
                new ReadValueId { NodeId = new NodeId(nodeIds[ESP32Tags.POS_Y]), AttributeId = Attributes.Value },
                new ReadValueId { NodeId = new NodeId(nodeIds[ESP32Tags.POS_T]), AttributeId = Attributes.Value }
            };

            _session.Read(null, 0, TimestampsToReturn.Both, nodesToRead,
                out DataValueCollection results, out DiagnosticInfoCollection _);

            float posX = Convert.ToSingle(results[0].Value);
            float posY = Convert.ToSingle(results[1].Value);
            float posT = Convert.ToSingle(results[2].Value);

            return (posX, posY, posT);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read position: {DeviceId}", deviceId);
            return null;
        }
    }

    public async Task<string?> ReadStateAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
        {
            _logger?.LogWarning("Device not found: {DeviceId}", deviceId);
            return null;
        }

        if (_session == null || !IsConnected)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return null;
        }

        try
        {
            var nodeId = config.GetNodeId(ESP32Tags.STATE);
            var nodesToRead = new ReadValueIdCollection
            {
                new ReadValueId { NodeId = new NodeId(nodeId), AttributeId = Attributes.Value }
            };

            _session.Read(null, 0, TimestampsToReturn.Both, nodesToRead,
                out DataValueCollection results, out DiagnosticInfoCollection _);

            if (results[0].StatusCode == StatusCodes.Good)
            {
                return results[0].Value?.ToString() ?? string.Empty;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read state: {DeviceId}", deviceId);
            return null;
        }
    }

    #endregion

    #region Write Operations

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

    private async Task<bool> WriteTagAsync(string deviceId, string tagName, object value, CancellationToken cancellationToken)
    {
        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
        {
            _logger?.LogWarning("Device not found: {DeviceId}", deviceId);
            return false;
        }

        if (_session == null || !IsConnected)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return false;
        }

        try
        {
            var nodeId = config.GetNodeId(tagName);
            var writeValue = new WriteValue
            {
                NodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };

            var nodesToWrite = new WriteValueCollection { writeValue };

            _session.Write(null, nodesToWrite, out StatusCodeCollection results, out DiagnosticInfoCollection _);

            bool success = results[0] == StatusCodes.Good;

            _logger?.LogInformation("Write {Tag} = {Value} -> {Result}",
                tagName, value, success ? "Success" : results[0].ToString());

            WriteCompleted?.Invoke(this, new WriteCompletedEventArgs(
                deviceId, tagName, value, success,
                success ? null : results[0].ToString()));

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to write {Tag}: {DeviceId}", tagName, deviceId);
            WriteCompleted?.Invoke(this, new WriteCompletedEventArgs(deviceId, tagName, value, false, ex.Message));
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> WriteMultipleAsync(string deviceId, Dictionary<string, object> tagValues, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, bool>();

        foreach (var kvp in tagValues)
        {
            results[kvp.Key] = await WriteTagAsync(deviceId, kvp.Key, kvp.Value, cancellationToken);
        }

        return results;
    }

    #endregion

    #region Subscription

    public async Task StartSubscriptionAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
        {
            _logger?.LogWarning("Device not found: {DeviceId}", deviceId);
            return;
        }

        if (_session == null || !IsConnected)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return;
        }

        // 기존 구독 제거
        if (_subscriptions.TryRemove(deviceId, out var existingSub))
        {
            try { existingSub.Delete(true); } catch { }
        }

        try
        {
            var subscription = new Subscription(_session.DefaultSubscription)
            {
                DisplayName = $"Subscription_{deviceId}",
                PublishingInterval = _connectionInfo.PublishingIntervalMs,
                KeepAliveCount = 10,
                LifetimeCount = 100,
                MaxNotificationsPerPublish = 1000,
                PublishingEnabled = true
            };

            _session.AddSubscription(subscription);
            subscription.Create();

            var nodeIds = config.GetAllNodeIds();

            foreach (var tagName in ESP32Tags.AllTags)
            {
                var monitoredItem = new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = tagName,
                    StartNodeId = new NodeId(nodeIds[tagName]),
                    AttributeId = Attributes.Value,
                    SamplingInterval = _connectionInfo.PublishingIntervalMs,
                    QueueSize = 1,
                    DiscardOldest = true
                };

                monitoredItem.Notification += (item, e) => OnMonitoredItemNotification(deviceId, item, e);
                subscription.AddItem(monitoredItem);
            }

            subscription.ApplyChanges();
            _subscriptions[deviceId] = subscription;

            _logger?.LogInformation("Started subscription for device: {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start subscription: {DeviceId}", deviceId);
            throw;
        }
    }

    private void OnMonitoredItemNotification(string deviceId, MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        if (e.NotificationValue is not MonitoredItemNotification notification)
            return;

        if (!_deviceDataCache.TryGetValue(deviceId, out var data))
            return;

        var tagName = item.DisplayName;
        var value = notification.Value.Value;
        var changedTags = new List<string> { tagName };

        switch (tagName)
        {
            case ESP32Tags.POS_X:
                data.PosX = Convert.ToSingle(value);
                break;
            case ESP32Tags.POS_Y:
                data.PosY = Convert.ToSingle(value);
                break;
            case ESP32Tags.POS_T:
                data.PosT = Convert.ToSingle(value);
                break;
            case ESP32Tags.TARGET_A:
                data.TargetA = Convert.ToBoolean(value);
                break;
            case ESP32Tags.CONTROL:
                data.Control = value?.ToString() ?? string.Empty;
                break;
            case ESP32Tags.STATE:
                data.State = value?.ToString() ?? string.Empty;
                break;
        }

        data.Timestamp = DateTime.UtcNow;
        data.IsGoodQuality = notification.Value.StatusCode == StatusCodes.Good;

        DataChanged?.Invoke(this, new DataChangedEventArgs(deviceId, data, changedTags));
    }

    public async Task StopSubscriptionAsync(string deviceId)
    {
        if (_subscriptions.TryRemove(deviceId, out var subscription))
        {
            try
            {
                subscription.Delete(true);
                _logger?.LogInformation("Stopped subscription for device: {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error stopping subscription: {DeviceId}", deviceId);
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

    #region Dispose

    public void Dispose()
    {
        if (_disposed) return;

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();

        DisconnectAsync().Wait();
        _connectionLock.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
