using System;
using DeviceConnector.Models;

namespace DeviceConnector.Events
{
    /// <summary>
    /// 디바이스 데이터 변경 이벤트 인자
    /// </summary>
    public class DataChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 디바이스 ID
        /// </summary>
        public string DeviceId { get; }

        /// <summary>
        /// 변경된 데이터
        /// </summary>
        public ESP32Data Data { get; }

        /// <summary>
        /// 이전 데이터 (최초 수신 시 null)
        /// </summary>
        public ESP32Data? PreviousData { get; }

        /// <summary>
        /// 이벤트 발생 시간
        /// </summary>
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
    /// 연결 상태 변경 이벤트 인자
    /// </summary>
    public class ConnectionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 현재 연결 상태
        /// </summary>
        public ConnectionStatus Status { get; }

        /// <summary>
        /// 이전 연결 상태
        /// </summary>
        public ConnectionState PreviousState { get; }

        /// <summary>
        /// 이벤트 발생 시간
        /// </summary>
        public DateTime EventTime { get; }

        public ConnectionChangedEventArgs(ConnectionStatus status, ConnectionState previousState)
        {
            Status = status;
            PreviousState = previousState;
            EventTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 에러 발생 이벤트 인자
    /// </summary>
    public class ErrorOccurredEventArgs : EventArgs
    {
        /// <summary>
        /// 에러 메시지
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 예외 정보 (있는 경우)
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 에러 심각도
        /// </summary>
        public ErrorSeverity Severity { get; }

        /// <summary>
        /// 이벤트 발생 시간
        /// </summary>
        public DateTime EventTime { get; }

        public ErrorOccurredEventArgs(string message, Exception? exception = null, ErrorSeverity severity = ErrorSeverity.Error)
        {
            Message = message;
            Exception = exception;
            Severity = severity;
            EventTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 에러 심각도
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>정보</summary>
        Info,
        
        /// <summary>경고</summary>
        Warning,
        
        /// <summary>에러</summary>
        Error,
        
        /// <summary>치명적 에러</summary>
        Critical
    }
}
