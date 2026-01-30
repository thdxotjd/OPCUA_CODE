namespace DeviceConnector.Events;

using DeviceConnector.Models;

/// <summary>
/// 데이터 변경 이벤트 인자
/// </summary>
public class DataChangedEventArgs : EventArgs
{
    /// <summary>디바이스 ID</summary>
    public string DeviceId { get; }

    /// <summary>변경된 데이터</summary>
    public ESP32Data Data { get; }

    /// <summary>변경 시간</summary>
    public DateTime Timestamp { get; }

    public DataChangedEventArgs(string deviceId, ESP32Data data)
    {
        DeviceId = deviceId;
        Data = data;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// 연결 상태 변경 이벤트 인자
/// </summary>
public class ConnectionChangedEventArgs : EventArgs
{
    /// <summary>현재 연결 상태</summary>
    public ConnectionStatus Status { get; }

    /// <summary>이전 연결 상태</summary>
    public ConnectionState PreviousState { get; }

    /// <summary>변경 시간</summary>
    public DateTime Timestamp { get; }

    public ConnectionChangedEventArgs(ConnectionStatus status, ConnectionState previousState)
    {
        Status = status;
        PreviousState = previousState;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Write 완료 이벤트 인자
/// </summary>
public class WriteCompletedEventArgs : EventArgs
{
    /// <summary>디바이스 ID</summary>
    public string DeviceId { get; }

    /// <summary>태그 이름</summary>
    public string TagName { get; }

    /// <summary>쓰기 값</summary>
    public object Value { get; }

    /// <summary>성공 여부</summary>
    public bool Success { get; }

    /// <summary>에러 메시지 (실패 시)</summary>
    public string? ErrorMessage { get; }

    /// <summary>완료 시간</summary>
    public DateTime Timestamp { get; }

    public WriteCompletedEventArgs(string deviceId, string tagName, object value, bool success, string? errorMessage = null)
    {
        DeviceId = deviceId;
        TagName = tagName;
        Value = value;
        Success = success;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// 에러 발생 이벤트 인자
/// </summary>
public class ErrorOccurredEventArgs : EventArgs
{
    /// <summary>에러 메시지</summary>
    public string Message { get; }

    /// <summary>예외 객체</summary>
    public Exception? Exception { get; }

    /// <summary>발생 시간</summary>
    public DateTime Timestamp { get; }

    public ErrorOccurredEventArgs(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
        Timestamp = DateTime.UtcNow;
    }
}
