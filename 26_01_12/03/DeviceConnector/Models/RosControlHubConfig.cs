namespace DeviceConnector.Models
{
    /// <summary>
    /// ROS_ControlHub 서버 연결 설정
    /// </summary>
    public class RosControlHubConfig
    {
        /// <summary>
        /// ROS_ControlHub 서버 기본 URL
        /// 예: http://localhost:5178
        /// </summary>
        public string ServerUrl { get; set; } = "http://localhost:5178";

        /// <summary>
        /// gRPC 엔드포인트 URL (자동 생성)
        /// </summary>
        public string GrpcUrl => ServerUrl;

        /// <summary>
        /// SignalR State Hub URL (자동 생성)
        /// </summary>
        public string StateHubUrl => $"{ServerUrl}/hubs/state";

        /// <summary>
        /// SignalR WebRTC Hub URL (자동 생성)
        /// </summary>
        public string WebRtcHubUrl => $"{ServerUrl}/hubs/webrtc";

        /// <summary>
        /// 연결 타임아웃 (초)
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// 자동 재연결 활성화
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// 재연결 시도 간격 (초)
        /// </summary>
        public int ReconnectIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// 최대 재연결 시도 횟수 (0 = 무한)
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 0;

        /// <summary>
        /// SignalR 상태 그룹 이름
        /// </summary>
        public string DefaultStateGroup { get; set; } = "default";
    }
}
