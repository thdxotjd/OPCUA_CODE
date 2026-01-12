using System;
using System.Threading.Tasks;
using DeviceConnector.Events;
using DeviceConnector.Models;

namespace DeviceConnector.Interfaces
{
    /// <summary>
    /// OPC UA 클라이언트 서비스 인터페이스
    /// gRPC 개발자는 이 인터페이스를 통해 OPC UA 통신 수행
    /// </summary>
    public interface IOpcUaClientService : IDisposable
    {
        #region 속성

        /// <summary>연결 상태 정보</summary>
        ConnectionStatus ConnectionStatus { get; }

        /// <summary>연결 여부</summary>
        bool IsConnected { get; }

        /// <summary>구독 중 여부</summary>
        bool IsSubscribed { get; }

        /// <summary>마지막으로 수신한 데이터</summary>
        ESP32Data? LastData { get; }

        #endregion

        #region 연결 관리

        /// <summary>
        /// OPC UA 서버에 연결
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// 연결 해제
        /// </summary>
        Task DisconnectAsync();

        #endregion

        #region 데이터 읽기/쓰기

        /// <summary>
        /// ESP32 데이터 한 번 읽기 (Polling 방식)
        /// </summary>
        Task<ESP32Data?> ReadDataAsync(string deviceId = "ESP32_01");

        /// <summary>
        /// ESP32에 명령 쓰기
        /// </summary>
        Task<bool> WriteCommandAsync(string deviceId, string tagName, object value);

        #endregion

        #region 구독 (실시간 데이터)

        /// <summary>
        /// 실시간 데이터 구독 시작
        /// 데이터 변경 시 DataChanged 이벤트 발생
        /// </summary>
        Task StartSubscriptionAsync(int samplingIntervalMs = 100, string deviceId = "ESP32_01");

        /// <summary>
        /// 구독 중지
        /// </summary>
        Task StopSubscriptionAsync();

        #endregion

        #region 이벤트

        /// <summary>데이터 변경 시 발생 (구독 모드)</summary>
        event EventHandler<DataChangedEventArgs>? DataChanged;

        /// <summary>연결 상태 변경 시 발생</summary>
        event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;

        /// <summary>에러 발생 시</summary>
        event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

        #endregion
    }
}
