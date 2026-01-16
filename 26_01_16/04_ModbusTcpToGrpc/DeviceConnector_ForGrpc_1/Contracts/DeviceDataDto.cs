using System;

namespace DeviceConnector.Contracts
{
    /// <summary>
    /// gRPC 통신용 디바이스 데이터 DTO
    /// KEPServerEX 태그 매핑:
    /// - POS_X: 40001 (Word)
    /// - POS_Y: 40002 (Word)
    /// - State: 40003.0 (Boolean)
    /// - To: 40004.0 (Boolean)
    /// </summary>
    public class DeviceDataDto
    {
        /// <summary>디바이스 ID (예: "ESP32_01")</summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>채널명 (예: "ModbusTCP")</summary>
        public string ChannelName { get; set; } = string.Empty;

        /// <summary>X축 위치 (Word, 40001)</summary>
        public short PosX { get; set; }

        /// <summary>Y축 위치 (Word, 40002)</summary>
        public short PosY { get; set; }

        /// <summary>상태 (Boolean, 40003.0)</summary>
        public bool State { get; set; }

        /// <summary>목표 도달 여부 (Boolean, 40004.0)</summary>
        public bool To { get; set; }

        /// <summary>타임스탬프 (UTC)</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>데이터 품질 (OPC UA Quality)</summary>
        public bool IsGoodQuality { get; set; }
    }

    /// <summary>
    /// 태그별 데이터 DTO (개별 태그 읽기/쓰기용)
    /// </summary>
    public class TagDataDto
    {
        /// <summary>디바이스 ID</summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>태그명 (POS_X, POS_Y, State, To)</summary>
        public string TagName { get; set; } = string.Empty;

        /// <summary>OPC UA 노드 ID (예: ns=2;s=ModbusTCP.ESP32_01.POS_X)</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>값 (object로 전달, 실제 타입은 TagDataType 참조)</summary>
        public object? Value { get; set; }

        /// <summary>데이터 타입</summary>
        public TagDataType DataType { get; set; }

        /// <summary>타임스탬프</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 태그 데이터 타입
    /// </summary>
    public enum TagDataType
    {
        /// <summary>Word (short, 16-bit signed)</summary>
        Word = 0,

        /// <summary>Boolean</summary>
        Boolean = 1
    }
}
