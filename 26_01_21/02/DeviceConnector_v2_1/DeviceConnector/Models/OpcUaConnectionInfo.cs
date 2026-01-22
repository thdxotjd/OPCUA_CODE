namespace DeviceConnector.Models;

/// <summary>
/// OPC UA 연결 정보
/// </summary>
public class OpcUaConnectionInfo
{
    /// <summary>
    /// OPC UA 서버 엔드포인트 URL
    /// 예: "opc.tcp://192.168.0.19:49320"
    /// </summary>
    public string EndpointUrl { get; set; } = "opc.tcp://localhost:49320";

    /// <summary>
    /// 애플리케이션 이름
    /// </summary>
    public string ApplicationName { get; set; } = "DeviceConnector";

    /// <summary>
    /// 세션 타임아웃 (ms)
    /// </summary>
    public uint SessionTimeout { get; set; } = 60000;

    /// <summary>
    /// 구독 발행 간격 (ms)
    /// </summary>
    public int PublishingIntervalMs { get; set; } = 100;

    /// <summary>
    /// 자동 재연결 활성화
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// 재연결 간격 (ms)
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 5000;
}

/// <summary>
/// 디바이스 태그 설정
/// DeviceName, ChannelName, Tags 구조 사용 (2026-02-28까지 유지)
/// </summary>
public class DeviceTagConfig
{
    /// <summary>
    /// 디바이스 식별자
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// KEPServerEX 채널 이름
    /// 예: "ModbusTCP"
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// KEPServerEX 디바이스 이름
    /// 예: "ESP32_01"
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// OPC UA NodeId 생성
    /// 형식: ns=2;s=ChannelName.DeviceName.TagName
    /// </summary>
    public string GetNodeId(string tagName)
    {
        return $"ns=2;s={ChannelName}.{DeviceName}.{tagName}";
    }

    /// <summary>
    /// 모든 태그의 NodeId 딕셔너리 반환
    /// </summary>
    public Dictionary<string, string> GetAllNodeIds()
    {
        return new Dictionary<string, string>
        {
            { ESP32Tags.POS_X, GetNodeId(ESP32Tags.POS_X) },
            { ESP32Tags.POS_Y, GetNodeId(ESP32Tags.POS_Y) },
            { ESP32Tags.POS_T, GetNodeId(ESP32Tags.POS_T) },
            { ESP32Tags.TARGET_A, GetNodeId(ESP32Tags.TARGET_A) },
            { ESP32Tags.CONTROL, GetNodeId(ESP32Tags.CONTROL) },
            { ESP32Tags.STATE, GetNodeId(ESP32Tags.STATE) }
        };
    }
}

/// <summary>
/// 연결 상태 정보
/// </summary>
public class ConnectionStatus
{
    public bool IsConnected { get; set; }
    public string? EndpointUrl { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? DisconnectedAt { get; set; }
    public string? LastError { get; set; }
    public int ReconnectAttempts { get; set; }
}
