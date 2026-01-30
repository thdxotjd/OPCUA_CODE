namespace DeviceConnector.Interfaces;

using DeviceConnector.Events;
using DeviceConnector.Models;

/// <summary>
/// STM_yolo OPC UA 클라이언트 서비스 인터페이스
/// KEPServerEX Channel1_opcua > ModbusTCP > STM > Stm_yolo 통신
/// </summary>
public interface ISTMYoloClientService : IDisposable
{
    #region 이벤트

    /// <summary>데이터 변경 시 발생 (Subscription)</summary>
    event EventHandler<STMYoloDataChangedEventArgs>? DataChanged;

    /// <summary>연결 상태 변경 시 발생</summary>
    event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;

    /// <summary>Write 완료 시 발생</summary>
    event EventHandler<WriteCompletedEventArgs>? WriteCompleted;

    /// <summary>에러 발생 시</summary>
    event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    #endregion

    #region 속성

    /// <summary>연결 상태</summary>
    ConnectionStatus Status { get; }

    /// <summary>연결 여부</summary>
    bool IsConnected { get; }

    /// <summary>현재 디바이스 데이터</summary>
    STMYoloData? CurrentData { get; }

    #endregion

    #region 연결 관리

    /// <summary>OPC UA 서버에 연결</summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>OPC UA 서버 연결 해제</summary>
    Task DisconnectAsync();

    #endregion

    #region 데이터 읽기

    /// <summary>모든 태그 데이터 읽기</summary>
    Task<STMYoloData?> ReadAllDataAsync(CancellationToken cancellationToken = default);

    /// <summary>Current 태그만 읽기 (Read 태그)</summary>
    Task<STMYoloData?> ReadCurrentDataAsync(CancellationToken cancellationToken = default);

    /// <summary>Target 태그만 읽기 (Write 태그 현재 값)</summary>
    Task<STMYoloData?> ReadTargetDataAsync(CancellationToken cancellationToken = default);

    /// <summary>특정 태그 값 읽기</summary>
    Task<object?> ReadTagAsync(string tagName, CancellationToken cancellationToken = default);

    #endregion

    #region 데이터 쓰기 - Target Tags

    /// <summary>TargetState 쓰기</summary>
    Task<bool> WriteTargetStateAsync(long value, CancellationToken cancellationToken = default);

    /// <summary>TargetSpeedMain 쓰기</summary>
    Task<bool> WriteTargetSpeedMainAsync(long value, CancellationToken cancellationToken = default);

    /// <summary>TargetSpeedSort 쓰기</summary>
    Task<bool> WriteTargetSpeedSortAsync(long value, CancellationToken cancellationToken = default);

    /// <summary>TargetSpeedLoad 쓰기</summary>
    Task<bool> WriteTargetSpeedLoadAsync(long value, CancellationToken cancellationToken = default);

    /// <summary>AgvSortArrived 쓰기</summary>
    Task<bool> WriteAgvSortArrivedAsync(bool value, CancellationToken cancellationToken = default);

    /// <summary>AgvSortDeparted 쓰기</summary>
    Task<bool> WriteAgvSortDepartedAsync(bool value, CancellationToken cancellationToken = default);

    /// <summary>AgvLoadArrived 쓰기</summary>
    Task<bool> WriteAgvLoadArrivedAsync(bool value, CancellationToken cancellationToken = default);

    /// <summary>AgvLoadDeparted 쓰기</summary>
    Task<bool> WriteAgvLoadDepartedAsync(bool value, CancellationToken cancellationToken = default);

    /// <summary>모든 속도 한번에 쓰기</summary>
    Task<bool> WriteAllSpeedsAsync(long main, long sort, long load, CancellationToken cancellationToken = default);

    /// <summary>특정 태그에 값 쓰기 (범용)</summary>
    Task<bool> WriteTagAsync(string tagName, object value, CancellationToken cancellationToken = default);

    #endregion

    #region 구독 관리

    /// <summary>Current 태그 구독 시작 (Read 태그)</summary>
    Task StartCurrentSubscriptionAsync(CancellationToken cancellationToken = default);

    /// <summary>모든 태그 구독 시작</summary>
    Task StartAllSubscriptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>구독 중지</summary>
    Task StopSubscriptionAsync();

    #endregion
}
