using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeviceConnector.Events;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using Microsoft.Extensions.Logging;

namespace DeviceConnector.Services
{
    /// <summary>
    /// 통합 디바이스 허브 서비스
    /// OPC UA (KEPServerEX) + ROS_ControlHub를 통합 관리
    /// 
    /// [기능]
    /// 1. OPC UA를 통한 직접 디바이스 통신 (ESP32 → KEPServerEX → DeviceConnector)
    /// 2. ROS_ControlHub와의 gRPC/SignalR 통신 (DeviceConnector ↔ ROS_ControlHub)
    /// 3. 양방향 상태 동기화 및 이벤트 전파
    /// </summary>
    public class IntegratedDeviceHub : IDisposable
    {
        #region Private Fields

        private readonly IOpcUaClientService _opcService;
        private readonly IRosControlHubClient _rosHubClient;
        private readonly ILogger<IntegratedDeviceHub>? _logger;
        
        private CancellationTokenSource? _syncCts;
        private bool _disposed;
        private bool _isSyncRunning;

        #endregion

        #region Properties

        /// <summary>OPC UA 연결 상태</summary>
        public bool IsOpcConnected => _opcService.IsConnected;

        /// <summary>ROS_ControlHub gRPC 연결 상태</summary>
        public bool IsRosGrpcConnected => _rosHubClient.IsGrpcConnected;

        /// <summary>ROS_ControlHub SignalR 연결 상태</summary>
        public bool IsRosSignalRConnected => _rosHubClient.IsSignalRConnected;

        /// <summary>모든 연결 활성화 여부</summary>
        public bool IsFullyConnected => IsOpcConnected && IsRosGrpcConnected && IsRosSignalRConnected;

        /// <summary>OPC UA에서 마지막으로 읽은 데이터</summary>
        public ESP32Data? LastOpcData => _opcService.LastData;

        /// <summary>ROS_ControlHub에서 마지막으로 수신한 상태</summary>
        public SystemStateEventArgs? LastRosState { get; private set; }

        #endregion

        #region Events

        /// <summary>OPC UA 데이터 변경 시</summary>
        public event EventHandler<DataChangedEventArgs>? OpcDataChanged;

        /// <summary>ROS_ControlHub 상태 업데이트 시</summary>
        public event EventHandler<SystemStateEventArgs>? RosStateUpdated;

        /// <summary>연결 상태 변경 시</summary>
        public event EventHandler<IntegratedConnectionEventArgs>? ConnectionChanged;

        /// <summary>에러 발생 시</summary>
        public event EventHandler<IntegratedErrorEventArgs>? ErrorOccurred;

        #endregion

        #region Constructor

        public IntegratedDeviceHub(
            IOpcUaClientService opcService,
            IRosControlHubClient rosHubClient,
            ILogger<IntegratedDeviceHub>? logger = null)
        {
            _opcService = opcService ?? throw new ArgumentNullException(nameof(opcService));
            _rosHubClient = rosHubClient ?? throw new ArgumentNullException(nameof(rosHubClient));
            _logger = logger;

            // 이벤트 핸들러 연결
            _opcService.DataChanged += OnOpcDataChanged;
            _opcService.ConnectionChanged += OnOpcConnectionChanged;
            _opcService.ErrorOccurred += OnOpcError;

            _rosHubClient.SystemStateUpdated += OnRosStateUpdated;
            _rosHubClient.ConnectionChanged += OnRosConnectionChanged;
            _rosHubClient.ErrorOccurred += OnRosError;
        }

        #endregion

        #region 연결 관리

        /// <summary>
        /// 모든 서비스 연결 시작
        /// </summary>
        /// <param name="joinRosGroup">ROS_ControlHub 상태 그룹 자동 참가</param>
        public async Task<bool> ConnectAllAsync(bool joinRosGroup = true)
        {
            _logger?.LogInformation("통합 연결 시작...");

            var results = new List<(string Service, bool Success)>();

            // 1. OPC UA 연결
            try
            {
                var opcResult = await _opcService.ConnectAsync();
                results.Add(("OPC UA", opcResult));
                _logger?.LogInformation("OPC UA 연결: {Result}", opcResult ? "성공" : "실패");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OPC UA 연결 실패");
                results.Add(("OPC UA", false));
            }

            // 2. ROS_ControlHub 연결
            try
            {
                var rosResult = await _rosHubClient.ConnectAsync();
                results.Add(("ROS_ControlHub", rosResult));
                _logger?.LogInformation("ROS_ControlHub 연결: {Result}", rosResult ? "성공" : "실패");

                if (rosResult && joinRosGroup)
                {
                    await _rosHubClient.JoinStateGroupAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ROS_ControlHub 연결 실패");
                results.Add(("ROS_ControlHub", false));
            }

            var allSuccess = results.TrueForAll(r => r.Success);
            _logger?.LogInformation("통합 연결 완료: {Result}", allSuccess ? "모두 성공" : "일부 실패");

            return allSuccess;
        }

        /// <summary>
        /// 모든 서비스 연결 해제
        /// </summary>
        public async Task DisconnectAllAsync()
        {
            _logger?.LogInformation("통합 연결 해제 중...");

            StopStateSync();

            await _rosHubClient.DisconnectAsync();
            await _opcService.DisconnectAsync();

            _logger?.LogInformation("통합 연결 해제 완료");
        }

        #endregion

        #region 상태 동기화

        /// <summary>
        /// OPC UA → ROS_ControlHub 상태 동기화 시작
        /// OPC UA에서 읽은 데이터를 ROS_ControlHub로 전송
        /// </summary>
        /// <param name="intervalMs">동기화 주기 (밀리초)</param>
        public void StartStateSync(int intervalMs = 500)
        {
            if (_isSyncRunning) return;

            _syncCts = new CancellationTokenSource();
            _isSyncRunning = true;

            Task.Run(async () =>
            {
                _logger?.LogInformation("상태 동기화 시작: {Interval}ms 주기", intervalMs);

                while (!_syncCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        // OPC UA에서 데이터 읽기
                        var data = await _opcService.ReadDataAsync();

                        if (data != null && IsRosGrpcConnected)
                        {
                            // 상태 변경이 있으면 ROS_ControlHub에 알림
                            // 여기서는 gRPC를 통해 명시적 업데이트는 하지 않고,
                            // ROS_ControlHub가 자체적으로 OPC UA를 통해 읽도록 함
                            // 필요시 커스텀 gRPC 메서드 추가 가능
                        }

                        await Task.Delay(intervalMs, _syncCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "상태 동기화 중 오류");
                        await Task.Delay(intervalMs * 2, _syncCts.Token);
                    }
                }

                _isSyncRunning = false;
                _logger?.LogInformation("상태 동기화 중지됨");
            }, _syncCts.Token);
        }

        /// <summary>
        /// 상태 동기화 중지
        /// </summary>
        public void StopStateSync()
        {
            _syncCts?.Cancel();
        }

        #endregion

        #region 명령 전송

        /// <summary>
        /// OPC UA를 통해 디바이스에 직접 명령 전송
        /// </summary>
        public Task<bool> SendOpcCommandAsync(string deviceId, string tagName, object value)
        {
            return _opcService.WriteCommandAsync(deviceId, tagName, value);
        }

        /// <summary>
        /// ROS_ControlHub를 통해 디바이스 명령 전송
        /// </summary>
        public Task<DeviceCommandResult> SendRosCommandAsync(string deviceId, string command, string payloadJson = "{}")
        {
            return _rosHubClient.SetDeviceStateAsync(deviceId, command, payloadJson);
        }

        /// <summary>
        /// 디바이스 시작 명령 (양쪽 모두 전송)
        /// </summary>
        public async Task<bool> StartDeviceAsync(string deviceId)
        {
            _logger?.LogInformation("디바이스 시작: {DeviceId}", deviceId);

            // OPC UA로 직접 명령
            var opcResult = await _opcService.WriteCommandAsync(deviceId, DeviceTagConfig.TAG_STATUS, (ushort)DeviceState.RUNNING);

            // ROS_ControlHub로도 명령 전송
            var rosResult = await _rosHubClient.SetDeviceStateAsync(deviceId, "start");

            return opcResult && rosResult.Success;
        }

        /// <summary>
        /// 디바이스 정지 명령 (양쪽 모두 전송)
        /// </summary>
        public async Task<bool> StopDeviceAsync(string deviceId)
        {
            _logger?.LogInformation("디바이스 정지: {DeviceId}", deviceId);

            var opcResult = await _opcService.WriteCommandAsync(deviceId, DeviceTagConfig.TAG_STATUS, (ushort)DeviceState.IDLE);
            var rosResult = await _rosHubClient.SetDeviceStateAsync(deviceId, "stop");

            return opcResult && rosResult.Success;
        }

        #endregion

        #region 이벤트 핸들러

        private void OnOpcDataChanged(object? sender, DataChangedEventArgs e)
        {
            _logger?.LogDebug("OPC 데이터 변경: {DeviceId}", e.DeviceId);
            OpcDataChanged?.Invoke(this, e);
        }

        private void OnOpcConnectionChanged(object? sender, ConnectionChangedEventArgs e)
        {
            _logger?.LogInformation("OPC 연결 상태 변경: {State}", e.Status.State);
            RaiseConnectionChanged();
        }

        private void OnOpcError(object? sender, ErrorOccurredEventArgs e)
        {
            _logger?.LogWarning("OPC 에러: {Message}", e.Message);
            ErrorOccurred?.Invoke(this, new IntegratedErrorEventArgs
            {
                Source = "OPC UA",
                Message = e.Message,
                Exception = e.Exception,
                IsRecoverable = e.IsRecoverable
            });
        }

        private void OnRosStateUpdated(object? sender, SystemStateEventArgs e)
        {
            LastRosState = e;
            _logger?.LogDebug("ROS 상태 수신: {DeviceName} - {Status}", e.DeviceName, e.DeviceStatus);
            RosStateUpdated?.Invoke(this, e);
        }

        private void OnRosConnectionChanged(object? sender, HubConnectionChangedEventArgs e)
        {
            _logger?.LogInformation("ROS 연결 상태 변경: gRPC={Grpc}, SignalR={SignalR}", e.IsGrpcConnected, e.IsSignalRConnected);
            RaiseConnectionChanged();
        }

        private void OnRosError(object? sender, HubErrorEventArgs e)
        {
            _logger?.LogWarning("ROS 에러 ({Source}): {Message}", e.Source, e.Message);
            ErrorOccurred?.Invoke(this, new IntegratedErrorEventArgs
            {
                Source = $"ROS_ControlHub ({e.Source})",
                Message = e.Message,
                Exception = e.Exception,
                IsRecoverable = true
            });
        }

        private void RaiseConnectionChanged()
        {
            ConnectionChanged?.Invoke(this, new IntegratedConnectionEventArgs
            {
                IsOpcConnected = IsOpcConnected,
                IsRosGrpcConnected = IsRosGrpcConnected,
                IsRosSignalRConnected = IsRosSignalRConnected
            });
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
                // 이벤트 핸들러 해제
                _opcService.DataChanged -= OnOpcDataChanged;
                _opcService.ConnectionChanged -= OnOpcConnectionChanged;
                _opcService.ErrorOccurred -= OnOpcError;

                _rosHubClient.SystemStateUpdated -= OnRosStateUpdated;
                _rosHubClient.ConnectionChanged -= OnRosConnectionChanged;
                _rosHubClient.ErrorOccurred -= OnRosError;

                _syncCts?.Cancel();
                _syncCts?.Dispose();

                _rosHubClient?.Dispose();
                _opcService?.Dispose();
            }

            _disposed = true;
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// 통합 연결 상태 이벤트
    /// </summary>
    public class IntegratedConnectionEventArgs : EventArgs
    {
        public bool IsOpcConnected { get; set; }
        public bool IsRosGrpcConnected { get; set; }
        public bool IsRosSignalRConnected { get; set; }
        public bool IsFullyConnected => IsOpcConnected && IsRosGrpcConnected && IsRosSignalRConnected;
    }

    /// <summary>
    /// 통합 에러 이벤트
    /// </summary>
    public class IntegratedErrorEventArgs : EventArgs
    {
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public bool IsRecoverable { get; set; }
    }

    #endregion
}
