using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using DeviceConnector.Services;

namespace DeviceConnector.Test;

/// <summary>
/// DeviceConnector 테스트 프로그램
/// KEPServerEX를 통한 ESP32 ModbusTCP 통신 테스트
/// </summary>
class Program
{
    // ========== 설정 (본인 환경에 맞게 수정) ==========
    private const string OPC_UA_ENDPOINT = "opc.tcp://192.168.0.19:49320";
    private const string CHANNEL_NAME = "ModbusTCP";
    private const string DEVICE_NAME = "ESP32_01";
    private const string DEVICE_ID = "ESP32_01";

    private static IOpcUaClientService? _client;
    private static bool _isRunning = true;

    static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
        Console.WriteLine("║     DeviceConnector (MODBUS_CODE) Test Program        ║");
        Console.WriteLine("║     ESP32 ModbusTCP - KEPServerEX OPC UA Client       ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // 연결 설정
        var connectionInfo = new OpcUaConnectionInfo
        {
            EndpointUrl = OPC_UA_ENDPOINT,
            PublishingIntervalMs = 100,
            AutoReconnect = true
        };

        // 디바이스 설정
        var deviceConfig = new DeviceTagConfig
        {
            DeviceId = DEVICE_ID,
            ChannelName = CHANNEL_NAME,
            DeviceName = DEVICE_NAME
        };

        Console.WriteLine($"[CONFIG] OPC UA Endpoint: {OPC_UA_ENDPOINT}");
        Console.WriteLine($"[CONFIG] Channel: {CHANNEL_NAME}, Device: {DEVICE_NAME}");
        Console.WriteLine();

        // 클라이언트 생성
        _client = new OpcUaClientService(connectionInfo);
        _client.AddDeviceConfig(deviceConfig);

        // 이벤트 핸들러 등록
        _client.ConnectionChanged += OnConnectionChanged;
        _client.DataChanged += OnDataChanged;
        _client.WriteCompleted += OnWriteCompleted;

        try
        {
            // 연결
            Console.WriteLine("[INFO] Connecting to OPC UA server...");
            await _client.ConnectAsync();
            Console.WriteLine("[INFO] Connected successfully!\n");

            // 메뉴 표시
            await ShowMenuAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Connection failed: {ex.Message}");
        }
        finally
        {
            _client?.Dispose();
        }

        Console.WriteLine("\n[INFO] Program terminated.");
    }

    private static async Task ShowMenuAsync()
    {
        while (_isRunning && _client != null)
        {
            Console.WriteLine("\n╔═══════════════════════════════════════╗");
            Console.WriteLine("║           Test Menu                   ║");
            Console.WriteLine("╠═══════════════════════════════════════╣");
            Console.WriteLine("║  1. Read All Data                     ║");
            Console.WriteLine("║  2. Read Position Only (POS_X/Y/T)    ║");
            Console.WriteLine("║  3. Read State                        ║");
            Console.WriteLine("║  4. Write TargetA (Boolean)           ║");
            Console.WriteLine("║  5. Write Control (String)            ║");
            Console.WriteLine("║  6. Write State (String)              ║");
            Console.WriteLine("║  7. Write Multiple Tags               ║");
            Console.WriteLine("║  8. Start Subscription (Real-time)    ║");
            Console.WriteLine("║  9. Stop Subscription                 ║");
            Console.WriteLine("║  0. Exit                              ║");
            Console.WriteLine("╚═══════════════════════════════════════╝");
            Console.Write("\nSelect option: ");

            var input = Console.ReadLine();

            try
            {
                switch (input)
                {
                    case "1":
                        await TestReadAllDataAsync();
                        break;
                    case "2":
                        await TestReadPositionAsync();
                        break;
                    case "3":
                        await TestReadStateAsync();
                        break;
                    case "4":
                        await TestWriteTargetAAsync();
                        break;
                    case "5":
                        await TestWriteControlAsync();
                        break;
                    case "6":
                        await TestWriteStateAsync();
                        break;
                    case "7":
                        await TestWriteMultipleAsync();
                        break;
                    case "8":
                        await TestStartSubscriptionAsync();
                        break;
                    case "9":
                        await TestStopSubscriptionAsync();
                        break;
                    case "0":
                        _isRunning = false;
                        break;
                    default:
                        Console.WriteLine("[WARN] Invalid option");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }
        }
    }

    #region Test Methods

    private static async Task TestReadAllDataAsync()
    {
        Console.WriteLine("\n[TEST] Reading all device data...");

        var data = await _client!.ReadDeviceDataAsync(DEVICE_ID);

        if (data != null)
        {
            Console.WriteLine("┌─────────────────────────────────────────────────┐");
            Console.WriteLine("│              Device Data                        │");
            Console.WriteLine("├─────────────────────────────────────────────────┤");
            Console.WriteLine($"│  Device ID  : {data.DeviceId,-30} │");
            Console.WriteLine($"│  Channel    : {data.ChannelName,-30} │");
            Console.WriteLine($"│  Device     : {data.DeviceName,-30} │");
            Console.WriteLine("├─────────────────────────────────────────────────┤");
            Console.WriteLine($"│  POS_X      : {data.PosX,10:F4} m                  │");
            Console.WriteLine($"│  POS_Y      : {data.PosY,10:F4} m                  │");
            Console.WriteLine($"│  POS_T      : {data.PosT,10:F2} °                  │");
            Console.WriteLine("├─────────────────────────────────────────────────┤");
            Console.WriteLine($"│  TargetA    : {data.TargetA,-30} │");
            Console.WriteLine($"│  Control    : {data.Control,-30} │");
            Console.WriteLine($"│  State      : {data.State,-30} │");
            Console.WriteLine("├─────────────────────────────────────────────────┤");
            Console.WriteLine($"│  Timestamp  : {data.Timestamp:yyyy-MM-dd HH:mm:ss.fff}       │");
            Console.WriteLine($"│  Quality    : {(data.IsGoodQuality ? "Good" : "Bad"),-30} │");
            Console.WriteLine("└─────────────────────────────────────────────────┘");
        }
        else
        {
            Console.WriteLine("[WARN] Failed to read device data");
        }
    }

    private static async Task TestReadPositionAsync()
    {
        Console.WriteLine("\n[TEST] Reading position data...");

        var position = await _client!.ReadPositionAsync(DEVICE_ID);

        if (position.HasValue)
        {
            Console.WriteLine("┌────────────────────────────────┐");
            Console.WriteLine("│        Position Data           │");
            Console.WriteLine("├────────────────────────────────┤");
            Console.WriteLine($"│  POS_X : {position.Value.PosX,10:F4} m        │");
            Console.WriteLine($"│  POS_Y : {position.Value.PosY,10:F4} m        │");
            Console.WriteLine($"│  POS_T : {position.Value.PosT,10:F2} °        │");
            Console.WriteLine("└────────────────────────────────┘");
        }
        else
        {
            Console.WriteLine("[WARN] Failed to read position");
        }
    }

    private static async Task TestReadStateAsync()
    {
        Console.WriteLine("\n[TEST] Reading State...");

        var state = await _client!.ReadStateAsync(DEVICE_ID);

        if (state != null)
        {
            Console.WriteLine($"[RESULT] State = \"{state}\"");
        }
        else
        {
            Console.WriteLine("[WARN] Failed to read state");
        }
    }

    private static async Task TestWriteTargetAAsync()
    {
        Console.WriteLine("\n[TEST] Write TargetA (Boolean)");
        Console.Write("Enter value (true/false): ");
        var input = Console.ReadLine();

        if (bool.TryParse(input, out bool value))
        {
            var result = await _client!.WriteTargetAAsync(DEVICE_ID, value);
            Console.WriteLine($"[RESULT] Write TargetA = {value} -> {(result ? "SUCCESS" : "FAILED")}");
        }
        else
        {
            Console.WriteLine("[WARN] Invalid boolean value");
        }
    }

    private static async Task TestWriteControlAsync()
    {
        Console.WriteLine("\n[TEST] Write Control (String)");
        Console.WriteLine("Suggested values: MOVE, STOP, RESET, HOME");
        Console.Write("Enter value: ");
        var value = Console.ReadLine() ?? string.Empty;

        var result = await _client!.WriteControlAsync(DEVICE_ID, value);
        Console.WriteLine($"[RESULT] Write Control = \"{value}\" -> {(result ? "SUCCESS" : "FAILED")}");
    }

    private static async Task TestWriteStateAsync()
    {
        Console.WriteLine("\n[TEST] Write State (String)");
        Console.WriteLine("Suggested values: IDLE, MOVING, ARRIVED, ERROR");
        Console.Write("Enter value: ");
        var value = Console.ReadLine() ?? string.Empty;

        var result = await _client!.WriteStateAsync(DEVICE_ID, value);
        Console.WriteLine($"[RESULT] Write State = \"{value}\" -> {(result ? "SUCCESS" : "FAILED")}");

        // 쓴 후 다시 읽어서 확인
        await Task.Delay(100);
        var readBack = await _client!.ReadStateAsync(DEVICE_ID);
        Console.WriteLine($"[VERIFY] Read back State = \"{readBack}\"");
    }

    private static async Task TestWriteMultipleAsync()
    {
        Console.WriteLine("\n[TEST] Write Multiple Tags");
        
        Console.Write("Enter TargetA (true/false): ");
        var targetAInput = Console.ReadLine();
        bool.TryParse(targetAInput, out bool targetA);

        Console.Write("Enter Control: ");
        var control = Console.ReadLine() ?? string.Empty;

        Console.Write("Enter State: ");
        var state = Console.ReadLine() ?? string.Empty;

        var tagValues = new Dictionary<string, object>
        {
            { ESP32Tags.TARGET_A, targetA },
            { ESP32Tags.CONTROL, control },
            { ESP32Tags.STATE, state }
        };

        Console.WriteLine("\n[INFO] Writing multiple tags...");
        var results = await _client!.WriteMultipleAsync(DEVICE_ID, tagValues);

        Console.WriteLine("┌────────────────────────────────────────┐");
        Console.WriteLine("│          Write Results                 │");
        Console.WriteLine("├────────────────────────────────────────┤");
        foreach (var result in results)
        {
            var status = result.Value ? "✓ SUCCESS" : "✗ FAILED";
            Console.WriteLine($"│  {result.Key,-12} : {status,-20} │");
        }
        Console.WriteLine("└────────────────────────────────────────┘");
    }

    private static async Task TestStartSubscriptionAsync()
    {
        Console.WriteLine("\n[TEST] Starting subscription (real-time data)...");
        Console.WriteLine("[INFO] Press any key to return to menu while subscription is active.\n");

        await _client!.StartSubscriptionAsync(DEVICE_ID);
        Console.WriteLine("[INFO] Subscription started. Waiting for data...\n");

        // 키 입력 대기 (비동기)
        await Task.Run(() => Console.ReadKey(true));
    }

    private static async Task TestStopSubscriptionAsync()
    {
        Console.WriteLine("\n[TEST] Stopping subscription...");
        await _client!.StopSubscriptionAsync(DEVICE_ID);
        Console.WriteLine("[INFO] Subscription stopped.");
    }

    #endregion

    #region Event Handlers

    private static void OnConnectionChanged(object? sender, Events.ConnectionChangedEventArgs e)
    {
        if (e.IsConnected)
        {
            Console.WriteLine($"\n[EVENT] Connected to {e.EndpointUrl}");
        }
        else
        {
            Console.WriteLine($"\n[EVENT] Disconnected: {e.ErrorMessage}");
        }
    }

    private static void OnDataChanged(object? sender, Events.DataChangedEventArgs e)
    {
        Console.WriteLine($"\n[SUBSCRIPTION] Data Changed - Device: {e.DeviceId}");
        Console.WriteLine($"  Changed Tags: {string.Join(", ", e.ChangedTags)}");
        Console.WriteLine($"  Position: ({e.Data.PosX:F4}, {e.Data.PosY:F4}, {e.Data.PosT:F2}°)");
        Console.WriteLine($"  TargetA: {e.Data.TargetA}, Control: \"{e.Data.Control}\", State: \"{e.Data.State}\"");
    }

    private static void OnWriteCompleted(object? sender, Events.WriteCompletedEventArgs e)
    {
        var status = e.Success ? "SUCCESS" : $"FAILED ({e.ErrorMessage})";
        Console.WriteLine($"[EVENT] Write Completed - {e.TagName} = {e.Value} -> {status}");
    }

    #endregion
}
