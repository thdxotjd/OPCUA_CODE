using System;

namespace DeviceConnector.Models
{
    /// <summary>
    /// OPC UA 서버 연결 상태 정보
    /// </summary>
    public class ConnectionStatus
    {
        /// <summary>현재 연결 상태</summary>
        public ConnectionState State { get; set; } = ConnectionState.Disconnected;

        /// <summary>연결된 서버 URL</summary>
        public string EndpointUrl { get; set; } = string.Empty;

        /// <summary>마지막 연결 시도 시간</summary>
        public DateTime LastConnectAttempt { get; set; }

        /// <summary>연결 성공 시간</summary>
        public DateTime? ConnectedSince { get; set; }

        /// <summary>마지막 에러 메시지</summary>
        public string? LastErrorMessage { get; set; }

        /// <summary>재연결 시도 횟수</summary>
        public int ReconnectAttempts { get; set; }
    }

    /// <summary>
    /// 연결 상태 열거형
    /// </summary>
    public enum ConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Reconnecting = 3,
        Error = 4
    }
}
