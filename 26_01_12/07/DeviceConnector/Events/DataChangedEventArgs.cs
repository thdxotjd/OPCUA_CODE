using System;
using DeviceConnector.Models;

namespace DeviceConnector.Events
{
    /// <summary>
    /// 디바이스 데이터 변경 이벤트
    /// OPC UA 구독에서 데이터 변경 시 발생
    /// </summary>
    public class DataChangedEventArgs : EventArgs
    {
        /// <summary>디바이스 ID</summary>
        public string DeviceId { get; }

        /// <summary>새로 수신된 데이터</summary>
        public ESP32Data Data { get; }

        /// <summary>이전 데이터 (최초 수신 시 null)</summary>
        public ESP32Data? PreviousData { get; }

        /// <summary>이벤트 발생 시간</summary>
        public DateTime EventTime { get; }

        public DataChangedEventArgs(string deviceId, ESP32Data data, ESP32Data? previousData = null)
        {
            DeviceId = deviceId;
            Data = data;
            PreviousData = previousData;
            EventTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 연결 상태 변경 이벤트
    /// </summary>
    public class ConnectionChangedEventArgs : EventArgs
    {
        /// <summary>연결 상태 정보</summary>
        public ConnectionStatus Status { get; }

        /// <summary>이전 상태</summary>
        public ConnectionState PreviousState { get; }

        public ConnectionChangedEventArgs(ConnectionStatus status, ConnectionState previousState)
        {
            Status = status;
            PreviousState = previousState;
        }
    }

    /// <summary>
    /// 에러 발생 이벤트
    /// </summary>
    public class ErrorOccurredEventArgs : EventArgs
    {
        /// <summary>에러 메시지</summary>
        public string Message { get; }

        /// <summary>예외 객체</summary>
        public Exception? Exception { get; }

        /// <summary>복구 가능 여부</summary>
        public bool IsRecoverable { get; }

        public ErrorOccurredEventArgs(string message, Exception? exception = null, bool isRecoverable = true)
        {
            Message = message;
            Exception = exception;
            IsRecoverable = isRecoverable;
        }
    }
}
