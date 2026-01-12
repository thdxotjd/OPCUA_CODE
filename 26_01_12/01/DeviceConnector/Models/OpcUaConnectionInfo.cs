using System;

namespace DeviceConnector.Models
{
    /// <summary>
    /// OPC UA 서버 연결 정보
    /// </summary>
    public class OpcUaConnectionInfo
    {
        /// <summary>
        /// OPC UA 서버 엔드포인트 URL
        /// 예: opc.tcp://192.168.0.19:49320
        /// </summary>
        public string EndpointUrl { get; set; } = "opc.tcp://localhost:49320";

        /// <summary>연결 세션 이름</summary>
        public string SessionName { get; set; } = "DeviceConnectorSession";

        /// <summary>연결 타임아웃 (초)</summary>
        public int ConnectionTimeoutSeconds { get; set; } = 10;

        /// <summary>세션 타임아웃 (분)</summary>
        public int SessionTimeoutMinutes { get; set; } = 30;

        /// <summary>자동 재연결 활성화</summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>재연결 시도 간격 (초)</summary>
        public int ReconnectIntervalSeconds { get; set; } = 5;

        /// <summary>사용자 인증 (null이면 Anonymous)</summary>
        public OpcUaCredentials? Credentials { get; set; }
    }

    /// <summary>
    /// OPC UA 인증 정보
    /// </summary>
    public class OpcUaCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// KEPServerEX 디바이스 태그 설정
    /// </summary>
    public class DeviceTagConfig
    {
        /// <summary>디바이스 식별자</summary>
        public string DeviceId { get; set; } = "ESP32_01";

        /// <summary>KEPServerEX 채널명</summary>
        public string ChannelName { get; set; } = "ModbusTCP";

        /// <summary>KEPServerEX 디바이스명</summary>
        public string DeviceName { get; set; } = "ESP32_01";

        /// <summary>
        /// OPC UA 노드 ID 생성
        /// KEPServerEX 형식: ns=2;s=Channel.Device.Tag
        /// </summary>
        public string GetNodeId(string tagName)
        {
            return $"ns=2;s={ChannelName}.{DeviceName}.{tagName}";
        }

        // KEPServerEX 태그 이름 상수
        public const string TAG_POS_X_LOW = "REG_POS_X_LOW";    // 40001
        public const string TAG_POS_X_HIGH = "REG_POS_X_HIGH";  // 40002
        public const string TAG_SPEED = "REG_SPEED";            // 40007
        public const string TAG_STATUS = "REG_STATUS";          // 40008
    }
}
