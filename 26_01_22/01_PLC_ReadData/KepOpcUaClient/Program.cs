using Microsoft.Extensions.Configuration;
using KepOpcUaClient.Models;
using KepOpcUaClient.OpcUaClient;

namespace KepOpcUaClient
{
    /// <summary>
    /// KEPServerEX OPC UA 클라이언트 테스트 프로그램
    /// 사용 기한: 2026년 2월 28일까지
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("  KEPServerEX OPC UA Client - Read/Write Test");
            Console.WriteLine("==============================================");
            Console.WriteLine();

            // 설정 로드
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var settings = config.Get<AppSettings>() ?? new AppSettings();

            Console.WriteLine($"[설정 정보]");
            Console.WriteLine($"  서버 URL: {settings.KepServer.EndpointUrl}");
            Console.WriteLine($"  채널명: {settings.Device.ChannelName}");
            Console.WriteLine($"  디바이스명: {settings.Device.DeviceName}");
            Console.WriteLine($"  태그 수: {settings.Device.Tags.Count}개");
            Console.WriteLine();

            // OPC UA 클라이언트 생성
            using var client = new OpcUaClient.KepOpcUaClient(settings.KepServer, settings.Device);

            // 데이터 변경 이벤트 핸들러
            client.DataChanged += (sender, e) =>
            {
                Console.WriteLine($"[변경] {e}");
            };

            try
            {
                // 1. 연결
                Console.WriteLine("[1] KEPServerEX 연결 시도...");
                var connected = await client.ConnectAsync();
                
                if (!connected)
                {
                    Console.WriteLine("[ERROR] 연결 실패. 프로그램을 종료합니다.");
                    return;
                }

                Console.WriteLine();

                // 메뉴 루프
                while (true)
                {
                    PrintMenu();
                    var key = Console.ReadKey(true).Key;
                    Console.WriteLine();

                    switch (key)
                    {
                        case ConsoleKey.D1:
                        case ConsoleKey.NumPad1:
                            await ReadAllTags(client);
                            break;

                        case ConsoleKey.D2:
                        case ConsoleKey.NumPad2:
                            await ReadSingleTag(client, settings.Device);
                            break;

                        case ConsoleKey.D3:
                        case ConsoleKey.NumPad3:
                            await WriteTag(client, settings.Device);
                            break;

                        case ConsoleKey.D4:
                        case ConsoleKey.NumPad4:
                            await SubscribeDemo(client);
                            break;

                        case ConsoleKey.D5:
                        case ConsoleKey.NumPad5:
                            await BrowseServer(client);
                            break;

                        case ConsoleKey.D6:
                        case ConsoleKey.NumPad6:
                            await WriteMultipleTags(client, settings.Device);
                            break;

                        case ConsoleKey.Q:
                            Console.WriteLine("프로그램을 종료합니다...");
                            await client.DisconnectAsync();
                            return;

                        default:
                            Console.WriteLine("잘못된 선택입니다.");
                            break;
                    }

                    Console.WriteLine();
                    Console.WriteLine("계속하려면 아무 키나 누르세요...");
                    Console.ReadKey(true);
                    Console.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 예외 발생: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void PrintMenu()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("  메뉴 선택");
            Console.WriteLine("==============================================");
            Console.WriteLine("  [1] 전체 태그 읽기");
            Console.WriteLine("  [2] 단일 태그 읽기");
            Console.WriteLine("  [3] 단일 태그 쓰기");
            Console.WriteLine("  [4] 실시간 구독 (5초)");
            Console.WriteLine("  [5] 서버 탐색 (Browse)");
            Console.WriteLine("  [6] 다중 태그 쓰기");
            Console.WriteLine("  [Q] 종료");
            Console.WriteLine("==============================================");
            Console.Write("선택: ");
        }

        /// <summary>
        /// 전체 태그 읽기
        /// </summary>
        static async Task ReadAllTags(OpcUaClient.KepOpcUaClient client)
        {
            Console.WriteLine("\n[전체 태그 읽기]");
            Console.WriteLine("----------------------------------------------");

            var deviceData = await client.ReadAllTagsAsync();

            Console.WriteLine($"채널명: {deviceData.ChannelName}");
            Console.WriteLine($"디바이스명: {deviceData.DeviceName}");
            Console.WriteLine($"연결 상태: {deviceData.IsConnected}");
            Console.WriteLine($"타임스탬프: {deviceData.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            Console.WriteLine();
            Console.WriteLine("[태그 값]");

            foreach (var tag in deviceData.Tags.OrderBy(t => t.Key))
            {
                Console.WriteLine($"  {tag.Key,-15} = {tag.Value}");
            }
        }

        /// <summary>
        /// 단일 태그 읽기
        /// </summary>
        static async Task ReadSingleTag(OpcUaClient.KepOpcUaClient client, DeviceConfig deviceConfig)
        {
            Console.WriteLine("\n[단일 태그 읽기]");
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine("사용 가능한 태그:");
            
            for (int i = 0; i < deviceConfig.Tags.Count; i++)
            {
                Console.WriteLine($"  [{i}] {deviceConfig.Tags[i].Name}");
            }

            Console.Write("태그 번호 선택: ");
            var input = Console.ReadLine();

            if (int.TryParse(input, out int index) && index >= 0 && index < deviceConfig.Tags.Count)
            {
                var tag = deviceConfig.Tags[index];
                var value = await client.ReadTagAsync(tag.NodeId);
                Console.WriteLine($"\n결과: {tag.Name} = {value}");
            }
            else
            {
                Console.WriteLine("잘못된 선택입니다.");
            }
        }

        /// <summary>
        /// 단일 태그 쓰기
        /// </summary>
        static async Task WriteTag(OpcUaClient.KepOpcUaClient client, DeviceConfig deviceConfig)
        {
            Console.WriteLine("\n[단일 태그 쓰기]");
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine("쓰기 가능한 태그:");

            var writableTags = deviceConfig.Tags
                .Where(t => t.Name.StartsWith("D") || t.Name.StartsWith("M") || t.Name.StartsWith("Y"))
                .ToList();

            for (int i = 0; i < writableTags.Count; i++)
            {
                Console.WriteLine($"  [{i}] {writableTags[i].Name} ({writableTags[i].DataType})");
            }

            Console.Write("태그 번호 선택: ");
            var indexInput = Console.ReadLine();

            if (int.TryParse(indexInput, out int index) && index >= 0 && index < writableTags.Count)
            {
                var tag = writableTags[index];
                Console.Write($"{tag.Name}에 쓸 값 입력: ");
                var valueInput = Console.ReadLine();

                object value;
                if (tag.DataType == "Boolean")
                {
                    value = valueInput?.ToLower() == "true" || valueInput == "1";
                }
                else
                {
                    value = short.Parse(valueInput ?? "0");
                }

                var result = await client.WriteTagAsync(tag.NodeId, value);
                Console.WriteLine(result ? "쓰기 성공!" : "쓰기 실패!");
            }
            else
            {
                Console.WriteLine("잘못된 선택입니다.");
            }
        }

        /// <summary>
        /// 다중 태그 쓰기
        /// </summary>
        static async Task WriteMultipleTags(OpcUaClient.KepOpcUaClient client, DeviceConfig deviceConfig)
        {
            Console.WriteLine("\n[다중 태그 쓰기]");
            Console.WriteLine("----------------------------------------------");

            // D100, D101, D102 에 값 쓰기 예시
            var tagsToWrite = new Dictionary<string, object>();

            Console.Write("D100 값 입력: ");
            var d100 = Console.ReadLine();
            if (short.TryParse(d100, out short d100Val))
            {
                var tag = deviceConfig.Tags.FirstOrDefault(t => t.Name == "D100");
                if (tag != null) tagsToWrite[tag.NodeId] = d100Val;
            }

            Console.Write("D101 값 입력: ");
            var d101 = Console.ReadLine();
            if (short.TryParse(d101, out short d101Val))
            {
                var tag = deviceConfig.Tags.FirstOrDefault(t => t.Name == "D101");
                if (tag != null) tagsToWrite[tag.NodeId] = d101Val;
            }

            Console.Write("D102 값 입력: ");
            var d102 = Console.ReadLine();
            if (short.TryParse(d102, out short d102Val))
            {
                var tag = deviceConfig.Tags.FirstOrDefault(t => t.Name == "D102");
                if (tag != null) tagsToWrite[tag.NodeId] = d102Val;
            }

            if (tagsToWrite.Count > 0)
            {
                var results = await client.WriteTagsAsync(tagsToWrite);
                Console.WriteLine("\n[쓰기 결과]");
                foreach (var kvp in results)
                {
                    Console.WriteLine($"  {kvp.Key}: {(kvp.Value ? "성공" : "실패")}");
                }
            }
        }

        /// <summary>
        /// 실시간 구독 데모
        /// </summary>
        static async Task SubscribeDemo(OpcUaClient.KepOpcUaClient client)
        {
            Console.WriteLine("\n[실시간 구독 시작 (5초간)]");
            Console.WriteLine("----------------------------------------------");

            client.Subscribe(100);

            Console.WriteLine("데이터 변경 대기 중... (PLC에서 값을 변경해 보세요)");
            await Task.Delay(5000);

            client.Unsubscribe();
            Console.WriteLine("구독 종료");
        }

        /// <summary>
        /// 서버 탐색
        /// </summary>
        static async Task BrowseServer(OpcUaClient.KepOpcUaClient client)
        {
            Console.WriteLine("\n[서버 탐색 (Objects 폴더)]");
            Console.WriteLine("----------------------------------------------");

            var references = await client.BrowseAsync("ns=0;i=85"); // Objects 폴더

            foreach (var reference in references)
            {
                Console.WriteLine($"  {reference.BrowseName} ({reference.NodeClass})");
                Console.WriteLine($"    NodeId: {reference.NodeId}");
            }
        }
    }
}
