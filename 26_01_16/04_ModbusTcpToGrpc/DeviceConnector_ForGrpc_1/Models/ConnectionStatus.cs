using System;

namespace DeviceConnector.Models
{
    /// <summary>
    /// OPC UA 클라이언트 연결 상태
    /// </summary>
    public class ConnectionStatus
    {
        /// <summary>
        /// 현재 연결 상태
        /// </summary>
        public ConnectionState State { get; set; } = ConnectionState.Disconnected;

        /// <summary>
        /// 서버 엔드포인트 URL
        /// </summary>
        public string EndpointUrl { get; set; } = string.Empty;

        /// <summary>
        /// 마지막 연결 시도 시간
        /// </summary>
        public DateTime? LastConnectAttempt { get; set; }

        /// <summary>
        /// 연결 성공 시간
        /// </summary>
        public DateTime? ConnectedSince { get; set; }

        /// <summary>
        /// 마지막 에러 메시지
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// 재연결 시도 횟수
        /// </summary>
        public int ReconnectAttempts { get; set; }

        /// <summary>
        /// 연결 여부
        /// </summary>
        public bool IsConnected => State == ConnectionState.Connected;

        /// <summary>
        /// 연결 지속 시간
        /// </summary>
        public TimeSpan? ConnectionDuration => 
            ConnectedSince.HasValue ? DateTime.UtcNow - ConnectedSince.Value : null;
    }

    /// <summary>
    /// 연결 상태 열거형
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>연결 해제됨</summary>
        Disconnected,
        
        /// <summary>연결 시도 중</summary>
        Connecting,
        
        /// <summary>연결됨</summary>
        Connected,
        
        /// <summary>재연결 중</summary>
        Reconnecting,
        
        /// <summary>연결 오류</summary>
        Error
    }
}
