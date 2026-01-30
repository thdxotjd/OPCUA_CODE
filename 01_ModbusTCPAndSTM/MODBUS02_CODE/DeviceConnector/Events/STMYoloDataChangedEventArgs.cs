namespace DeviceConnector.Events;

using DeviceConnector.Models;

/// <summary>
/// STM_yolo 데이터 변경 이벤트 인자
/// </summary>
public class STMYoloDataChangedEventArgs : EventArgs
{
    /// <summary>디바이스 ID</summary>
    public string DeviceId { get; }

    /// <summary>변경된 데이터</summary>
    public STMYoloData Data { get; }

    /// <summary>변경된 태그 이름</summary>
    public string? ChangedTagName { get; }

    /// <summary>변경 전 값</summary>
    public object? PreviousValue { get; }

    /// <summary>변경 후 값</summary>
    public object? NewValue { get; }

    /// <summary>변경 시간</summary>
    public DateTime Timestamp { get; }

    public STMYoloDataChangedEventArgs(
        string deviceId, 
        STMYoloData data, 
        string? changedTagName = null,
        object? previousValue = null,
        object? newValue = null)
    {
        DeviceId = deviceId;
        Data = data;
        ChangedTagName = changedTagName;
        PreviousValue = previousValue;
        NewValue = newValue;
        Timestamp = DateTime.UtcNow;
    }
}
