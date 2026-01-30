namespace DeviceConnector.Models;

/// <summary>
/// OPC UA 연결 상태 정보
/// </summary>
public class ConnectionStatus
{
    /// <summary>연결 상태</summary>
    public ConnectionState State { get; set; } = ConnectionState.Disconnected;

    /// <summary>서버 URL</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>마지막 연결 성공 시간</summary>
    public DateTime? LastConnectedTime { get; set; }

    /// <summary>재연결 시도 횟수</summary>
    public int ReconnectAttempts { get; set; }

    /// <summary>마지막 에러 메시지</summary>
    public string? LastError { get; set; }

    /// <summary>연결 여부</summary>
    public bool IsConnected => State == ConnectionState.Connected;
}

/// <summary>
/// 연결 상태 열거형
/// </summary>
public enum ConnectionState
{
    /// <summary>연결 끊김</summary>
    Disconnected,

    /// <summary>연결 시도 중</summary>
    Connecting,

    /// <summary>연결됨</summary>
    Connected,

    /// <summary>재연결 시도 중</summary>
    Reconnecting,

    /// <summary>에러 발생</summary>
    Error
}
