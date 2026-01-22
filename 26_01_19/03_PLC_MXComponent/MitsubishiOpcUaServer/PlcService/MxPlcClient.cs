using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MitsubishiOpcUaServer.Models;
using Microsoft.Extensions.Logging;

namespace MitsubishiOpcUaServer.PlcService
{
    /// <summary>
    /// MX Component를 사용한 Mitsubishi PLC 통신 클라이언트
    /// </summary>
    public class MxPlcClient : IDisposable
    {
        private readonly ILogger<MxPlcClient> _logger;
        private readonly DeviceConfig _config;
        private dynamic? _actUtlType;
        private bool _isConnected;
        private bool _disposed;

        public bool IsConnected => _isConnected;

        public MxPlcClient(DeviceConfig config, ILogger<MxPlcClient> logger)
        {
            _config = config;
            _logger = logger;
            _isConnected = false;
        }

        /// <summary>
        /// PLC 연결
        /// </summary>
        public bool Connect()
        {
            try
            {
                // ActUtlType COM 객체 생성
                var actType = Type.GetTypeFromProgID("ActUtlType.ActUtlType");
                if (actType == null)
                {
                    _logger.LogError("ActUtlType COM 객체를 찾을 수 없습니다. MX Component가 설치되어 있는지 확인하세요.");
                    return false;
                }

                _actUtlType = Activator.CreateInstance(actType);
                if (_actUtlType == null)
                {
                    _logger.LogError("ActUtlType 인스턴스 생성 실패");
                    return false;
                }

                // Logical Station Number 설정
                _actUtlType.ActLogicalStationNumber = _config.LogicalStationNumber;

                // PLC 연결
                int result = _actUtlType.Open();
                if (result != 0)
                {
                    _logger.LogError("PLC 연결 실패. Error Code: 0x{ErrorCode:X8}", result);
                    return false;
                }

                _isConnected = true;
                _logger.LogInformation("PLC 연결 성공. Logical Station: {Station}, Device: {Device}", 
                    _config.LogicalStationNumber, _config.DeviceName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PLC 연결 중 예외 발생");
                return false;
            }
        }

        /// <summary>
        /// PLC 연결 해제
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (_actUtlType != null && _isConnected)
                {
                    _actUtlType.Close();
                    _isConnected = false;
                    _logger.LogInformation("PLC 연결 해제됨");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PLC 연결 해제 중 예외 발생");
            }
        }

        /// <summary>
        /// 모든 태그 읽기 (DeviceData 구조 반환)
        /// </summary>
        public DeviceData ReadAllTags()
        {
            var deviceData = new DeviceData
            {
                ChannelName = _config.ChannelName,
                DeviceName = _config.DeviceName,
                IsConnected = _isConnected,
                Timestamp = DateTime.UtcNow
            };

            if (!_isConnected || _actUtlType == null)
            {
                deviceData.ErrorCode = -1;
                return deviceData;
            }

            try
            {
                foreach (var tag in _config.Tags)
                {
                    string alias = tag.Key;      // 별칭 (예: "Temperature")
                    string address = tag.Value;  // PLC 주소 (예: "D100")

                    object? value = ReadDevice(address);
                    if (value != null)
                    {
                        deviceData.Tags[alias] = value;
                    }
                }

                deviceData.ErrorCode = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "태그 읽기 중 예외 발생");
                deviceData.ErrorCode = -2;
            }

            return deviceData;
        }

        /// <summary>
        /// 단일 디바이스 읽기
        /// </summary>
        public object? ReadDevice(string address)
        {
            if (!_isConnected || _actUtlType == null)
                return null;

            try
            {
                // 주소 파싱 (D100, M0, X0, Y0 등)
                string deviceType = GetDeviceType(address);

                // GetDevice2 메서드 사용 (반환값으로 데이터 받음)
                object[] args = new object[] { address, 0 };
                int result = _actUtlType.GetType().InvokeMember(
                    "GetDevice2",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    _actUtlType,
                    args);

                if (result == 0)
                {
                    int value = Convert.ToInt32(args[1]);
                    
                    // 비트 디바이스는 Boolean으로 변환
                    if (IsBitDevice(deviceType))
                    {
                        return value != 0;
                    }
                    return value;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "디바이스 읽기 실패: {Address}", address);
                return null;
            }
        }

        /// <summary>
        /// 32비트 정수 읽기 (2워드)
        /// </summary>
        public int? ReadDevice32(string address)
        {
            if (!_isConnected || _actUtlType == null)
                return null;

            try
            {
                int[] data = new int[1];
                object[] args = new object[] { address, 2, data };
                int result = (int)_actUtlType.GetType().InvokeMember(
                    "ReadDeviceBlock2",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    _actUtlType,
                    args);

                if (result == 0)
                {
                    return ((int[])args[2])[0];
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "32비트 디바이스 읽기 실패: {Address}", address);
                return null;
            }
        }

        /// <summary>
        /// 실수 읽기 (FLOAT, 2워드)
        /// </summary>
        public float? ReadDeviceFloat(string address)
        {
            if (!_isConnected || _actUtlType == null)
                return null;

            try
            {
                short[] data = new short[2];
                object[] args = new object[] { address, 2, data };
                int result = (int)_actUtlType.GetType().InvokeMember(
                    "ReadDeviceBlock",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    _actUtlType,
                    args);

                if (result == 0)
                {
                    short[] values = (short[])args[2];
                    byte[] bytes = new byte[4];
                    Buffer.BlockCopy(values, 0, bytes, 0, 4);
                    return BitConverter.ToSingle(bytes, 0);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "실수 디바이스 읽기 실패: {Address}", address);
                return null;
            }
        }

        /// <summary>
        /// 단일 디바이스 쓰기
        /// </summary>
        public bool WriteDevice(string address, object value)
        {
            if (!_isConnected || _actUtlType == null)
                return false;

            try
            {
                string deviceType = GetDeviceType(address);
                int writeValue;

                // 비트 디바이스
                if (IsBitDevice(deviceType))
                {
                    writeValue = Convert.ToBoolean(value) ? 1 : 0;
                }
                // 워드 디바이스
                else
                {
                    writeValue = Convert.ToInt32(value);
                }

                object[] args = new object[] { address, writeValue };
                int result = (int)_actUtlType.GetType().InvokeMember(
                    "SetDevice2",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    _actUtlType,
                    args);

                return result == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "디바이스 쓰기 실패: {Address} = {Value}", address, value);
                return false;
            }
        }

        /// <summary>
        /// 태그 별칭으로 쓰기
        /// </summary>
        public bool WriteTag(string tagAlias, object value)
        {
            if (_config.Tags.TryGetValue(tagAlias, out string? address))
            {
                return WriteDevice(address, value);
            }

            _logger.LogWarning("알 수 없는 태그 별칭: {TagAlias}", tagAlias);
            return false;
        }

        #region Helper Methods

        private string GetDeviceType(string address)
        {
            // D100 -> D, M0 -> M, X10 -> X
            int i = 0;
            while (i < address.Length && !char.IsDigit(address[i]))
            {
                i++;
            }
            return address.Substring(0, i).ToUpper();
        }

        private bool IsBitDevice(string deviceType)
        {
            // 비트 디바이스: M, X, Y, B, L, F, V, SM, SB
            return deviceType switch
            {
                "M" or "X" or "Y" or "B" or "L" or "F" or "V" or "SM" or "SB" => true,
                _ => false
            };
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                if (_actUtlType != null)
                {
                    Marshal.ReleaseComObject(_actUtlType);
                    _actUtlType = null;
                }
                _disposed = true;
            }
        }
    }
}
