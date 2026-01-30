namespace DeviceConnector.Models;

/// <summary>
/// ESP32 디바이스 데이터 모델
/// KEPServerEX ModbusTCP 태그 매핑
/// 
/// ┌─────────────────────────────────────────────────────────────┐
/// │ 태그 구성 (v2.2 - TargetA Coil 주소 변경)                   │
/// ├─────────────┬──────────┬───────────┬───────────────────────┤
/// │ Tag Name    │ Address  │ Data Type │ 방향                  │
/// ├─────────────┼──────────┼───────────┼───────────────────────┤
/// │ POS_X       │ 40001    │ Float     │ Read (ESP32 → OPC)    │
/// │ POS_Y       │ 40003    │ Float     │ Read (ESP32 → OPC)    │
/// │ POS_T       │ 40005    │ Float     │ Read (ESP32 → OPC)    │
/// │ TargetA     │ 00007    │ Boolean   │ Write (OPC → ESP32)   │  ← Coil 주소
/// │ Control     │ 40100.20H│ String    │ Write (OPC → ESP32)   │
/// │ State       │ 40200.20H│ String    │ Write (OPC → ESP32)   │
/// └─────────────┴──────────┴───────────┴───────────────────────┘
/// </summary>
public class ESP32Data
{
    #region 식별 정보

    /// <summary>디바이스 ID (예: "ESP32_01")</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>KEPServerEX 채널명 (예: "ModbusTCP")</summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>KEPServerEX 디바이스명 (예: "ESP32_01")</summary>
    public string DeviceName { get; set; } = string.Empty;

    #endregion

    #region 위치 데이터 (Read Only - ESP32에서 전송)

    /// <summary>X 좌표 (m) - Modbus 40001-40002 (Float)</summary>
    public float PosX { get; set; }

    /// <summary>Y 좌표 (m) - Modbus 40003-40004 (Float)</summary>
    public float PosY { get; set; }

    /// <summary>각도 Theta (rad) - Modbus 40005-40006 (Float)</summary>
    public float PosTheta { get; set; }

    #endregion

    #region 제어 데이터 (Write - OPC UA에서 ESP32로 전송)

    /// <summary>
    /// 목표 A 플래그 - Modbus Coil 00007 (Boolean)
    /// ※ v2.2 변경: Holding Register 비트(40007.0) → Coil(00007)
    /// Function Code: FC05 (Write Single Coil)
    /// </summary>
    public bool TargetA { get; set; }

    /// <summary>제어 명령 - Modbus 40100.20H (String, 20자)</summary>
    public string Control { get; set; } = string.Empty;

    /// <summary>상태 정보 - Modbus 40200.20H (String, 20자)</summary>
    public string State { get; set; } = string.Empty;

    #endregion

    #region 메타 데이터

    /// <summary>데이터 갱신 시간 (UTC)</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>OPC UA Quality 상태 (Good=true)</summary>
    public bool IsGoodQuality { get; set; } = true;

    #endregion

    #region 메서드

    /// <summary>객체 복제</summary>
    public ESP32Data Clone() => new()
    {
        DeviceId = DeviceId,
        ChannelName = ChannelName,
        DeviceName = DeviceName,
        PosX = PosX,
        PosY = PosY,
        PosTheta = PosTheta,
        TargetA = TargetA,
        Control = Control,
        State = State,
        Timestamp = Timestamp,
        IsGoodQuality = IsGoodQuality
    };

    public override string ToString() =>
        $"[{DeviceId}] Pos({PosX:F3}, {PosY:F3}, {PosTheta:F3}) TargetA={TargetA} Control={Control} State={State}";

    #endregion
}
