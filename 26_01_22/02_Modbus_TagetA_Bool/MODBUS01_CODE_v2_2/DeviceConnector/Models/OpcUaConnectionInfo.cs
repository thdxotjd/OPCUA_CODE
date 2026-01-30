namespace DeviceConnector.Models;

/// <summary>
/// OPC UA 연결 설정 정보
/// </summary>
public class OpcUaConnectionInfo
{
    /// <summary>OPC UA 서버 엔드포인트 URL</summary>
    /// <example>opc.tcp://127.0.0.1:49320</example>
    public string EndpointUrl { get; set; } = "opc.tcp://127.0.0.1:49320";

    /// <summary>애플리케이션 이름</summary>
    public string ApplicationName { get; set; } = "DeviceConnector";

    /// <summary>보안 정책 (None, Basic256Sha256 등)</summary>
    public string SecurityPolicy { get; set; } = "None";

    /// <summary>자동 재연결 여부</summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>재연결 간격 (ms)</summary>
    public int ReconnectInterval { get; set; } = 5000;

    /// <summary>연결 타임아웃 (ms)</summary>
    public int ConnectionTimeout { get; set; } = 10000;

    /// <summary>세션 타임아웃 (ms)</summary>
    public int SessionTimeout { get; set; } = 60000;

    /// <summary>Subscription 발행 간격 (ms)</summary>
    public int PublishingInterval { get; set; } = 100;

    /// <summary>MonitoredItem 샘플링 간격 (ms)</summary>
    public int SamplingInterval { get; set; } = 100;

    /// <summary>사용자 이름 (인증 필요 시)</summary>
    public string? Username { get; set; }

    /// <summary>비밀번호 (인증 필요 시)</summary>
    public string? Password { get; set; }
}

/// <summary>
/// 디바이스별 태그 설정
/// KEPServerEX NodeId 형식: ns=2;s=ChannelName.DeviceName.TagName
/// </summary>
public class DeviceTagConfig
{
    /// <summary>디바이스 식별자 (예: "ESP32_01")</summary>
    public string DeviceId { get; set; } = "ESP32_01";

    /// <summary>KEPServerEX 채널명 (예: "ModbusTCP")</summary>
    public string ChannelName { get; set; } = "ModbusTCP";

    /// <summary>KEPServerEX 디바이스명 (예: "ESP32_01")</summary>
    public string DeviceName { get; set; } = "ESP32_01";

    /// <summary>태그 이름 목록</summary>
    public DeviceTagNames Tags { get; set; } = new();

    /// <summary>
    /// NodeId 생성
    /// </summary>
    /// <param name="tagName">태그 이름</param>
    /// <returns>OPC UA NodeId 문자열</returns>
    public string GetNodeId(string tagName) =>
        $"ns=2;s={ChannelName}.{DeviceName}.{tagName}";
}

/// <summary>
/// ESP32 태그 이름 정의 (KEPServerEX 태그와 매핑)
/// 
/// ┌─────────────────────────────────────────────────────────────────────┐
/// │ v2.2 변경사항: TargetA 주소 변경                                    │
/// │ - 이전: 40007.0 (Holding Register Bit) - Write 실패 문제           │
/// │ - 변경: 00007 (Coil) - FC05 Write Single Coil 사용                 │
/// └─────────────────────────────────────────────────────────────────────┘
/// </summary>
public class DeviceTagNames
{
    #region Read 태그 (ESP32 → OPC UA) - Holding Register

    /// <summary>X 좌표 태그 - Address: 40001, Type: Float</summary>
    public string PosX { get; set; } = "POS_X";

    /// <summary>Y 좌표 태그 - Address: 40003, Type: Float</summary>
    public string PosY { get; set; } = "POS_Y";

    /// <summary>각도 태그 - Address: 40005, Type: Float</summary>
    public string PosTheta { get; set; } = "POS_T";

    #endregion

    #region Write 태그 (OPC UA → ESP32)

    /// <summary>
    /// 목표 A 플래그 태그 - Address: 00007 (Coil), Type: Boolean
    /// ※ v2.2: Coil 주소로 변경 (FC05 Write Single Coil)
    /// </summary>
    public string TargetA { get; set; } = "TargetA";

    /// <summary>제어 명령 태그 - Address: 40100.20H, Type: String</summary>
    public string Control { get; set; } = "Control";

    /// <summary>상태 정보 태그 - Address: 40200.20H, Type: String</summary>
    public string State { get; set; } = "State";

    #endregion
}

/// <summary>
/// ESP32 태그 이름 상수 (gRPC 개발자용)
/// </summary>
public static class ESP32Tags
{
    // Read 태그
    public const string POS_X = "POS_X";
    public const string POS_Y = "POS_Y";
    public const string POS_T = "POS_T";

    // Write 태그
    public const string TARGET_A = "TargetA";    // Coil 00007
    public const string CONTROL = "Control";     // String
    public const string STATE = "State";         // String
}

/// <summary>
/// KEPServerEX Modbus 주소 상수 (참고용)
/// </summary>
public static class ModbusAddresses
{
    // Holding Register (40xxx) - Read
    public const string POS_X = "40001";      // Float (2 registers)
    public const string POS_Y = "40003";      // Float (2 registers)
    public const string POS_T = "40005";      // Float (2 registers)

    // Coil (0xxxx) - Write Boolean
    /// <summary>
    /// TargetA - Coil 주소 (v2.2 변경)
    /// FC05: Write Single Coil
    /// </summary>
    public const string TARGET_A = "00007";   // Boolean (Coil)

    // Holding Register (40xxx) - Write String
    public const string CONTROL = "40100.20H"; // String 20자
    public const string STATE = "40200.20H";   // String 20자
}
