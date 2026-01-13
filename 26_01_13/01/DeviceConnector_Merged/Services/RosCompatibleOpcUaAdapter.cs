using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using Microsoft.Extensions.Logging;

namespace DeviceConnector.Services
{
    /// <summary>
    /// ROS_ControlHub의 IOpcUaAdapter 인터페이스와 호환되는 어댑터
    /// DeviceConnector의 OpcUaClientService를 ROS_ControlHub에서 사용할 수 있도록 변환
    /// 
    /// [사용 시나리오]
    /// 1. DeviceConnector를 독립적으로 사용하면서 ROS_ControlHub와 동일한 인터페이스 제공
    /// 2. ROS_ControlHub의 기존 코드를 수정하지 않고 DeviceConnector로 교체 가능
    /// </summary>
    public class RosCompatibleOpcUaAdapter : IDisposable
    {
        #region Private Fields

        private readonly IOpcUaClientService _opcService;
        private readonly DeviceTagConfig _deviceConfig;
        private readonly ILogger<RosCompatibleOpcUaAdapter>? _logger;
        private bool _disposed;

        #endregion

        #region Constructor

        public RosCompatibleOpcUaAdapter(
            IOpcUaClientService opcService,
            DeviceTagConfig? deviceConfig = null,
            ILogger<RosCompatibleOpcUaAdapter>? logger = null)
        {
            _opcService = opcService ?? throw new ArgumentNullException(nameof(opcService));
            _deviceConfig = deviceConfig ?? new DeviceTagConfig();
            _logger = logger;
        }

        #endregion

        #region ROS_ControlHub IOpcUaAdapter 호환 메서드

        /// <summary>
        /// 설비 상태를 읽어 Extensions 형태로 반환
        /// ROS_ControlHub의 IOpcUaAdapter.ReadStateAsync와 동일한 시그니처
        /// </summary>
        public async Task<IDictionary<string, object>> ReadStateAsync(CancellationToken ct)
        {
            var result = new Dictionary<string, object>();

            try
            {
                // DeviceConnector의 ESP32Data를 읽어와서 ROS_ControlHub 형식으로 변환
                var data = await _opcService.ReadDataAsync(_deviceConfig.DeviceId);

                if (data != null)
                {
                    // ROS_ControlHub의 OpcUaClientAdapter와 동일한 키 사용
                    result["opc.connected"] = _opcService.IsConnected;
                    result["opc.conveyor.running"] = data.Status == DeviceState.RUNNING;
                    result["opc.conveyor.speed"] = (double)data.Speed;

                    // 추가 ESP32 데이터
                    result["esp32.positionX"] = data.PositionX;
                    result["esp32.status"] = data.StatusCode;
                    result["esp32.statusName"] = data.Status.ToString();
                    result["esp32.isGoodQuality"] = data.IsGoodQuality;
                    result["esp32.timestamp"] = data.Timestamp;

                    // 디바이스 이름 및 상태
                    result["deviceName"] = $"Kepware_{_deviceConfig.ChannelName}_{_deviceConfig.DeviceName}";
                    result["deviceStatus"] = _opcService.IsConnected ? "Online" : "Offline";
                }
                else
                {
                    // 데이터 읽기 실패 시 기본값
                    result["opc.connected"] = false;
                    result["opc.conveyor.running"] = false;
                    result["opc.conveyor.speed"] = 0.0;
                    result["deviceName"] = $"Kepware_{_deviceConfig.ChannelName}_{_deviceConfig.DeviceName}";
                    result["deviceStatus"] = "Offline";
                }

                _logger?.LogDebug("상태 읽기 완료: connected={Connected}, running={Running}", 
                    result["opc.connected"], result["opc.conveyor.running"]);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "상태 읽기 실패");

                result["opc.connected"] = false;
                result["opc.conveyor.running"] = false;
                result["opc.conveyor.speed"] = 0.0;
                result["deviceName"] = $"Kepware_{_deviceConfig.ChannelName}_{_deviceConfig.DeviceName}";
                result["deviceStatus"] = "Error";
                result["error"] = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// 설비 상태 제어 (쓰기)
        /// ROS_ControlHub의 IOpcUaAdapter.WriteStateAsync와 동일한 시그니처
        /// </summary>
        public async Task WriteStateAsync(string deviceId, string stateJson)
        {
            try
            {
                _logger?.LogInformation("[OPC-UA Write] DeviceId: {DeviceId}, State: {State}", deviceId, stateJson);

                // JSON 파싱하여 적절한 태그에 쓰기
                // ROS_ControlHub는 JSON 형식으로 명령을 전달함
                // 예: {"status": "start", "payload": {}}

                var command = System.Text.Json.JsonSerializer.Deserialize<CommandPayload>(stateJson);

                if (command != null)
                {
                    // 명령에 따른 태그 쓰기
                    switch (command.Status?.ToLower())
                    {
                        case "start":
                            await _opcService.WriteCommandAsync(deviceId, DeviceTagConfig.TAG_STATUS, (ushort)DeviceState.RUNNING);
                            break;

                        case "stop":
                            await _opcService.WriteCommandAsync(deviceId, DeviceTagConfig.TAG_STATUS, (ushort)DeviceState.IDLE);
                            break;

                        default:
                            // 커스텀 명령 처리
                            if (!string.IsNullOrEmpty(command.TagName) && command.Value != null)
                            {
                                await _opcService.WriteCommandAsync(deviceId, command.TagName, command.Value);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "상태 쓰기 실패: {DeviceId}", deviceId);
                throw;
            }
        }

        #endregion

        #region 연결 관리 (편의 메서드)

        /// <summary>
        /// OPC UA 서버 연결
        /// </summary>
        public Task<bool> ConnectAsync() => _opcService.ConnectAsync();

        /// <summary>
        /// 연결 해제
        /// </summary>
        public Task DisconnectAsync() => _opcService.DisconnectAsync();

        /// <summary>
        /// 연결 상태
        /// </summary>
        public bool IsConnected => _opcService.IsConnected;

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _opcService?.Dispose();
            }

            _disposed = true;
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// ROS_ControlHub에서 전달되는 명령 JSON 구조
        /// </summary>
        private class CommandPayload
        {
            public string? Status { get; set; }
            public object? Payload { get; set; }
            
            // 확장: 직접 태그 쓰기용
            public string? TagName { get; set; }
            public object? Value { get; set; }
        }

        #endregion
    }
}
