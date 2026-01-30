namespace DeviceConnector.Services;

using DeviceConnector.Events;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

/// <summary>
/// STM_yolo OPC UA 클라이언트 서비스 구현
/// KEPServerEX를 통한 STM_yolo 디바이스 통신
/// 
/// ┌─────────────────────────────────────────────────────────────────────────┐
/// │ 태그 주소 체계                                                          │
/// │ - Write Tags (Target): ns=2;i=40001 ~ 40008                            │
/// │ - Read Tags (Current): ns=2;i=50001 ~ 50008                            │
/// │ - 또는 String NodeId: ns=2;s=Channel1_opcua.STM.Stm_yolo.TagName       │
/// └─────────────────────────────────────────────────────────────────────────┘
/// </summary>
public class STMYoloClientService : ISTMYoloClientService
{
    #region Private Fields

    private readonly OpcUaConnectionInfo _connectionInfo;
    private readonly STMYoloTagConfig _tagConfig;
    private readonly ILogger<STMYoloClientService>? _logger;
    
    private Session? _session;
    private Subscription? _subscription;
    private STMYoloData _currentData;
    private bool _disposed;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private CancellationTokenSource? _reconnectCts;

    #endregion

    #region Events

    public event EventHandler<STMYoloDataChangedEventArgs>? DataChanged;
    public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;
    public event EventHandler<WriteCompletedEventArgs>? WriteCompleted;
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    #endregion

    #region Properties

    public bool IsConnected => _session?.Connected ?? false;
    public ConnectionStatus Status { get; private set; } = new();
    public STMYoloData? CurrentData => _currentData?.Clone();

    #endregion

    #region Constructor

    public STMYoloClientService(
        OpcUaConnectionInfo connectionInfo, 
        STMYoloTagConfig? tagConfig = null,
        ILogger<STMYoloClientService>? logger = null)
    {
        _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
        _tagConfig = tagConfig ?? new STMYoloTagConfig();
        _logger = logger;
        _currentData = new STMYoloData
        {
            DeviceId = _tagConfig.DeviceId,
            ChannelName = _tagConfig.ChannelName,
            DeviceName = _tagConfig.DeviceName
        };
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

            var endpoint = CoreClientUtils.SelectEndpoint(
                _connectionInfo.EndpointUrl,
                useSecurity: _connectionInfo.SecurityPolicy != "None");

            var endpointConfig = EndpointConfiguration.Create(config);
            var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfig);

            UserIdentity userIdentity = string.IsNullOrEmpty(_connectionInfo.Username)
                ? new UserIdentity(new AnonymousIdentityToken())
                : new UserIdentity(_connectionInfo.Username, _connectionInfo.Password);

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

            if (_subscription != null && _session != null)
            {
                try { _session.RemoveSubscription(_subscription); } catch { }
                _subscription = null;
            }

            if (_session != null)
            {
                _session.KeepAlive -= Session_KeepAlive;
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
            catch (OperationCanceledException) { break; }
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
            Status.LastConnectedTime = DateTime.UtcNow;

        ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(Status, previousState));
    }

    #endregion

    #region 데이터 읽기

    public async Task<STMYoloData?> ReadAllDataAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return null;
        }

        try
        {
            var nodeIds = GetAllNodeIds();
            var results = await ReadNodesAsync(nodeIds, cancellationToken);

            if (results != null)
            {
                UpdateCurrentData(results);
                return _currentData.Clone();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read all data");
            OnErrorOccurred(new ErrorOccurredEventArgs($"Read failed: {ex.Message}", ex));
        }

        return null;
    }

    public async Task<STMYoloData?> ReadCurrentDataAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null) return null;

        try
        {
            var nodeIds = GetCurrentNodeIds();
            var results = await ReadNodesAsync(nodeIds, cancellationToken);

            if (results != null)
            {
                UpdateCurrentDataFromResults(results, isTargetData: false);
                return _currentData.Clone();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read current data");
        }

        return null;
    }

    public async Task<STMYoloData?> ReadTargetDataAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null) return null;

        try
        {
            var nodeIds = GetTargetNodeIds();
            var results = await ReadNodesAsync(nodeIds, cancellationToken);

            if (results != null)
            {
                UpdateCurrentDataFromResults(results, isTargetData: true);
                return _currentData.Clone();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read target data");
        }

        return null;
    }

    public async Task<object?> ReadTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null) return null;

        try
        {
            var nodeId = _tagConfig.GetNodeId(tagName);
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
                out DiagnosticInfoCollection diagnostics);

            if (results.Count > 0 && StatusCode.IsGood(results[0].StatusCode))
            {
                return results[0].Value;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read tag: {TagName}", tagName);
        }

        return null;
    }

    private async Task<DataValueCollection?> ReadNodesAsync(
        ReadValueIdCollection nodeIds, 
        CancellationToken cancellationToken)
    {
        if (_session == null) return null;

        _session.Read(
            null,
            0,
            TimestampsToReturn.Both,
            nodeIds,
            out DataValueCollection results,
            out DiagnosticInfoCollection diagnostics);

        return results;
    }

    #endregion

    #region 데이터 쓰기

    public Task<bool> WriteTargetStateAsync(long value, CancellationToken cancellationToken = default)
        => WriteTagAsync(STMYoloTagConfig.TagNames.TargetState, value, cancellationToken);

    public Task<bool> WriteTargetSpeedMainAsync(long value, CancellationToken cancellationToken = default)
        => WriteTagAsync(STMYoloTagConfig.TagNames.TargetSpeedMain, value, cancellationToken);

    public Task<bool> WriteTargetSpeedSortAsync(long value, CancellationToken cancellationToken = default)
        => WriteTagAsync(STMYoloTagConfig.TagNames.TargetSpeedSort, value, cancellationToken);

    public Task<bool> WriteTargetSpeedLoadAsync(long value, CancellationToken cancellationToken = default)
        => WriteTagAsync(STMYoloTagConfig.TagNames.TargetSpeedLoad, value, cancellationToken);

    public Task<bool> WriteAgvSortArrivedAsync(bool value, CancellationToken cancellationToken = default)
        => WriteTagAsync(STMYoloTagConfig.TagNames.AgvSortArrived, value, cancellationToken);

    public Task<bool> WriteAgvSortDepartedAsync(bool value, CancellationToken cancellationToken = default)
        => WriteTagAsync(STMYoloTagConfig.TagNames.AgvSortDeparted, value, cancellationToken);

    public Task<bool> WriteAgvLoadArrivedAsync(bool value, CancellationToken cancellationToken = default)
        => WriteTagAsync(STMYoloTagConfig.TagNames.AgvLoadArrived, value, cancellationToken);

    public Task<bool> WriteAgvLoadDepartedAsync(bool value, CancellationToken cancellationToken = default)
        => WriteTagAsync(STMYoloTagConfig.TagNames.AgvLoadDeparted, value, cancellationToken);

    public async Task<bool> WriteAllSpeedsAsync(long main, long sort, long load, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return false;
        }

        try
        {
            var writeValues = new WriteValueCollection
            {
                CreateWriteValue(STMYoloTagConfig.TagNames.TargetSpeedMain, main),
                CreateWriteValue(STMYoloTagConfig.TagNames.TargetSpeedSort, sort),
                CreateWriteValue(STMYoloTagConfig.TagNames.TargetSpeedLoad, load)
            };

            _session.Write(
                null,
                writeValues,
                out StatusCodeCollection results,
                out DiagnosticInfoCollection diagnostics);

            var allSuccess = results.All(r => StatusCode.IsGood(r));

            if (allSuccess)
            {
                _logger?.LogInformation("Write all speeds success: Main={Main}, Sort={Sort}, Load={Load}", 
                    main, sort, load);
            }
            else
            {
                for (int i = 0; i < results.Count; i++)
                {
                    if (!StatusCode.IsGood(results[i]))
                    {
                        _logger?.LogWarning("Write failed for speed[{Index}]: {StatusCode}", i, results[i]);
                    }
                }
            }

            return allSuccess;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to write all speeds");
            OnErrorOccurred(new ErrorOccurredEventArgs($"Write speeds failed: {ex.Message}", ex));
            return false;
        }
    }

    public async Task<bool> WriteTagAsync(string tagName, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return false;
        }

        try
        {
            var nodeId = _tagConfig.GetNodeId(tagName);
            var writeValue = new WriteValue
            {
                NodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };

            _session.Write(
                null,
                new WriteValueCollection { writeValue },
                out StatusCodeCollection results,
                out DiagnosticInfoCollection diagnostics);

            var success = StatusCode.IsGood(results[0]);

            if (success)
            {
                _logger?.LogInformation("Write success: {Tag} = {Value} (NodeId: {NodeId})", tagName, value, nodeId);
            }
            else
            {
                _logger?.LogWarning("Write failed: {Tag} StatusCode={StatusCode} (NodeId: {NodeId})", 
                    tagName, results[0], nodeId);
            }

            OnWriteCompleted(tagName, value, success, success ? null : $"StatusCode: {results[0]}");
            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Write exception: {Tag}", tagName);
            OnWriteCompleted(tagName, value, false, ex.Message);
            OnErrorOccurred(new ErrorOccurredEventArgs($"Write failed: {ex.Message}", ex));
            return false;
        }
    }

    private WriteValue CreateWriteValue(string tagName, object value)
    {
        return new WriteValue
        {
            NodeId = new NodeId(_tagConfig.GetNodeId(tagName)),
            AttributeId = Attributes.Value,
            Value = new DataValue(new Variant(value))
        };
    }

    private void OnWriteCompleted(string tagName, object value, bool success, string? error)
    {
        WriteCompleted?.Invoke(this, new WriteCompletedEventArgs(_tagConfig.DeviceId, tagName, value, success, error));
    }

    #endregion

    #region 구독 관리

    public async Task StartCurrentSubscriptionAsync(CancellationToken cancellationToken = default)
    {
        await StartSubscriptionAsync(GetCurrentNodeIds(), "Current", cancellationToken);
    }

    public async Task StartAllSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        await StartSubscriptionAsync(GetAllNodeIds(), "All", cancellationToken);
    }

    private async Task StartSubscriptionAsync(ReadValueIdCollection nodeIds, string subscriptionName, CancellationToken cancellationToken)
    {
        if (!IsConnected || _session == null)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return;
        }

        if (_subscription != null)
        {
            _logger?.LogInformation("Subscription already exists, removing...");
            try { _session.RemoveSubscription(_subscription); } catch { }
        }

        try
        {
            _subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = _connectionInfo.PublishingInterval,
                DisplayName = $"STMYolo_{subscriptionName}"
            };

            var monitoredItems = nodeIds.Select(n => new MonitoredItem
            {
                StartNodeId = n.NodeId,
                AttributeId = Attributes.Value,
                DisplayName = GetTagNameFromNodeId(n.NodeId.ToString()),
                SamplingInterval = _connectionInfo.SamplingInterval,
                QueueSize = 1,
                DiscardOldest = true
            }).ToList();

            _subscription.AddItems(monitoredItems);
            _session.AddSubscription(_subscription);
            _subscription.Create();

            foreach (var item in monitoredItems)
            {
                item.Notification += OnMonitoredItemNotification;
            }

            _logger?.LogInformation("Subscription started: {Name} with {Count} items", 
                subscriptionName, monitoredItems.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start subscription");
            OnErrorOccurred(new ErrorOccurredEventArgs($"Subscription failed: {ex.Message}", ex));
        }
    }

    public async Task StopSubscriptionAsync()
    {
        if (_subscription != null && _session != null)
        {
            try
            {
                _session.RemoveSubscription(_subscription);
                _subscription = null;
                _logger?.LogInformation("Subscription stopped");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping subscription");
            }
        }
    }

    private void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            if (e.NotificationValue is not MonitoredItemNotification notification)
                return;

            var dataValue = notification.Value;
            var tagName = item.DisplayName;
            var previousValue = GetCurrentValueByTagName(tagName);
            var newValue = dataValue.Value;

            UpdateSingleTagValue(tagName, dataValue);

            _currentData.Timestamp = DateTime.UtcNow;
            _currentData.IsGoodQuality = StatusCode.IsGood(dataValue.StatusCode);

            DataChanged?.Invoke(this, new STMYoloDataChangedEventArgs(
                _tagConfig.DeviceId, 
                _currentData.Clone(), 
                tagName,
                previousValue,
                newValue));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing notification");
        }
    }

    #endregion

    #region Helper Methods

    private ReadValueIdCollection GetAllNodeIds()
    {
        var collection = new ReadValueIdCollection();
        
        // Target Tags
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.TargetState));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.TargetSpeedMain));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.TargetSpeedSort));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.TargetSpeedLoad));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.AgvSortArrived));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.AgvSortDeparted));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.AgvLoadArrived));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.AgvLoadDeparted));
        
        // Current Tags
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.CurrentState));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.CurrentSpeedMain));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.CurrentSpeedSort));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.CurrentSpeedLoad));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.CurrentFloor));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.IsLiftMoving));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.IsRobotWorking));
        collection.Add(CreateReadValueId(STMYoloTagConfig.TagNames.IsRobotDone));

        return collection;
    }

    private ReadValueIdCollection GetCurrentNodeIds()
    {
        var collection = new ReadValueIdCollection
        {
            CreateReadValueId(STMYoloTagConfig.TagNames.CurrentState),
            CreateReadValueId(STMYoloTagConfig.TagNames.CurrentSpeedMain),
            CreateReadValueId(STMYoloTagConfig.TagNames.CurrentSpeedSort),
            CreateReadValueId(STMYoloTagConfig.TagNames.CurrentSpeedLoad),
            CreateReadValueId(STMYoloTagConfig.TagNames.CurrentFloor),
            CreateReadValueId(STMYoloTagConfig.TagNames.IsLiftMoving),
            CreateReadValueId(STMYoloTagConfig.TagNames.IsRobotWorking),
            CreateReadValueId(STMYoloTagConfig.TagNames.IsRobotDone)
        };
        return collection;
    }

    private ReadValueIdCollection GetTargetNodeIds()
    {
        var collection = new ReadValueIdCollection
        {
            CreateReadValueId(STMYoloTagConfig.TagNames.TargetState),
            CreateReadValueId(STMYoloTagConfig.TagNames.TargetSpeedMain),
            CreateReadValueId(STMYoloTagConfig.TagNames.TargetSpeedSort),
            CreateReadValueId(STMYoloTagConfig.TagNames.TargetSpeedLoad),
            CreateReadValueId(STMYoloTagConfig.TagNames.AgvSortArrived),
            CreateReadValueId(STMYoloTagConfig.TagNames.AgvSortDeparted),
            CreateReadValueId(STMYoloTagConfig.TagNames.AgvLoadArrived),
            CreateReadValueId(STMYoloTagConfig.TagNames.AgvLoadDeparted)
        };
        return collection;
    }

    private ReadValueId CreateReadValueId(string tagName)
    {
        return new ReadValueId
        {
            NodeId = new NodeId(_tagConfig.GetNodeId(tagName)),
            AttributeId = Attributes.Value
        };
    }

    private string GetTagNameFromNodeId(string nodeId)
    {
        // ns=2;s=STM.Stm_yolo.TagName 형식에서 TagName 추출
        if (nodeId.Contains(";s="))
        {
            var parts = nodeId.Split('.');
            if (parts.Length >= 3)
            {
                return parts[^1]; // 마지막 부분이 TagName
            }
        }
        return nodeId;
    }

    private void UpdateCurrentData(DataValueCollection results)
    {
        int idx = 0;
        
        // Target Tags
        _currentData.TargetState = GetLongValue(results[idx++]);
        _currentData.TargetSpeedMain = GetLongValue(results[idx++]);
        _currentData.TargetSpeedSort = GetLongValue(results[idx++]);
        _currentData.TargetSpeedLoad = GetLongValue(results[idx++]);
        _currentData.AgvSortArrived = GetBoolValue(results[idx++]);
        _currentData.AgvSortDeparted = GetBoolValue(results[idx++]);
        _currentData.AgvLoadArrived = GetBoolValue(results[idx++]);
        _currentData.AgvLoadDeparted = GetBoolValue(results[idx++]);
        
        // Current Tags
        _currentData.CurrentState = GetLongValue(results[idx++]);
        _currentData.CurrentSpeedMain = GetLongValue(results[idx++]);
        _currentData.CurrentSpeedSort = GetLongValue(results[idx++]);
        _currentData.CurrentSpeedLoad = GetLongValue(results[idx++]);
        _currentData.CurrentFloor = GetLongValue(results[idx++]);
        _currentData.IsLiftMoving = GetBoolValue(results[idx++]);
        _currentData.IsRobotWorking = GetBoolValue(results[idx++]);
        _currentData.IsRobotDone = GetBoolValue(results[idx++]);

        _currentData.Timestamp = DateTime.UtcNow;
        _currentData.IsGoodQuality = results.All(r => StatusCode.IsGood(r.StatusCode));
    }

    private void UpdateCurrentDataFromResults(DataValueCollection results, bool isTargetData)
    {
        int idx = 0;

        if (isTargetData)
        {
            _currentData.TargetState = GetLongValue(results[idx++]);
            _currentData.TargetSpeedMain = GetLongValue(results[idx++]);
            _currentData.TargetSpeedSort = GetLongValue(results[idx++]);
            _currentData.TargetSpeedLoad = GetLongValue(results[idx++]);
            _currentData.AgvSortArrived = GetBoolValue(results[idx++]);
            _currentData.AgvSortDeparted = GetBoolValue(results[idx++]);
            _currentData.AgvLoadArrived = GetBoolValue(results[idx++]);
            _currentData.AgvLoadDeparted = GetBoolValue(results[idx++]);
        }
        else
        {
            _currentData.CurrentState = GetLongValue(results[idx++]);
            _currentData.CurrentSpeedMain = GetLongValue(results[idx++]);
            _currentData.CurrentSpeedSort = GetLongValue(results[idx++]);
            _currentData.CurrentSpeedLoad = GetLongValue(results[idx++]);
            _currentData.CurrentFloor = GetLongValue(results[idx++]);
            _currentData.IsLiftMoving = GetBoolValue(results[idx++]);
            _currentData.IsRobotWorking = GetBoolValue(results[idx++]);
            _currentData.IsRobotDone = GetBoolValue(results[idx++]);
        }

        _currentData.Timestamp = DateTime.UtcNow;
    }

    private void UpdateSingleTagValue(string tagName, DataValue dataValue)
    {
        switch (tagName)
        {
            case "TargetState": _currentData.TargetState = GetLongValue(dataValue); break;
            case "TargetSpeedMain": _currentData.TargetSpeedMain = GetLongValue(dataValue); break;
            case "TargetSpeedSort": _currentData.TargetSpeedSort = GetLongValue(dataValue); break;
            case "TargetSpeedLoad": _currentData.TargetSpeedLoad = GetLongValue(dataValue); break;
            case "AgvSortArrived": _currentData.AgvSortArrived = GetBoolValue(dataValue); break;
            case "AgvSortDeparted": _currentData.AgvSortDeparted = GetBoolValue(dataValue); break;
            case "AgvLoadArrived": _currentData.AgvLoadArrived = GetBoolValue(dataValue); break;
            case "AgvLoadDeparted": _currentData.AgvLoadDeparted = GetBoolValue(dataValue); break;
            case "CurrentState": _currentData.CurrentState = GetLongValue(dataValue); break;
            case "CurrentSpeedMain": _currentData.CurrentSpeedMain = GetLongValue(dataValue); break;
            case "CurrentSpeedSort": _currentData.CurrentSpeedSort = GetLongValue(dataValue); break;
            case "CurrentSpeedLoad": _currentData.CurrentSpeedLoad = GetLongValue(dataValue); break;
            case "CurrentFloor": _currentData.CurrentFloor = GetLongValue(dataValue); break;
            case "IsLiftMoving": _currentData.IsLiftMoving = GetBoolValue(dataValue); break;
            case "IsRobotWorking": _currentData.IsRobotWorking = GetBoolValue(dataValue); break;
            case "IsRobotDone": _currentData.IsRobotDone = GetBoolValue(dataValue); break;
        }
    }

    private object? GetCurrentValueByTagName(string tagName)
    {
        return tagName switch
        {
            "TargetState" => _currentData.TargetState,
            "TargetSpeedMain" => _currentData.TargetSpeedMain,
            "TargetSpeedSort" => _currentData.TargetSpeedSort,
            "TargetSpeedLoad" => _currentData.TargetSpeedLoad,
            "AgvSortArrived" => _currentData.AgvSortArrived,
            "AgvSortDeparted" => _currentData.AgvSortDeparted,
            "AgvLoadArrived" => _currentData.AgvLoadArrived,
            "AgvLoadDeparted" => _currentData.AgvLoadDeparted,
            "CurrentState" => _currentData.CurrentState,
            "CurrentSpeedMain" => _currentData.CurrentSpeedMain,
            "CurrentSpeedSort" => _currentData.CurrentSpeedSort,
            "CurrentSpeedLoad" => _currentData.CurrentSpeedLoad,
            "CurrentFloor" => _currentData.CurrentFloor,
            "IsLiftMoving" => _currentData.IsLiftMoving,
            "IsRobotWorking" => _currentData.IsRobotWorking,
            "IsRobotDone" => _currentData.IsRobotDone,
            _ => null
        };
    }

    private static long GetLongValue(DataValue dv)
    {
        if (dv?.Value == null) return 0;
        return Convert.ToInt64(dv.Value);
    }

    private static bool GetBoolValue(DataValue dv)
    {
        if (dv?.Value == null) return false;
        return Convert.ToBoolean(dv.Value);
    }

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
