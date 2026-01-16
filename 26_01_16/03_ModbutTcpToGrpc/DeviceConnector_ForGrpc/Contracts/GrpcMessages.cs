using System;

namespace DeviceConnector.Contracts
{
    #region 읽기 요청/응답

    /// <summary>
    /// 디바이스 데이터 읽기 요청
    /// </summary>
    public class ReadDataRequest
    {
        /// <summary>디바이스 ID (기본: "ESP32_01")</summary>
        public string DeviceId { get; set; } = "ESP32_01";
    }

    /// <summary>
    /// 디바이스 데이터 읽기 응답
    /// </summary>
    public class ReadDataResponse
    {
        /// <summary>성공 여부</summary>
        public bool Success { get; set; }

        /// <summary>에러 메시지 (실패 시)</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>디바이스 데이터</summary>
        public DeviceDataDto? Data { get; set; }
    }

    /// <summary>
    /// 태그 읽기 요청
    /// </summary>
    public class ReadTagRequest
    {
        /// <summary>디바이스 ID</summary>
        public string DeviceId { get; set; } = "ESP32_01";

        /// <summary>태그명 (POS_X, POS_Y, State, To)</summary>
        public string TagName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 태그 읽기 응답
    /// </summary>
    public class ReadTagResponse
    {
        /// <summary>성공 여부</summary>
        public bool Success { get; set; }

        /// <summary>에러 메시지</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>태그 데이터</summary>
        public TagDataDto? Data { get; set; }
    }

    #endregion

    #region 쓰기 요청/응답

    /// <summary>
    /// 태그 쓰기 요청
    /// </summary>
    public class WriteTagRequest
    {
        /// <summary>디바이스 ID</summary>
        public string DeviceId { get; set; } = "ESP32_01";

        /// <summary>태그명 (POS_X, POS_Y, State, To)</summary>
        public string TagName { get; set; } = string.Empty;

        /// <summary>쓸 값 (POS_X/POS_Y: short, State/To: bool)</summary>
        public object? Value { get; set; }
    }

    /// <summary>
    /// 태그 쓰기 응답
    /// </summary>
    public class WriteTagResponse
    {
        /// <summary>성공 여부</summary>
        public bool Success { get; set; }

        /// <summary>에러 메시지</summary>
        public string? ErrorMessage { get; set; }
    }

    #endregion

    #region 구독 요청/응답

    /// <summary>
    /// 데이터 구독 요청 (실시간 스트리밍)
    /// </summary>
    public class SubscribeRequest
    {
        /// <summary>디바이스 ID</summary>
        public string DeviceId { get; set; } = "ESP32_01";

        /// <summary>샘플링 간격 (밀리초, 기본: 100ms)</summary>
        public int SamplingIntervalMs { get; set; } = 100;
    }

    /// <summary>
    /// 구독 데이터 (스트림으로 전달)
    /// </summary>
    public class SubscribeDataResponse
    {
        /// <summary>디바이스 데이터</summary>
        public DeviceDataDto? Data { get; set; }

        /// <summary>시퀀스 번호</summary>
        public long SequenceNumber { get; set; }
    }

    #endregion

    #region 연결 상태

    /// <summary>
    /// 연결 상태 응답
    /// </summary>
    public class ConnectionStatusResponse
    {
        /// <summary>연결 여부</summary>
        public bool IsConnected { get; set; }

        /// <summary>연결 상태 (Disconnected, Connecting, Connected, Reconnecting, Error)</summary>
        public string State { get; set; } = "Disconnected";

        /// <summary>OPC UA 서버 엔드포인트 URL</summary>
        public string? EndpointUrl { get; set; }

        /// <summary>연결 시작 시간</summary>
        public DateTime? ConnectedSince { get; set; }

        /// <summary>재연결 시도 횟수</summary>
        public int ReconnectAttempts { get; set; }

        /// <summary>마지막 에러 메시지</summary>
        public string? LastError { get; set; }
    }

    #endregion
}
