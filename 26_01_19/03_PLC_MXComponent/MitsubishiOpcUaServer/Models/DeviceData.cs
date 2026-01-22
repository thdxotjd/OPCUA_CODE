using System;
using System.Collections.Generic;

namespace MitsubishiOpcUaServer.Models
{
    /// <summary>
    /// 범용 디바이스 데이터 모델
    /// 사용 기한: 2026년 2월 28일까지
    /// </summary>
    public class DeviceData
    {
        /// <summary>
        /// 채널명 (예: "MitsubishiSerial", "MxComponent")
        /// </summary>
        public string ChannelName { get; set; } = string.Empty;

        /// <summary>
        /// 디바이스명 (예: "Q02UCPU", "PLC01")
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// 태그 데이터 (Key: 태그명, Value: 값)
        /// 예: { "D100": 1234, "M0": true, "D200": 56.78 }
        /// </summary>
        public Dictionary<string, object> Tags { get; set; } = new();

        /// <summary>
        /// 데이터 수집 시간 (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 통신 상태
        /// </summary>
        public bool IsConnected { get; set; } = false;

        /// <summary>
        /// 에러 코드 (0: 정상)
        /// </summary>
        public int ErrorCode { get; set; } = 0;

        #region 편의 메서드

        /// <summary>
        /// 태그 값 가져오기 (타입 변환)
        /// </summary>
        public T? GetTag<T>(string tagName)
        {
            if (Tags.TryGetValue(tagName, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
            return default;
        }

        /// <summary>
        /// 태그 값 설정
        /// </summary>
        public void SetTag(string tagName, object value)
        {
            Tags[tagName] = value;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// OPC UA Node ID 생성
        /// </summary>
        public string GetNodeId(string tagName)
        {
            return $"ns=2;s={ChannelName}.{DeviceName}.{tagName}";
        }

        #endregion

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] {ChannelName}.{DeviceName} - Tags: {Tags.Count}개, Connected: {IsConnected}";
        }
    }
}
