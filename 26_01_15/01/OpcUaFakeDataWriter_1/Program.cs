using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ROS_ControlHub.TestData;

namespace OpcUaFakeDataWriter
{
    /// <summary>
    /// OPC UA 가짜 데이터 쓰기 테스트 콘솔
    /// KEPServerEX에 연결하여 가짜 값을 씁니다.
    /// </summary>
    class Program
    {
        // KEPServerEX OPC UA 엔드포인트 (환경에 맞게 수정)
        private const string DEFAULT_ENDPOINT = "opc.tcp://localhost:49320";

        static async Task Main(string[] args)
        {
            Console.WriteLine("==========================================");
            Console.WriteLine("OPC UA 가짜 데이터 쓰기 테스트");
            Console.WriteLine("==========================================\n");

            // 엔드포인트 입력
            Console.Write($"OPC UA Endpoint [{DEFAULT_ENDPOINT}]: ");
            var endpoint = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(endpoint))
                endpoint = DEFAULT_ENDPOINT;

            FakeDataWriterService? service = null;

            try
            {
                Console.WriteLine($"\nConnecting to {endpoint}...");
                service = new FakeDataWriterService(endpoint);
                Console.WriteLine("Connected!\n");

                await RunMenu(service);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n연결 실패: {ex.Message}");
                Console.WriteLine("\nKEPServerEX가 실행 중인지 확인하세요.");
                Console.WriteLine("OPC UA Configuration에서 엔드포인트가 활성화되어 있는지 확인하세요.");
            }
            finally
            {
                service?.Dispose();
            }

            Console.WriteLine("\n종료합니다. 아무 키나 누르세요...");
            Console.ReadKey();
        }

        static async Task RunMenu(FakeDataWriterService service)
        {
            while (true)
            {
                Console.WriteLine("\n========== 메뉴 ==========");
                Console.WriteLine("1. 모든 장비 초기값 쓰기");
                Console.WriteLine("2. 단일 태그 값 쓰기");
                Console.WriteLine("3. 여러 태그 값 쓰기");
                Console.WriteLine("4. 랜덤 데이터 쓰기 (단일 장비)");
                Console.WriteLine("5. 랜덤 데이터 쓰기 (모든 장비)");
                Console.WriteLine("------- 시나리오 -------");
                Console.WriteLine("6. ESP32 시작 시나리오");
                Console.WriteLine("7. ESP32 정지 시나리오");
                Console.WriteLine("8. ESP32 에러 시나리오");
                Console.WriteLine("9. AGV 이동 시나리오");
                Console.WriteLine("10. PLC 생산 카운트 시나리오");
                Console.WriteLine("------- 자동 모드 -------");
                Console.WriteLine("11. 자동 업데이트 시작 (1초 간격)");
                Console.WriteLine("12. 자동 업데이트 중지");
                Console.WriteLine("==========================");
                Console.WriteLine("0. 종료");
                Console.Write("\n선택: ");

                var input = Console.ReadLine();

                try
                {
                    switch (input)
                    {
                        case "1":
                            await service.WriteInitialValuesAsync();
                            break;

                        case "2":
                            await WriteSingleTag(service);
                            break;

                        case "3":
                            await WriteMultipleTags(service);
                            break;

                        case "4":
                            await WriteRandomSingleDevice(service);
                            break;

                        case "5":
                            await service.WriteRandomDataToAllAsync();
                            Console.WriteLine("모든 장비에 랜덤 데이터 쓰기 완료");
                            break;

                        case "6":
                            await service.SimulateEsp32StartAsync();
                            break;

                        case "7":
                            await service.SimulateEsp32StopAsync();
                            break;

                        case "8":
                            Console.Write("에러 코드 (기본: 101): ");
                            var errorStr = Console.ReadLine();
                            short errorCode = string.IsNullOrEmpty(errorStr) ? (short)101 : short.Parse(errorStr);
                            await service.SimulateEsp32ErrorAsync(errorCode);
                            break;

                        case "9":
                            Console.Write("목표 X 좌표: ");
                            var targetX = float.Parse(Console.ReadLine() ?? "50");
                            Console.Write("목표 Y 좌표: ");
                            var targetY = float.Parse(Console.ReadLine() ?? "30");
                            await service.SimulateAgvMoveAsync(targetX, targetY);
                            break;

                        case "10":
                            Console.Write("생산 개수 (기본: 10): ");
                            var countStr = Console.ReadLine();
                            int count = string.IsNullOrEmpty(countStr) ? 10 : int.Parse(countStr);
                            await service.SimulatePlcProductionAsync(count);
                            break;

                        case "11":
                            Console.Write("업데이트 간격 ms (기본: 1000): ");
                            var intervalStr = Console.ReadLine();
                            int interval = string.IsNullOrEmpty(intervalStr) ? 1000 : int.Parse(intervalStr);
                            service.StartAutoUpdate(interval);
                            Console.WriteLine("자동 업데이트 시작됨. 메뉴로 돌아가려면 Enter...");
                            Console.ReadLine();
                            break;

                        case "12":
                            service.StopAutoUpdate();
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

        /// <summary>
        /// 단일 태그 값 쓰기
        /// </summary>
        static async Task WriteSingleTag(FakeDataWriterService service)
        {
            Console.WriteLine("\n장비 목록: ESP32_01, PLC_01, AGV_01, ROBOT_01");
            Console.Write("장비 이름: ");
            var deviceName = Console.ReadLine() ?? "ESP32_01";

            Console.WriteLine("\n태그 예시:");
            Console.WriteLine("  ESP32_01: Connected, Running, Speed, PositionX, Temperature, ErrorCode");
            Console.WriteLine("  PLC_01: D100, D101, D102, M0, M1, Y0, Y1");
            Console.WriteLine("  AGV_01: BatteryLevel, CurrentX, CurrentY, Speed, Status");
            Console.WriteLine("  ROBOT_01: Joint1~6, GripperState");
            Console.Write("태그 이름: ");
            var tagName = Console.ReadLine() ?? "Speed";

            Console.Write("값: ");
            var valueStr = Console.ReadLine() ?? "100";

            // 타입 추론
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

        /// <summary>
        /// 여러 태그 값 쓰기
        /// </summary>
        static async Task WriteMultipleTags(FakeDataWriterService service)
        {
            Console.Write("장비 이름: ");
            var deviceName = Console.ReadLine() ?? "ESP32_01";

            var tags = new Dictionary<string, object>();
            Console.WriteLine("태그 입력 (빈 입력시 완료):");

            while (true)
            {
                Console.Write("  태그 이름: ");
                var tagName = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(tagName))
                    break;

                Console.Write("  값: ");
                var valueStr = Console.ReadLine() ?? "0";

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

                tags[tagName] = value;
            }

            if (tags.Count > 0)
            {
                var result = await service.WriteTagsAsync(deviceName, tags);
                Console.WriteLine($"결과: {(result.Success ? "성공" : "실패")} - {result.Message}");
            }
            else
            {
                Console.WriteLine("입력된 태그가 없습니다.");
            }
        }

        /// <summary>
        /// 단일 장비에 랜덤 데이터 쓰기
        /// </summary>
        static async Task WriteRandomSingleDevice(FakeDataWriterService service)
        {
            Console.WriteLine("장비 목록: ESP32_01, PLC_01, AGV_01, ROBOT_01");
            Console.Write("장비 이름: ");
            var deviceName = Console.ReadLine() ?? "ESP32_01";

            await service.WriteRandomDataAsync(deviceName);
            Console.WriteLine($"{deviceName}에 랜덤 데이터 쓰기 완료");
        }
    }
}
