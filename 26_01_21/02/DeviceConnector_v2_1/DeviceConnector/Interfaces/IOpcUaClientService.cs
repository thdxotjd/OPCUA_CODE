namespace DeviceConnector.Interfaces;

using DeviceConnector.Events;
using DeviceConnector.Models;

/// <summary>
/// OPC UA 클라이언트 서비스 인터페이스
/// KEPServerEX를 통한 ESP32 ModbusTCP 디바이스 통신
/// </summary>
public interface IOpcUaClientService : IDisposable
{
    #region Events

    /// <summary>
    /// 데이터 변경 이벤트 (구독 시 발생)
    /// </summary>
    event EventHandler<DataChangedEventArgs>? DataChanged;

    /// <summary>
    /// 연결 상태 변경 이벤트
    /// </summary>
    event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;

    /// <summary>
    /// 쓰기 완료 이벤트
    /// </summary>
    event EventHandler<WriteCompletedEventArgs>? WriteCompleted;

    #endregion

    #region Properties

    /// <summary>
    /// 연결 상태
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 현재 연결 상태 정보
    /// </summary>
    ConnectionStatus Status { get; }

    #endregion

    #region Connection

    /// <summary>
    /// OPC UA 서버 연결
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// OPC UA 서버 연결 해제
    /// </summary>
    Task DisconnectAsync();

    #endregion

    #region Device Configuration

    /// <summary>
    /// 디바이스 설정 추가
    /// </summary>
    void AddDeviceConfig(DeviceTagConfig config);

    /// <summary>
    /// 디바이스 설정 제거
    /// </summary>
    void RemoveDeviceConfig(string deviceId);

    /// <summary>
    /// 등록된 모든 디바이스 ID 반환
    /// </summary>
    IEnumerable<string> GetRegisteredDeviceIds();

    #endregion

    #region Read Operations (Read Only Tags)

    /// <summary>
    /// 디바이스 데이터 읽기 (전체 태그)
    /// </summary>
    Task<ESP32Data?> ReadDeviceDataAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 위치 데이터만 읽기 (POS_X, POS_Y, POS_T)
    /// </summary>
    Task<(float PosX, float PosY, float PosT)?> ReadPositionAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// State 읽기 (String) - Read/Write 가능
    /// </summary>
    Task<string?> ReadStateAsync(string deviceId, CancellationToken cancellationToken = default);

    #endregion

    #region Write Operations (Writable Tags)

    /// <summary>
    /// TargetA 쓰기 (Boolean)
    /// </summary>
    Task<bool> WriteTargetAAsync(string deviceId, bool value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Control 쓰기 (String)
    /// </summary>
    Task<bool> WriteControlAsync(string deviceId, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// State 쓰기 (String)
    /// </summary>
    Task<bool> WriteStateAsync(string deviceId, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 여러 태그 동시 쓰기
    /// </summary>
    Task<Dictionary<string, bool>> WriteMultipleAsync(string deviceId, Dictionary<string, object> tagValues, CancellationToken cancellationToken = default);

    #endregion

    #region Subscription

    /// <summary>
    /// 디바이스 구독 시작 (실시간 데이터 수신)
    /// </summary>
    Task StartSubscriptionAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 디바이스 구독 중지
    /// </summary>
    Task StopSubscriptionAsync(string deviceId);

    /// <summary>
    /// 모든 디바이스 구독 시작
    /// </summary>
    Task StartAllSubscriptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 모든 구독 중지
    /// </summary>
    Task StopAllSubscriptionsAsync();

    #endregion
}
