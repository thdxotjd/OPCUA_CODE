using DeviceConnector.Events;
using DeviceConnector.Models;
using DeviceConnector.Services;

namespace DeviceConnector.Test;

/// <summary>
/// DeviceConnector v2.2 테스트 프로그램
/// ※ TargetA Coil(00007) Write 테스트 포함
/// </summary>
class Program
{
    private static OpcUaClientService? _client;
    private static readonly string DefaultDeviceId = "ESP32_01";

    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     DeviceConnector v2.2 - ESP32 ModbusTCP Test            ║");
        Console.WriteLine("║     TargetA: Coil 00007 (FC05 Write Single Coil)           ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // 연결 설정
        var connectionInfo = new OpcUaConnectionInfo
        {
            EndpointUrl = "opc.tcp://127.0.0.1:49320",
            AutoReconnect = true,
            ReconnectInterval = 5000,
            PublishingInterval = 100,
            SamplingInterval = 100
        };

        // 디바이스 태그 설정
        var deviceConfig = new DeviceTagConfig
        {
            DeviceId = DefaultDeviceId,
            ChannelName = "ModbusTCP",
            DeviceName = "ESP32_01",
            Tags = new DeviceTagNames
            {
                PosX = "POS_X",
                PosY = "POS_Y",
                PosTheta = "POS_T",
                TargetA = "TargetA",   // Coil 00007
                Control = "Control",
                State = "State"
            }
        };

        Console.WriteLine("[CONFIG] OPC UA Server: " + connectionInfo.EndpointUrl);
        Console.WriteLine("[CONFIG] Device: " + deviceConfig.DeviceId);
        Console.WriteLine("[CONFIG] Channel: " + deviceConfig.ChannelName);
        Console.WriteLine();
        Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ KEPServerEX 태그 설정 (v2.2)                            │");
        Console.WriteLine("├─────────────┬──────────┬───────────┬─────────────────────┤");
        Console.WriteLine("│ Tag Name    │ Address  │ Data Type │ Direction           │");
        Console.WriteLine("├─────────────┼──────────┼───────────┼─────────────────────┤");
        Console.WriteLine("│ POS_X       │ 40001    │ Float     │ Read                │");
        Console.WriteLine("│ POS_Y       │ 40003    │ Float     │ Read                │");
        Console.WriteLine("│ POS_T       │ 40005    │ Float     │ Read                │");
        Console.WriteLine("│ TargetA     │ 00007    │ Boolean   │ Write (Coil FC05)   │");
        Console.WriteLine("│ Control     │ 40100.20H│ String    │ Write               │");
        Console.WriteLine("│ State       │ 40200.20H│ String    │ Write               │");
        Console.WriteLine("└─────────────┴──────────┴───────────┴─────────────────────┘");
        Console.WriteLine();

        _client = new OpcUaClientService(connectionInfo);
        _client.AddDeviceConfig(deviceConfig);

        // 이벤트 핸들러 등록
        _client.ConnectionChanged += OnConnectionChanged;
        _client.DataChanged += OnDataChanged;
        _client.WriteCompleted += OnWriteCompleted;
        _client.ErrorOccurred += OnErrorOccurred;

        await RunMenuLoop();

        _client.Dispose();
        Console.WriteLine("\n프로그램을 종료합니다.");
    }

    static async Task RunMenuLoop()
    {
        while (true)
        {
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("  메뉴 선택");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  [1] 연결 (Connect)");
            Console.WriteLine("  [2] 연결 해제 (Disconnect)");
            Console.WriteLine("  [3] 데이터 읽기 (Read All)");
            Console.WriteLine("  [4] TargetA 쓰기 - Coil 00007 (Boolean)");
            Console.WriteLine("  [5] Control 쓰기 (String)");
            Console.WriteLine("  [6] State 쓰기 (String)");
            Console.WriteLine("  [7] 구독 시작 (Start Subscription)");
            Console.WriteLine("  [8] 구독 중지 (Stop Subscription)");
            Console.WriteLine("  [9] 연결 상태 확인");
            Console.WriteLine("  [0] 종료");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.Write("선택: ");

            var input = Console.ReadLine();

            try
            {
                switch (input)
                {
                    case "1":
                        await ConnectAsync();
                        break;
                    case "2":
                        await DisconnectAsync();
                        break;
                    case "3":
                        await ReadDataAsync();
                        break;
                    case "4":
                        await WriteTargetAAsync();
                        break;
                    case "5":
                        await WriteControlAsync();
                        break;
                    case "6":
                        await WriteStateAsync();
                        break;
                    case "7":
                        await StartSubscriptionAsync();
                        break;
                    case "8":
                        await StopSubscriptionAsync();
                        break;
                    case "9":
                        ShowConnectionStatus();
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
                Console.WriteLine($"[ERROR] {ex.Message}");
            }
        }
    }

    static async Task ConnectAsync()
    {
        Console.WriteLine("\n[INFO] OPC UA 서버에 연결 중...");
        var result = await _client!.ConnectAsync();
        Console.WriteLine(result ? "[SUCCESS] 연결 성공!" : "[FAILED] 연결 실패");
    }

    static async Task DisconnectAsync()
    {
        Console.WriteLine("\n[INFO] 연결 해제 중...");
        await _client!.DisconnectAsync();
        Console.WriteLine("[SUCCESS] 연결 해제 완료");
    }

    static async Task ReadDataAsync()
    {
        if (!_client!.IsConnected)
        {
            Console.WriteLine("[WARN] 먼저 연결하세요.");
            return;
        }

        Console.WriteLine("\n[INFO] 데이터 읽기 중...");
        var data = await _client.ReadDeviceDataAsync(DefaultDeviceId);

        if (data != null)
        {
            Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ ESP32 Data                                              │");
            Console.WriteLine("├─────────────────────────────────────────────────────────┤");
            Console.WriteLine($"│ DeviceId : {data.DeviceId,-44} │");
            Console.WriteLine($"│ PosX     : {data.PosX,-44:F4} │");
            Console.WriteLine($"│ PosY     : {data.PosY,-44:F4} │");
            Console.WriteLine($"│ PosTheta : {data.PosTheta,-44:F4} │");
            Console.WriteLine($"│ TargetA  : {data.TargetA,-44} │");
            Console.WriteLine($"│ Control  : {data.Control,-44} │");
            Console.WriteLine($"│ State    : {data.State,-44} │");
            Console.WriteLine($"│ Quality  : {(data.IsGoodQuality ? "Good" : "Bad"),-44} │");
            Console.WriteLine($"│ Time     : {data.Timestamp:yyyy-MM-dd HH:mm:ss.fff,-32} │");
            Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        }
        else
        {
            Console.WriteLine("[WARN] 데이터를 읽을 수 없습니다.");
        }
    }

    static async Task WriteTargetAAsync()
    {
        if (!_client!.IsConnected)
        {
            Console.WriteLine("[WARN] 먼저 연결하세요.");
            return;
        }

        Console.WriteLine("\n┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ TargetA Write (Coil 00007 - FC05 Write Single Coil)     │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        Console.Write("값 입력 (true/false 또는 1/0): ");
        var input = Console.ReadLine()?.Trim().ToLower();

        bool value;
        if (input == "true" || input == "1")
            value = true;
        else if (input == "false" || input == "0")
            value = false;
        else
        {
            Console.WriteLine("[ERROR] 잘못된 입력입니다. (true/false 또는 1/0)");
            return;
        }

        Console.WriteLine($"[INFO] TargetA = {value} 쓰기 중... (Coil 00007)");
        var result = await _client.WriteTargetAAsync(DefaultDeviceId, value);
        Console.WriteLine(result ? "[SUCCESS] 쓰기 성공!" : "[FAILED] 쓰기 실패 - KEPServerEX 태그 주소가 00007(Coil)인지 확인하세요.");
    }

    static async Task WriteControlAsync()
    {
        if (!_client!.IsConnected)
        {
            Console.WriteLine("[WARN] 먼저 연결하세요.");
            return;
        }

        Console.Write("\nControl 값 입력 (최대 20자): ");
        var value = Console.ReadLine() ?? string.Empty;
        
        if (value.Length > 20)
        {
            value = value[..20];
            Console.WriteLine($"[WARN] 20자로 잘림: {value}");
        }

        Console.WriteLine($"[INFO] Control = \"{value}\" 쓰기 중...");
        var result = await _client.WriteControlAsync(DefaultDeviceId, value);
        Console.WriteLine(result ? "[SUCCESS] 쓰기 성공!" : "[FAILED] 쓰기 실패");
    }

    static async Task WriteStateAsync()
    {
        if (!_client!.IsConnected)
        {
            Console.WriteLine("[WARN] 먼저 연결하세요.");
            return;
        }

        Console.Write("\nState 값 입력 (최대 20자): ");
        var value = Console.ReadLine() ?? string.Empty;

        if (value.Length > 20)
        {
            value = value[..20];
            Console.WriteLine($"[WARN] 20자로 잘림: {value}");
        }

        Console.WriteLine($"[INFO] State = \"{value}\" 쓰기 중...");
        var result = await _client.WriteStateAsync(DefaultDeviceId, value);
        Console.WriteLine(result ? "[SUCCESS] 쓰기 성공!" : "[FAILED] 쓰기 실패");
    }

    static async Task StartSubscriptionAsync()
    {
        if (!_client!.IsConnected)
        {
            Console.WriteLine("[WARN] 먼저 연결하세요.");
            return;
        }

        Console.WriteLine("\n[INFO] 구독 시작 중...");
        await _client.StartSubscriptionAsync(DefaultDeviceId);
        Console.WriteLine("[SUCCESS] 구독 시작! 데이터 변경 시 자동으로 출력됩니다.");
    }

    static async Task StopSubscriptionAsync()
    {
        Console.WriteLine("\n[INFO] 구독 중지 중...");
        await _client!.StopSubscriptionAsync(DefaultDeviceId);
        Console.WriteLine("[SUCCESS] 구독 중지 완료");
    }

    static void ShowConnectionStatus()
    {
        var status = _client!.Status;
        Console.WriteLine("\n┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Connection Status                                       │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        Console.WriteLine($"│ State      : {status.State,-42} │");
        Console.WriteLine($"│ Server     : {status.ServerUrl,-42} │");
        Console.WriteLine($"│ Connected  : {status.IsConnected,-42} │");
        Console.WriteLine($"│ Last Time  : {status.LastConnectedTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A",-30} │");
        Console.WriteLine($"│ Reconnects : {status.ReconnectAttempts,-42} │");
        Console.WriteLine($"│ Last Error : {(status.LastError ?? "None")[..Math.Min(status.LastError?.Length ?? 4, 42)],-42} │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
    }

    #region Event Handlers

    static void OnConnectionChanged(object? sender, ConnectionChangedEventArgs e)
    {
        Console.WriteLine($"\n[EVENT] Connection: {e.PreviousState} → {e.Status.State}");
    }

    static void OnDataChanged(object? sender, DataChangedEventArgs e)
    {
        Console.WriteLine($"\n[EVENT] Data Changed: {e.DeviceId}");
        Console.WriteLine($"        Pos({e.Data.PosX:F3}, {e.Data.PosY:F3}, {e.Data.PosTheta:F3}) TargetA={e.Data.TargetA}");
    }

    static void OnWriteCompleted(object? sender, WriteCompletedEventArgs e)
    {
        var status = e.Success ? "SUCCESS" : "FAILED";
        Console.WriteLine($"\n[EVENT] Write {status}: {e.DeviceId}.{e.TagName} = {e.Value}");
        if (!e.Success && e.ErrorMessage != null)
        {
            Console.WriteLine($"        Error: {e.ErrorMessage}");
        }
    }

    static void OnErrorOccurred(object? sender, ErrorOccurredEventArgs e)
    {
        Console.WriteLine($"\n[EVENT] Error: {e.Message}");
    }

    #endregion
}
