using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ROS_ControlHub.TestData
{
    /// <summary>
    /// ROS_ControlHub gRPC 서버와 통신하는 클라이언트
    /// FakeDataWriterService가 KEPServerEX에 쓴 데이터를 
    /// ROS_ControlHub를 통해 gRPC로 읽어올 수 있습니다.
    /// 
    /// 데이터 구조 규칙 (2026-02-28까지 유지):
    /// - DeviceName: 장비 이름
    /// - ChannelName: KEPServerEX 채널명
    /// - Tags: 장비별 태그 딕셔너리
    /// </summary>
    public class RosControlHubClient : IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly HttpClient _httpClient;
        private readonly string _grpcEndpoint;
        private readonly string _restEndpoint;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="grpcEndpoint">gRPC 엔드포인트 (기본: http://localhost:5178)</param>
        /// <param name="restEndpoint">REST API 엔드포인트 (기본: http://localhost:5178)</param>
        public RosControlHubClient(string grpcEndpoint = "http://localhost:5178", string? restEndpoint = null)
        {
            _grpcEndpoint = grpcEndpoint;
            _restEndpoint = restEndpoint ?? grpcEndpoint;

            // gRPC 채널 생성 (HTTP/2)
            _channel = GrpcChannel.ForAddress(_grpcEndpoint, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true,
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
                }
            });

            // REST API용 HttpClient
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_restEndpoint),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Console.WriteLine($"[RosControlHubClient] Initialized");
            Console.WriteLine($"  gRPC: {_grpcEndpoint}");
            Console.WriteLine($"  REST: {_restEndpoint}");
        }

        #region gRPC 메서드 (Proto 기반)

        /// <summary>
        /// 장비 상태 설정 (Start/Stop/Reset)
        /// gRPC: SetDeviceState
        /// </summary>
        public async Task<DeviceResult> SetDeviceStateAsync(string deviceName, string command, CancellationToken ct = default)
        {
            try
            {
                var client = new Control.ControlService.ControlServiceClient(_channel);
                var request = new Control.DeviceCommand
                {
                    DeviceName = deviceName,
                    Command = command
                };

                var response = await client.SetDeviceStateAsync(request, cancellationToken: ct);

                Console.WriteLine($"[gRPC] SetDeviceState: {deviceName} -> {command} = {response.Success}");

                return new DeviceResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    DeviceName = response.DeviceName
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[gRPC] SetDeviceState Error: {ex.Message}");
                return new DeviceResult { Success = false, Message = ex.Message, DeviceName = deviceName };
            }
        }

        /// <summary>
        /// 모든 장비에 명령 전송
        /// gRPC: SetAllDevicesState
        /// </summary>
        public async Task<GlobalResult> SetAllDevicesStateAsync(string command, CancellationToken ct = default)
        {
            try
            {
                var client = new Control.ControlService.ControlServiceClient(_channel);
                var request = new Control.GlobalCommand { Command = command };

                var response = await client.SetAllDevicesStateAsync(request, cancellationToken: ct);

                Console.WriteLine($"[gRPC] SetAllDevicesState: {command} = {response.Success}");

                return new GlobalResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[gRPC] SetAllDevicesState Error: {ex.Message}");
                return new GlobalResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// AGV 이동 명령
        /// gRPC: MoveAgv
        /// </summary>
        public async Task<DeviceResult> MoveAgvAsync(string deviceName, double targetX, double targetY, CancellationToken ct = default)
        {
            try
            {
                var client = new Control.ControlService.ControlServiceClient(_channel);
                var request = new Control.AgvMoveCommand
                {
                    DeviceName = deviceName,
                    TargetX = targetX,
                    TargetY = targetY
                };

                var response = await client.MoveAgvAsync(request, cancellationToken: ct);

                Console.WriteLine($"[gRPC] MoveAgv: {deviceName} -> ({targetX}, {targetY}) = {response.Success}");

                return new DeviceResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    DeviceName = response.DeviceName
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[gRPC] MoveAgv Error: {ex.Message}");
                return new DeviceResult { Success = false, Message = ex.Message, DeviceName = deviceName };
            }
        }

        #endregion

        #region REST API 메서드

        /// <summary>
        /// 장비 상태 조회 (REST API)
        /// GET /api/state/{deviceName}
        /// </summary>
        public async Task<DeviceStateResponse?> GetDeviceStateAsync(string deviceName, CancellationToken ct = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/state/{deviceName}", ct);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<DeviceStateResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    Console.WriteLine($"[REST] GetDeviceState: {deviceName} = OK");
                    return result;
                }
                else
                {
                    Console.WriteLine($"[REST] GetDeviceState: {deviceName} = {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[REST] GetDeviceState Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 모든 장비 상태 조회 (REST API)
        /// GET /api/state
        /// </summary>
        public async Task<List<DeviceStateResponse>?> GetAllDeviceStatesAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/state", ct);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<List<DeviceStateResponse>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    Console.WriteLine($"[REST] GetAllDeviceStates: {result?.Count ?? 0} devices");
                    return result;
                }
                else
                {
                    Console.WriteLine($"[REST] GetAllDeviceStates: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[REST] GetAllDeviceStates Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 장비 제어 (REST API)
        /// POST /api/control/{deviceName}
        /// </summary>
        public async Task<bool> ControlDeviceAsync(string deviceName, string command, CancellationToken ct = default)
        {
            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(new { command }),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"/api/control/{deviceName}", content, ct);

                Console.WriteLine($"[REST] ControlDevice: {deviceName} -> {command} = {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[REST] ControlDevice Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 연결 테스트

        /// <summary>
        /// ROS_ControlHub 서버 연결 테스트
        /// </summary>
        public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
        {
            try
            {
                // REST API 헬스체크
                var response = await _httpClient.GetAsync("/health", ct);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("[Test] ROS_ControlHub connection: OK");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[Test] ROS_ControlHub connection: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test] ROS_ControlHub connection failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            _channel?.Dispose();
            _httpClient?.Dispose();
            Console.WriteLine("[RosControlHubClient] Disposed");
        }

        #endregion
    }

    #region 응답 모델

    /// <summary>
    /// 장비 제어 결과
    /// </summary>
    public class DeviceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 전체 장비 제어 결과
    /// </summary>
    public class GlobalResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 장비 상태 응답
    /// </summary>
    public class DeviceStateResponse
    {
        public string DeviceName { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
        public Dictionary<string, object>? Tags { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion
}
