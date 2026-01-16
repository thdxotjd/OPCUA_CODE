using System;
using System.Threading;
using System.Threading.Tasks;
using DeviceConnector.Models;
using DeviceConnector.Events;

namespace DeviceConnector.Interfaces
{
    /// <summary>
    /// OPC UA 클라이언트 서비스 인터페이스
    /// ASP.NET Core DI에서 사용
    /// </summary>
    public interface IOpcUaClientService : IDisposable
    {
        #region 연결 관리

        /// <summary>
        /// OPC UA 서버에 연결
        /// </summary>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>연결 성공 여부</returns>
        Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// OPC UA 서버 연결 해제
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 현재 연결 상태
        /// </summary>
        ConnectionStatus ConnectionStatus { get; }

        /// <summary>
        /// 연결 여부
        /// </summary>
        bool IsConnected { get; }

        #endregion

        #region 데이터 읽기

        /// <summary>
        /// ESP32 데이터 읽기 (단일 호출)
        /// </summary>
        /// <param name="deviceId">디바이스 ID (기본: ESP32_01)</param>
        /// <returns>ESP32 데이터</returns>
        Task<ESP32Data?> ReadDataAsync(string deviceId = "ESP32_01");

        /// <summary>
        /// 특정 태그 값 읽기
        /// </summary>
        /// <typeparam name="T">반환 타입</typeparam>
        /// <param name="nodeId">OPC UA 노드 ID</param>
        /// <returns>태그 값</returns>
        Task<T?> ReadTagAsync<T>(string nodeId);

        /// <summary>
        /// 마지막으로 읽은 데이터 (캐시)
        /// </summary>
        ESP32Data? LastData { get; }

        #endregion

        #region 데이터 쓰기

        /// <summary>
        /// 특정 태그에 값 쓰기
        /// </summary>
        /// <typeparam name="T">값 타입</typeparam>
        /// <param name="nodeId">OPC UA 노드 ID</param>
        /// <param name="value">쓸 값</param>
        /// <returns>성공 여부</returns>
        Task<bool> WriteTagAsync<T>(string nodeId, T value);

        /// <summary>
        /// POS_X 값 쓰기
        /// </summary>
        /// <param name="value">X축 위치값</param>
        /// <param name="deviceId">디바이스 ID</param>
        /// <returns>성공 여부</returns>
        Task<bool> WritePosXAsync(short value, string deviceId = "ESP32_01");

        /// <summary>
        /// POS_Y 값 쓰기
        /// </summary>
        /// <param name="value">Y축 위치값</param>
        /// <param name="deviceId">디바이스 ID</param>
        /// <returns>성공 여부</returns>
        Task<bool> WritePosYAsync(short value, string deviceId = "ESP32_01");

        /// <summary>
        /// State 값 쓰기
        /// </summary>
        /// <param name="value">상태값</param>
        /// <param name="deviceId">디바이스 ID</param>
        /// <returns>성공 여부</returns>
        Task<bool> WriteStateAsync(bool value, string deviceId = "ESP32_01");

        /// <summary>
        /// To 값 쓰기
        /// </summary>
        /// <param name="value">목표 도달 여부</param>
        /// <param name="deviceId">디바이스 ID</param>
        /// <returns>성공 여부</returns>
        Task<bool> WriteToAsync(bool value, string deviceId = "ESP32_01");

        #endregion

        #region 구독 (실시간 모니터링)

        /// <summary>
        /// 데이터 변경 구독 시작
        /// </summary>
        /// <param name="samplingIntervalMs">샘플링 간격 (밀리초)</param>
        /// <param name="deviceId">디바이스 ID</param>
        Task StartSubscriptionAsync(int samplingIntervalMs = 100, string deviceId = "ESP32_01");

        /// <summary>
        /// 구독 중지
        /// </summary>
        Task StopSubscriptionAsync();

        /// <summary>
        /// 구독 활성화 여부
        /// </summary>
        bool IsSubscribed { get; }

        #endregion

        #region 이벤트

        /// <summary>
        /// 데이터 변경 시 발생
        /// </summary>
        event EventHandler<DataChangedEventArgs>? DataChanged;

        /// <summary>
        /// 연결 상태 변경 시 발생
        /// </summary>
        event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;

        /// <summary>
        /// 에러 발생 시
        /// </summary>
        event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

        #endregion

        #region 설정

        /// <summary>
        /// 연결 설정 변경
        /// </summary>
        void Configure(OpcUaConnectionInfo connectionInfo);

        /// <summary>
        /// 디바이스 태그 설정 추가
        /// </summary>
        void AddDeviceConfig(DeviceTagConfig config);

        #endregion
    }
}
