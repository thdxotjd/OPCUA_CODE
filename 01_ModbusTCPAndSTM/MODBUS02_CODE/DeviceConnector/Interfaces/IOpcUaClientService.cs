namespace DeviceConnector.Interfaces;

using DeviceConnector.Events;
using DeviceConnector.Models;

/// <summary>
/// OPC UA 클라이언트 서비스 인터페이스
/// gRPC 개발자가 사용할 계약
/// </summary>
public interface IOpcUaClientService : IDisposable
{
    #region 이벤트

    /// <summary>데이터 변경 시 발생 (Subscription)</summary>
    event EventHandler<DataChangedEventArgs>? DataChanged;

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

    #endregion

    #region 연결 관리

    /// <summary>OPC UA 서버에 연결</summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>OPC UA 서버 연결 해제</summary>
    Task DisconnectAsync();

    #endregion

    #region 디바이스 관리

    /// <summary>디바이스 설정 추가</summary>
    void AddDeviceConfig(DeviceTagConfig config);

    /// <summary>디바이스 설정 제거</summary>
    void RemoveDeviceConfig(string deviceId);

    /// <summary>등록된 디바이스 목록 조회</summary>
    IReadOnlyList<string> GetRegisteredDevices();

    #endregion

    #region 데이터 읽기

    /// <summary>특정 디바이스의 ESP32 데이터 읽기</summary>
    Task<ESP32Data?> ReadDeviceDataAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>모든 디바이스의 ESP32 데이터 읽기</summary>
    Task<Dictionary<string, ESP32Data>> ReadAllDevicesDataAsync(CancellationToken cancellationToken = default);

    #endregion

    #region 데이터 쓰기

    /// <summary>
    /// TargetA 태그에 Boolean 값 쓰기
    /// ※ v2.2: Coil 주소(00007) 사용 - FC05 Write Single Coil
    /// </summary>
    Task<bool> WriteTargetAAsync(string deviceId, bool value, CancellationToken cancellationToken = default);

    /// <summary>Control 태그에 문자열 쓰기</summary>
    Task<bool> WriteControlAsync(string deviceId, string value, CancellationToken cancellationToken = default);

    /// <summary>State 태그에 문자열 쓰기</summary>
    Task<bool> WriteStateAsync(string deviceId, string value, CancellationToken cancellationToken = default);

    /// <summary>특정 태그에 값 쓰기 (범용)</summary>
    Task<bool> WriteTagAsync(string deviceId, string tagName, object value, CancellationToken cancellationToken = default);

    #endregion

    #region 구독 관리

    /// <summary>특정 디바이스 데이터 구독 시작</summary>
    Task StartSubscriptionAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>특정 디바이스 구독 중지</summary>
    Task StopSubscriptionAsync(string deviceId);

    /// <summary>모든 디바이스 구독 시작</summary>
    Task StartAllSubscriptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>모든 구독 중지</summary>
    Task StopAllSubscriptionsAsync();

    #endregion
}
