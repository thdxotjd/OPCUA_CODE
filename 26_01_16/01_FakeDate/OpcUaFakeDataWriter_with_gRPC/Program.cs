using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ROS_ControlHub.TestData;

namespace OpcUaFakeDataWriter
{
    /// <summary>
    /// OPC UA 가짜 데이터 쓰기 + ROS_ControlHub gRPC 통신 테스트 콘솔
    /// 
    /// 1. KEPServerEX에 가짜 데이터 쓰기 (FakeDataWriterService)
    /// 2. ROS_ControlHub와 gRPC 통신 (RosControlHubClient)
    /// </summary>
    class Program
    {
        private const string DEFAULT_OPCUA_ENDPOINT = "opc.tcp://localhost:49320";
        private const string DEFAULT_GRPC_ENDPOINT = "http://localhost:5178";

        static async Task Main(string[] args)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("OPC UA 가짜 데이터 + ROS_ControlHub gRPC 테스트");
            Console.WriteLine("============================================================\n");

            // 모드 선택
            Console.WriteLine("모드 선택:");
            Console.WriteLine("1. OPC UA 가짜 데이터 쓰기 (KEPServerEX)");
            Console.WriteLine("2. ROS_ControlHub gRPC 클라이언트");
            Console.WriteLine("3. 둘 다 (통합 테스트)");
            Console.Write("\n선택 [3]: ");
            
            var modeInput = Console.ReadLine();
            var mode = string.IsNullOrEmpty(modeInput) ? 3 : int.Parse(modeInput);

            FakeDataWriterService? opcuaService = null;
            RosControlHubClient? grpcClient = null;

            try
            {
                // OPC UA 연결
                if (mode == 1 || mode == 3)
                {
                    Console.Write($"\nOPC UA Endpoint [{DEFAULT_OPCUA_ENDPOINT}]: ");
                    var opcuaEndpoint = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(opcuaEndpoint))
                        opcuaEndpoint = DEFAULT_OPCUA_ENDPOINT;

                    Console.WriteLine($"Connecting to OPC UA: {opcuaEndpoint}...");
                    opcuaService = new FakeDataWriterService(opcuaEndpoint);
                    Console.WriteLine("OPC UA Connected!\n");
                }

                // gRPC 연결
                if (mode == 2 || mode == 3)
                {
                    Console.Write($"gRPC Endpoint [{DEFAULT_GRPC_ENDPOINT}]: ");
                    var grpcEndpoint = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(grpcEndpoint))
                        grpcEndpoint = DEFAULT_GRPC_ENDPOINT;

                    grpcClient = new RosControlHubClient(grpcEndpoint);
                    Console.WriteLine("gRPC Client Ready!\n");
                }

                // 메뉴 실행
                await RunMenu(opcuaService, grpcClient);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n연결 실패: {ex.Message}");
            }
            finally
            {
                opcuaService?.Dispose();
                grpcClient?.Dispose();
            }

            Console.WriteLine("\n종료합니다. 아무 키나 누르세요...");
            Console.ReadKey();
        }

        static async Task RunMenu(FakeDataWriterService? opcua, RosControlHubClient? grpc)
        {
            while (true)
            {
                Console.WriteLine("\n==================== 메뉴 ====================");
                
                if (opcua != null)
                {
                    Console.WriteLine("--- OPC UA (KEPServerEX에 가짜 데이터 쓰기) ---");
                    Console.WriteLine("1. 모든 장비 초기값 쓰기");
                    Console.WriteLine("2. 단일 태그 값 쓰기");
                    Console.WriteLine("3. ESP32 시작 시나리오");
                    Console.WriteLine("4. ESP32 정지 시나리오");
                    Console.WriteLine("5. 자동 업데이트 시작/중지");
                }

                if (grpc != null)
                {
                    Console.WriteLine("--- gRPC (ROS_ControlHub 통신) ---");
                    Console.WriteLine("11. [gRPC] 연결 테스트");
                    Console.WriteLine("12. [gRPC] 장비 상태 조회 (REST)");
                    Console.WriteLine("13. [gRPC] 모든 장비 상태 조회 (REST)");
                    Console.WriteLine("14. [gRPC] 장비 제어 - Start");
                    Console.WriteLine("15. [gRPC] 장비 제어 - Stop");
                    Console.WriteLine("16. [gRPC] AGV 이동 명령");
                }

                if (opcua != null && grpc != null)
                {
                    Console.WriteLine("--- 통합 테스트 ---");
                    Console.WriteLine("21. OPC UA 쓰기 → gRPC 읽기 테스트");
                }

                Console.WriteLine("================================================");
                Console.WriteLine("0. 종료");
                Console.Write("\n선택: ");

                var input = Console.ReadLine();

                try
                {
                    switch (input)
                    {
                        // OPC UA 메뉴
                        case "1":
                            if (opcua != null) await opcua.WriteInitialValuesAsync();
                            break;
                        case "2":
                            if (opcua != null) await WriteSingleTag(opcua);
                            break;
                        case "3":
                            if (opcua != null) await opcua.SimulateEsp32StartAsync();
                            Console.WriteLine("ESP32 시작됨");
                            break;
                        case "4":
                            if (opcua != null) await opcua.SimulateEsp32StopAsync();
                            Console.WriteLine("ESP32 정지됨");
                            break;
                        case "5":
                            if (opcua != null) ToggleAutoUpdate(opcua);
                            break;

                        // gRPC 메뉴
                        case "11":
                            if (grpc != null) await grpc.TestConnectionAsync();
                            break;
                        case "12":
                            if (grpc != null) await GetDeviceState(grpc);
                            break;
                        case "13":
                            if (grpc != null) await GetAllDeviceStates(grpc);
                            break;
                        case "14":
                            if (grpc != null) await GrpcControlDevice(grpc, "start");
                            break;
                        case "15":
                            if (grpc != null) await GrpcControlDevice(grpc, "stop");
                            break;
                        case "16":
                            if (grpc != null) await GrpcMoveAgv(grpc);
                            break;

                        // 통합 테스트
                        case "21":
                            if (opcua != null && grpc != null) 
                                await IntegrationTest(opcua, grpc);
                            break;

                        case "0":
                            return;

                        default:
                            Console.WriteLine("잘못된 선택입니다.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"오류: {ex.Message}");
                }
            }
        }

        #region OPC UA 메서드

        static async Task WriteSingleTag(FakeDataWriterService service)
        {
            Console.Write("장비 이름 [ESP32_01]: ");
            var deviceName = Console.ReadLine();
            if (string.IsNullOrEmpty(deviceName)) deviceName = "ESP32_01";

            Console.Write("태그 이름 [Speed]: ");
            var tagName = Console.ReadLine();
            if (string.IsNullOrEmpty(tagName)) tagName = "Speed";

            Console.Write("값: ");
            var valueStr = Console.ReadLine() ?? "100";

            object value;
            if (valueStr.Equals("true", StringComparison.OrdinalIgnoreCase))
                value = true;
            else if (valueStr.Equals("false", StringComparison.OrdinalIgnoreCase))
                value = false;
            else if (short.TryParse(valueStr, out short shortVal))
                value = shortVal;
            else if (float.TryParse(valueStr, out float floatVal))
                value = floatVal;
            else
                value = valueStr;

            var result = await service.WriteTagAsync(deviceName, tagName, value);
            Console.WriteLine($"결과: {(result.Success ? "성공" : "실패")} - {result.Message}");
        }

        static bool _autoUpdateRunning = false;
        static void ToggleAutoUpdate(FakeDataWriterService service)
        {
            if (_autoUpdateRunning)
            {
                service.StopAutoUpdate();
                _autoUpdateRunning = false;
                Console.WriteLine("자동 업데이트 중지됨");
            }
            else
            {
                Console.Write("업데이트 간격 ms [1000]: ");
                var input = Console.ReadLine();
                var interval = string.IsNullOrEmpty(input) ? 1000 : int.Parse(input);
                service.StartAutoUpdate(interval);
                _autoUpdateRunning = true;
                Console.WriteLine($"자동 업데이트 시작됨 ({interval}ms 간격)");
            }
        }

        #endregion

        #region gRPC 메서드

        static async Task GetDeviceState(RosControlHubClient client)
        {
            Console.Write("장비 이름 [ESP32_01]: ");
            var deviceName = Console.ReadLine();
            if (string.IsNullOrEmpty(deviceName)) deviceName = "ESP32_01";

            var state = await client.GetDeviceStateAsync(deviceName);
            if (state != null)
            {
                Console.WriteLine($"\n=== {state.DeviceName} ({state.ChannelName}) ===");
                Console.WriteLine($"Timestamp: {state.Timestamp}");
                if (state.Tags != null)
                {
                    foreach (var tag in state.Tags)
                    {
                        Console.WriteLine($"  {tag.Key}: {tag.Value}");
                    }
                }
            }
            else
            {
                Console.WriteLine("상태 조회 실패");
            }
        }

        static async Task GetAllDeviceStates(RosControlHubClient client)
        {
            var states = await client.GetAllDeviceStatesAsync();
            if (states != null)
            {
                foreach (var state in states)
                {
                    Console.WriteLine($"\n=== {state.DeviceName} ({state.ChannelName}) ===");
                    if (state.Tags != null)
                    {
                        foreach (var tag in state.Tags)
                        {
                            Console.WriteLine($"  {tag.Key}: {tag.Value}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("상태 조회 실패");
            }
        }

        static async Task GrpcControlDevice(RosControlHubClient client, string command)
        {
            Console.Write("장비 이름 [ESP32_01]: ");
            var deviceName = Console.ReadLine();
            if (string.IsNullOrEmpty(deviceName)) deviceName = "ESP32_01";

            var result = await client.SetDeviceStateAsync(deviceName, command);
            Console.WriteLine($"결과: {(result.Success ? "성공" : "실패")} - {result.Message}");
        }

        static async Task GrpcMoveAgv(RosControlHubClient client)
        {
            Console.Write("AGV 이름 [AGV_01]: ");
            var deviceName = Console.ReadLine();
            if (string.IsNullOrEmpty(deviceName)) deviceName = "AGV_01";

            Console.Write("목표 X: ");
            var targetX = double.Parse(Console.ReadLine() ?? "50");

            Console.Write("목표 Y: ");
            var targetY = double.Parse(Console.ReadLine() ?? "30");

            var result = await client.MoveAgvAsync(deviceName, targetX, targetY);
            Console.WriteLine($"결과: {(result.Success ? "성공" : "실패")} - {result.Message}");
        }

        #endregion

        #region 통합 테스트

        static async Task IntegrationTest(FakeDataWriterService opcua, RosControlHubClient grpc)
        {
            Console.WriteLine("\n=== 통합 테스트: OPC UA 쓰기 → gRPC 읽기 ===\n");

            // 1. OPC UA로 가짜 데이터 쓰기
            Console.WriteLine("1. OPC UA: ESP32_01.Speed = 150 쓰기...");
            await opcua.WriteTagAsync("ESP32_01", "Speed", (short)150);
            await opcua.WriteTagAsync("ESP32_01", "Running", true);
            await opcua.WriteTagAsync("ESP32_01", "Temperature", 35.5f);

            // 2. 잠시 대기
            Console.WriteLine("2. 1초 대기...");
            await Task.Delay(1000);

            // 3. gRPC로 데이터 읽기
            Console.WriteLine("3. gRPC: ESP32_01 상태 읽기...");
            var state = await grpc.GetDeviceStateAsync("ESP32_01");

            if (state != null && state.Tags != null)
            {
                Console.WriteLine("\n=== 결과 ===");
                Console.WriteLine($"Speed: {state.Tags.GetValueOrDefault("Speed")} (예상: 150)");
                Console.WriteLine($"Running: {state.Tags.GetValueOrDefault("Running")} (예상: True)");
                Console.WriteLine($"Temperature: {state.Tags.GetValueOrDefault("Temperature")} (예상: 35.5)");
                Console.WriteLine("\n통합 테스트 완료!");
            }
            else
            {
                Console.WriteLine("gRPC 읽기 실패 - ROS_ControlHub가 실행 중인지 확인하세요.");
            }
        }

        #endregion
    }
}
