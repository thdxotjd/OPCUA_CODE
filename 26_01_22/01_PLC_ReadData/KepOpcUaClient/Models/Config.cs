namespace KepOpcUaClient.Models
{
    /// <summary>
    /// KEPServerEX 연결 설정
    /// 사용 기한: 2026년 2월 28일까지
    /// </summary>
    public class KepServerConfig
    {
        /// <summary>
        /// OPC UA 서버 엔드포인트 URL
        /// KEPServerEX 기본: opc.tcp://localhost:49320
        /// </summary>
        public string EndpointUrl { get; set; } = "opc.tcp://localhost:49320";

        /// <summary>
        /// 서버명
        /// </summary>
        public string ServerName { get; set; } = "PTC.KepwareServer";

        /// <summary>
        /// 보안 모드 (None, Sign, SignAndEncrypt)
        /// </summary>
        public string SecurityMode { get; set; } = "None";

        /// <summary>
        /// 익명 접근 허용
        /// </summary>
        public bool AllowAnonymous { get; set; } = true;
    }

    /// <summary>
    /// 디바이스 설정
    /// </summary>
    public class DeviceConfig
    {
        /// <summary>
        /// KEPServerEX 채널명
        /// </summary>
        public string ChannelName { get; set; } = "PLC01";

        /// <summary>
        /// KEPServerEX Collection 명
        /// </summary>
        public string CollectionName { get; set; } = "PLC1";

        /// <summary>
        /// 드라이버명
        /// </summary>
        public string DriverName { get; set; } = "MxComponent";

        /// <summary>
        /// 디바이스명
        /// </summary>
        public string DeviceName { get; set; } = "Q02UCPU";

        /// <summary>
        /// 태그 목록
        /// </summary>
        public List<TagConfig> Tags { get; set; } = new();
    }

    /// <summary>
    /// 태그 설정
    /// </summary>
    public class TagConfig
    {
        /// <summary>
        /// 태그명
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// OPC UA NodeId
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 데이터 타입 (Int16, Boolean, DateTime 등)
        /// </summary>
        public string DataType { get; set; } = "Int16";
    }

    /// <summary>
    /// 앱 설정
    /// </summary>
    public class AppSettings
    {
        public KepServerConfig KepServer { get; set; } = new();
        public DeviceConfig Device { get; set; } = new();
    }
}
