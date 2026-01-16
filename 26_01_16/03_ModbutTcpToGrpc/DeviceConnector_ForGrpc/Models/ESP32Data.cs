using System;

namespace DeviceConnector.Models
{
    /// <summary>
    /// ESP32 디바이스에서 수집되는 데이터 모델
    /// KEPServerEX Modbus TCP 태그 매핑:
    /// - POS_X (40001): Word - X축 위치값
    /// - POS_Y (40002): Word - Y축 위치값
    /// - State (40003.0): Boolean - 상태
    /// - To (40004.0): Boolean - 목표 도달 여부
    /// </summary>
    public class ESP32Data
    {
        /// <summary>
        /// X축 위치값
        /// Modbus 레지스터 40001 (Word)
        /// </summary>
        public short PositionX { get; set; }

        /// <summary>
        /// Y축 위치값
        /// Modbus 레지스터 40002 (Word)
        /// </summary>
        public short PositionY { get; set; }

        /// <summary>
        /// 디바이스 상태
        /// Modbus 레지스터 40003.0 (Boolean)
        /// </summary>
        public bool State { get; set; }

        /// <summary>
        /// 목표 도달 여부
        /// Modbus 레지스터 40004.0 (Boolean)
        /// </summary>
        public bool To { get; set; }

        /// <summary>
        /// 데이터 수집 시간 (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 데이터 품질 (OPC UA Quality)
        /// </summary>
        public bool IsGoodQuality { get; set; }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] POS_X={PositionX}, POS_Y={PositionY}, State={State}, To={To}";
        }
    }
}
