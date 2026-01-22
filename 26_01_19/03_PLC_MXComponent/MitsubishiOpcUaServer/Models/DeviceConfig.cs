using System.Collections.Generic;

namespace MitsubishiOpcUaServer.Models
{
    /// <summary>
    /// 디바이스 설정 모델
    /// 사용 기한: 2026년 2월 28일까지
    /// </summary>
    public class DeviceConfig
    {
        /// <summary>
        /// 채널명
        /// </summary>
        public string ChannelName { get; set; } = "MxComponent";

        /// <summary>
        /// 디바이스명
        /// </summary>
        public string DeviceName { get; set; } = "Q02UCPU";

        /// <summary>
        /// MX Component Logical Station Number
        /// </summary>
        public int LogicalStationNumber { get; set; } = 0;

        /// <summary>
        /// 태그 설정 (Key: 별칭, Value: PLC 주소)
        /// 예: { "Temperature": "D100", "RunStatus": "M0" }
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new();

        /// <summary>
        /// 스캔 주기 (ms)
        /// </summary>
        public int ScanRate { get; set; } = 100;

        /// <summary>
        /// OPC UA Node ID 생성
        /// </summary>
        public string GetNodeId(string tagAlias)
        {
            return $"ns=2;s={ChannelName}.{DeviceName}.{tagAlias}";
        }

        /// <summary>
        /// 모든 태그의 Node ID 목록 반환
        /// </summary>
        public Dictionary<string, string> GetAllNodeIds()
        {
            var nodeIds = new Dictionary<string, string>();
            foreach (var tag in Tags)
            {
                nodeIds[tag.Key] = $"ns=2;s={ChannelName}.{DeviceName}.{tag.Key}";
            }
            return nodeIds;
        }
    }

    /// <summary>
    /// OPC UA 서버 설정
    /// </summary>
    public class OpcUaServerConfig
    {
        /// <summary>
        /// 서버 포트
        /// </summary>
        public int Port { get; set; } = 4840;

        /// <summary>
        /// 서버 이름
        /// </summary>
        public string ServerName { get; set; } = "MitsubishiOpcUaServer";

        /// <summary>
        /// 익명 접근 허용
        /// </summary>
        public bool AllowAnonymous { get; set; } = true;
    }

    /// <summary>
    /// 전체 앱 설정
    /// </summary>
    public class AppSettings
    {
        public DeviceConfig Device { get; set; } = new();
        public OpcUaServerConfig OpcUaServer { get; set; } = new();
    }
}
