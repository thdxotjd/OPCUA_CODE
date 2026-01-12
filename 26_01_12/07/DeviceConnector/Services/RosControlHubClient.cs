using System;
using System.Threading;
using System.Threading.Tasks;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using Grpc.Net.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace DeviceConnector.Services
{
    /// <summary>
    /// ROS_ControlHub 클라이언트 서비스 구현
    /// gRPC를 통한 명령 전송 + SignalR을 통한 실시간 상태 수신
    /// </summary>
    public class RosControlHubClient : IRosControlHubClient
    {
        #region Private Fields

        private readonly RosControlHubConfig _config;
        private readonly ILogger<RosControlHubClient>? _logger;
        
        private GrpcChannel? _grpcChannel;
        private Control.ControlService.ControlServiceClient? _grpcClient;
        private HubConnection? _signalRConnection;
        
        private CancellationTokenSource? _reconnectCts;
        private bool _disposed;
        private int _reconnectAttempts;

        #endregion

        #region Properties

        public bool IsGrpcConnected => _grpcChannel?.State == Grpc.Core.ConnectivityState.Ready;
        public bool IsSignalRConnected => _signalRConnection?.State == HubConnectionState.Connected;
        public string ServerUrl => _config.ServerUrl;

        #endregion

        #region Events

        public event EventHandler<SystemStateEventArgs>? SystemStateUpdated;
        public event EventHandler<HubConnectionChangedEventArgs>? ConnectionChanged;
        public event EventHandler<HubErrorEventArgs>? ErrorOccurred;

        #endregion

        #region Constructor

        public RosControlHubClient(RosControlHubConfig config, ILogger<RosControlHubClient>? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
        }

        #endregion

        #region 연결 관리

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _logger?.LogInformation("ROS_ControlHub 연결 시작: {Url}", _config.ServerUrl);

                // 1. gRPC 채널 생성
                var grpcOptions = new GrpcChannelOptions
                {
                    HttpHandler = new SocketsHttpHandler
                    {
                        EnableMultipleHttp2Connections = true,
                        ConnectTimeout = TimeSpan.FromSeconds(_config.ConnectionTimeoutSeconds)
                    }
                };

                _grpcChannel = GrpcChannel.ForAddress(_config.GrpcUrl, grpcOptions);
                _grpcClient = new Control.ControlService.ControlServiceClient(_grpcChannel);

                // 2. SignalR 연결 생성
                _signalRConnection = new HubConnectionBuilder()
                    .WithUrl(_config.StateHubUrl)
                    .WithAutomaticReconnect(new[] { 
                        TimeSpan.FromSeconds(0), 
                        TimeSpan.FromSeconds(2), 
                        TimeSpan.FromSeconds(5), 
                        TimeSpan.FromSeconds(10) 
                    })
                    .Build();

                // SignalR 이벤트 핸들러 등록
                RegisterSignalRHandlers();

                // 3. SignalR 연결 시작
                await _signalRConnection.StartAsync();

                _reconnectAttempts = 0;
                _logger?.LogInformation("ROS_ControlHub 연결 성공");

                OnConnectionChanged(new HubConnectionChangedEventArgs
                {
                    IsGrpcConnected = true,
                    IsSignalRConnected = true
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ROS_ControlHub 연결 실패");
                
                OnErrorOccurred(new HubErrorEventArgs
                {
                    Message = $"연결 실패: {ex.Message}",
                    Exception = ex,
                    Source = "Connection"
                });

                OnConnectionChanged(new HubConnectionChangedEventArgs
                {
                    IsGrpcConnected = false,
                    IsSignalRConnected = false,
                    ErrorMessage = ex.Message
                });

                if (_config.AutoReconnect)
                {
                    StartReconnectTimer();
                }

                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            _reconnectCts?.Cancel();

            try
            {
                // SignalR 연결 해제
                if (_signalRConnection != null)
                {
                    await _signalRConnection.StopAsync();
                    await _signalRConnection.DisposeAsync();
                    _signalRConnection = null;
                }

                // gRPC 채널 해제
                if (_grpcChannel != null)
                {
                    await _grpcChannel.ShutdownAsync();
                    _grpcChannel.Dispose();
                    _grpcChannel = null;
                    _grpcClient = null;
                }

                _logger?.LogInformation("ROS_ControlHub 연결 해제 완료");

                OnConnectionChanged(new HubConnectionChangedEventArgs
                {
                    IsGrpcConnected = false,
                    IsSignalRConnected = false
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "연결 해제 중 오류");
            }
        }

        private void RegisterSignalRHandlers()
        {
            if (_signalRConnection == null) return;

            // SystemStateUpdated 이벤트 수신
            _signalRConnection.On<SystemStateDto>("SystemStateUpdated", (state) =>
            {
                _logger?.LogDebug("상태 수신: {DeviceName} - {Status}", state.DeviceName, state.DeviceStatus);

                OnSystemStateUpdated(new SystemStateEventArgs
                {
                    Timestamp = state.Timestamp,
                    DeviceName = state.DeviceName,
                    DeviceStatus = state.DeviceStatus,
                    Extensions = state.Extensions
                });
            });

            // 연결 상태 이벤트
            _signalRConnection.Closed += async (error) =>
            {
                _logger?.LogWarning("SignalR 연결 끊김: {Error}", error?.Message);

                OnConnectionChanged(new HubConnectionChangedEventArgs
                {
                    IsGrpcConnected = IsGrpcConnected,
                    IsSignalRConnected = false,
                    ErrorMessage = error?.Message
                });

                if (_config.AutoReconnect && !_disposed)
                {
                    StartReconnectTimer();
                }
            };

            _signalRConnection.Reconnected += (connectionId) =>
            {
                _logger?.LogInformation("SignalR 재연결 성공: {ConnectionId}", connectionId);

                OnConnectionChanged(new HubConnectionChangedEventArgs
                {
                    IsGrpcConnected = true,
                    IsSignalRConnected = true
                });

                return Task.CompletedTask;
            };

            _signalRConnection.Reconnecting += (error) =>
            {
                _logger?.LogInformation("SignalR 재연결 시도 중...");
                return Task.CompletedTask;
            };
        }

        private void StartReconnectTimer()
        {
            if (_reconnectCts != null && !_reconnectCts.IsCancellationRequested)
                return;

            _reconnectCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (!_reconnectCts.Token.IsCancellationRequested)
                {
                    if (_config.MaxReconnectAttempts > 0 && _reconnectAttempts >= _config.MaxReconnectAttempts)
                    {
                        _logger?.LogWarning("최대 재연결 시도 횟수 초과: {Attempts}", _reconnectAttempts);
                        break;
                    }

                    await Task.Delay(_config.ReconnectIntervalSeconds * 1000, _reconnectCts.Token);
                    _reconnectAttempts++;

                    _logger?.LogInformation("재연결 시도 #{Attempt}...", _reconnectAttempts);

                    if (await ConnectAsync())
                    {
                        break;
                    }
                }
            }, _reconnectCts.Token);
        }

        #endregion

        #region gRPC 명령

        public async Task<DeviceCommandResult> SetDeviceStateAsync(string deviceId, string command, string payloadJson = "{}")
        {
            if (_grpcClient == null)
            {
                return new DeviceCommandResult { Success = false, Message = "gRPC 클라이언트가 연결되지 않았습니다." };
            }

            try
            {
                _logger?.LogInformation("디바이스 명령 전송: {DeviceId} - {Command}", deviceId, command);

                var request = new Control.DeviceCommand
                {
                    DeviceId = deviceId,
                    Command = command,
                    PayloadJson = payloadJson
                };

                var response = await _grpcClient.SetDeviceStateAsync(request);

                return new DeviceCommandResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "디바이스 명령 전송 실패");
                OnErrorOccurred(new HubErrorEventArgs
                {
                    Message = $"명령 전송 실패: {ex.Message}",
                    Exception = ex,
                    Source = "gRPC"
                });

                return new DeviceCommandResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<GlobalCommandResult> SetAllDevicesStateAsync(string command)
        {
            if (_grpcClient == null)
            {
                return new GlobalCommandResult { Success = false, Message = "gRPC 클라이언트가 연결되지 않았습니다." };
            }

            try
            {
                _logger?.LogInformation("전체 디바이스 명령 전송: {Command}", command);

                var request = new Control.GlobalCommand { Command = command };
                var response = await _grpcClient.SetAllDevicesStateAsync(request);

                return new GlobalCommandResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    AffectedCount = response.AffectedCount
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "전체 명령 전송 실패");
                OnErrorOccurred(new HubErrorEventArgs
                {
                    Message = $"전체 명령 전송 실패: {ex.Message}",
                    Exception = ex,
                    Source = "gRPC"
                });

                return new GlobalCommandResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<DeviceCommandResult> MoveAgvAsync(string deviceId, double x, double y)
        {
            if (_grpcClient == null)
            {
                return new DeviceCommandResult { Success = false, Message = "gRPC 클라이언트가 연결되지 않았습니다." };
            }

            try
            {
                _logger?.LogInformation("AGV 이동 명령: {DeviceId} -> ({X}, {Y})", deviceId, x, y);

                var request = new Control.AgvMoveCommand
                {
                    DeviceId = deviceId,
                    TargetType = "coordinate",
                    X = x,
                    Y = y
                };

                var response = await _grpcClient.MoveAgvAsync(request);

                return new DeviceCommandResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AGV 이동 명령 전송 실패");
                return new DeviceCommandResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<DeviceCommandResult> MoveAgvToPointAsync(string deviceId, string pointName)
        {
            if (_grpcClient == null)
            {
                return new DeviceCommandResult { Success = false, Message = "gRPC 클라이언트가 연결되지 않았습니다." };
            }

            try
            {
                _logger?.LogInformation("AGV 포인트 이동: {DeviceId} -> {Point}", deviceId, pointName);

                var request = new Control.AgvMoveCommand
                {
                    DeviceId = deviceId,
                    TargetType = "point",
                    PointName = pointName
                };

                var response = await _grpcClient.MoveAgvAsync(request);

                return new DeviceCommandResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AGV 포인트 이동 명령 전송 실패");
                return new DeviceCommandResult { Success = false, Message = ex.Message };
            }
        }

        #endregion

        #region SignalR 그룹 관리

        public async Task JoinStateGroupAsync(string groupName = "default")
        {
            if (_signalRConnection == null || _signalRConnection.State != HubConnectionState.Connected)
            {
                throw new InvalidOperationException("SignalR이 연결되지 않았습니다.");
            }

            await _signalRConnection.InvokeAsync("JoinGroup", groupName);
            _logger?.LogInformation("상태 그룹 참가: {Group}", groupName);
        }

        public async Task LeaveStateGroupAsync(string groupName = "default")
        {
            if (_signalRConnection == null || _signalRConnection.State != HubConnectionState.Connected)
            {
                return;
            }

            await _signalRConnection.InvokeAsync("LeaveGroup", groupName);
            _logger?.LogInformation("상태 그룹 퇴장: {Group}", groupName);
        }

        #endregion

        #region 이벤트 발생

        protected virtual void OnSystemStateUpdated(SystemStateEventArgs e)
        {
            SystemStateUpdated?.Invoke(this, e);
        }

        protected virtual void OnConnectionChanged(HubConnectionChangedEventArgs e)
        {
            ConnectionChanged?.Invoke(this, e);
        }

        protected virtual void OnErrorOccurred(HubErrorEventArgs e)
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

        #region DTO for SignalR

        /// <summary>
        /// SignalR에서 수신하는 시스템 상태 DTO
        /// ROS_ControlHub의 SystemStateDto와 동일한 구조
        /// </summary>
        private class SystemStateDto
        {
            public DateTimeOffset Timestamp { get; set; }
            public string DeviceName { get; set; } = string.Empty;
            public string DeviceStatus { get; set; } = string.Empty;
            public Dictionary<string, object> Extensions { get; set; } = new();
        }

        #endregion
    }
}
