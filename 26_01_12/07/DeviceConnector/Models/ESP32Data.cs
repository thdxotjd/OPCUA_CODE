using System;

namespace DeviceConnector.Models
{
    /// <summary>
    /// ESP32 디바이스에서 수집되는 데이터
    /// 
    /// KEPServerEX Modbus TCP 태그 매핑:
    /// - ModbusTCP.ESP32_01.REG_POS_X_LOW  (40001) : Position X 하위 16비트
    /// - ModbusTCP.ESP32_01.REG_POS_X_HIGH (40002) : Position X 상위 16비트
    /// - ModbusTCP.ESP32_01.REG_SPEED      (40007) : 속도
    /// - ModbusTCP.ESP32_01.REG_STATUS     (40008) : 상태
    /// </summary>
    public class ESP32Data
    {
        /// <summary>
        /// X축 위치값 (mm)
        /// 2개의 16비트 레지스터를 조합한 float 값
        /// </summary>
        public float PositionX { get; set; }

        /// <summary>
        /// 속도값 (mm/s)
        /// </summary>
        public ushort Speed { get; set; }

        /// <summary>
        /// 상태 코드 (raw value)
        /// </summary>
        public ushort StatusCode { get; set; }

        /// <summary>
        /// 상태 코드를 Enum으로 변환
        /// </summary>
        public DeviceState Status => (DeviceState)StatusCode;

        /// <summary>
        /// 데이터 수집 시간 (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// OPC UA 데이터 품질 (Good = true)
        /// </summary>
        public bool IsGoodQuality { get; set; }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] PosX={PositionX:F2}mm, Speed={Speed}mm/s, Status={Status}";
        }
    }

    /// <summary>
    /// 디바이스 동작 상태 (ESP32 펌웨어와 동일)
    /// </summary>
    public enum DeviceState
    {
        IDLE = 0,
        RUNNING = 1,
        ARRIVED = 2,
        UNKNOWN = 99
    }
}
