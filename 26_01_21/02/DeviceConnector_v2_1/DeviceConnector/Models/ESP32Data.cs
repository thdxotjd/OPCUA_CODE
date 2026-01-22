namespace DeviceConnector.Models;

/// <summary>
/// ESP32 디바이스 데이터 모델
/// KEPServerEX Tag Map (ModbusTCP.ESP32_01):
/// - POS_X:   40001 (Float) - Read Only
/// - POS_Y:   40003 (Float) - Read Only
/// - POS_T:   40005 (Float) - Read Only
/// - TargetA: 40007.0 (Boolean) - Write
/// - Control: 40100.20H (String) - Write
/// - State:   40200.20H (String) - Write
/// </summary>
public class ESP32Data
{
    /// <summary>
    /// 디바이스 ID (예: "ESP32_01")
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// 채널 이름 (예: "ModbusTCP")
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// 디바이스 이름 (예: "ESP32_01")
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    #region Read Only Tags (ESP32 → OPC UA)

    /// <summary>
    /// X 좌표 (단위: m)
    /// Modbus 주소: 40001-40002 (Float)
    /// </summary>
    public float PosX { get; set; }

    /// <summary>
    /// Y 좌표 (단위: m)
    /// Modbus 주소: 40003-40004 (Float)
    /// </summary>
    public float PosY { get; set; }

    /// <summary>
    /// 방향각 (단위: degree)
    /// Modbus 주소: 40005-40006 (Float)
    /// </summary>
    public float PosT { get; set; }

    #endregion

    #region Write Tags (OPC UA → ESP32)

    /// <summary>
    /// 목표 도달 플래그
    /// Modbus 주소: 40007.0 (Boolean)
    /// </summary>
    public bool TargetA { get; set; }

    /// <summary>
    /// 제어 명령 문자열
    /// Modbus 주소: 40100.20H (String, 20 bytes)
    /// 예: "MOVE", "STOP", "RESET"
    /// </summary>
    public string Control { get; set; } = string.Empty;

    /// <summary>
    /// 상태 문자열 (Read/Write)
    /// Modbus 주소: 40200.20H (String, 20 bytes)
    /// 예: "IDLE", "MOVING", "ARRIVED", "ERROR"
    /// ESP32에서 Write, OPC UA에서도 Read/Write 가능
    /// </summary>
    public string State { get; set; } = string.Empty;

    #endregion

    /// <summary>
    /// 데이터 수집 시간 (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 데이터 품질
    /// </summary>
    public bool IsGoodQuality { get; set; } = true;

    public override string ToString()
    {
        return $"[{DeviceId}] Pos({PosX:F3}, {PosY:F3}, {PosT:F1}°) TargetA:{TargetA} Ctrl:'{Control}' State:'{State}'";
    }
}

/// <summary>
/// ESP32 태그 이름 상수
/// </summary>
public static class ESP32Tags
{
    // Read Only Tags
    public const string POS_X = "POS_X";
    public const string POS_Y = "POS_Y";
    public const string POS_T = "POS_T";

    // Write Tags
    public const string TARGET_A = "TargetA";
    public const string CONTROL = "Control";
    public const string STATE = "State";

    /// <summary>
    /// 모든 태그 이름 배열
    /// </summary>
    public static readonly string[] AllTags = { POS_X, POS_Y, POS_T, TARGET_A, CONTROL, STATE };

    /// <summary>
    /// Read Only 태그 배열 (ESP32 → OPC UA)
    /// </summary>
    public static readonly string[] ReadOnlyTags = { POS_X, POS_Y, POS_T };

    /// <summary>
    /// Write Only 태그 배열 (OPC UA → ESP32)
    /// </summary>
    public static readonly string[] WriteOnlyTags = { TARGET_A, CONTROL };

    /// <summary>
    /// Read/Write 가능 태그 배열
    /// </summary>
    public static readonly string[] ReadWriteTags = { STATE };

    /// <summary>
    /// Write 가능한 모든 태그 배열
    /// </summary>
    public static readonly string[] WritableTags = { TARGET_A, CONTROL, STATE };
}
