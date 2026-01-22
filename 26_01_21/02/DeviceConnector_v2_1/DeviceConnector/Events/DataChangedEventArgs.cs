namespace DeviceConnector.Events;

using DeviceConnector.Models;

/// <summary>
/// 데이터 변경 이벤트 인자
/// </summary>
public class DataChangedEventArgs : EventArgs
{
    /// <summary>
    /// 디바이스 ID
    /// </summary>
    public string DeviceId { get; }

    /// <summary>
    /// 변경된 ESP32 데이터
    /// </summary>
    public ESP32Data Data { get; }

    /// <summary>
    /// 변경된 태그 이름 목록
    /// </summary>
    public IReadOnlyList<string> ChangedTags { get; }

    public DataChangedEventArgs(string deviceId, ESP32Data data, IEnumerable<string>? changedTags = null)
    {
        DeviceId = deviceId;
        Data = data;
        ChangedTags = changedTags?.ToList() ?? new List<string>();
    }
}

/// <summary>
/// 연결 상태 변경 이벤트 인자
/// </summary>
public class ConnectionChangedEventArgs : EventArgs
{
    public bool IsConnected { get; }
    public string? EndpointUrl { get; }
    public string? ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public ConnectionChangedEventArgs(bool isConnected, string? endpointUrl = null, string? errorMessage = null)
    {
        IsConnected = isConnected;
        EndpointUrl = endpointUrl;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// 쓰기 완료 이벤트 인자
/// </summary>
public class WriteCompletedEventArgs : EventArgs
{
    public string DeviceId { get; }
    public string TagName { get; }
    public object? Value { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public WriteCompletedEventArgs(string deviceId, string tagName, object? value, bool success, string? errorMessage = null)
    {
        DeviceId = deviceId;
        TagName = tagName;
        Value = value;
        Success = success;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }
}
