using System;
using System.Threading;
using System.Threading.Tasks;
using DeviceConnector.Contracts;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;

namespace DeviceConnector.Services
{
    /// <summary>
    /// IDeviceDataProvider 구현체
    /// OpcUaClientService를 gRPC 서비스에서 사용할 수 있도록 래핑
    /// </summary>
    public class DeviceDataProvider : IDeviceDataProvider
    {
        private readonly IOpcUaClientService _opcUaService;

        public DeviceDataProvider(IOpcUaClientService opcUaService)
        {
            _opcUaService = opcUaService ?? throw new ArgumentNullException(nameof(opcUaService));
            
            // OPC UA 데이터 변경 이벤트를 gRPC용 이벤트로 변환
            _opcUaService.DataChanged += OnOpcUaDataChanged;
        }

        /// <summary>
        /// 데이터 변경 이벤트 (gRPC 스트리밍용)
        /// </summary>
        public event EventHandler<DeviceDataChangedEventArgs>? DataChanged;

        /// <summary>
        /// 디바이스 데이터 읽기
        /// </summary>
        public async Task<DeviceDataDto?> GetDeviceDataAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            var data = await _opcUaService.ReadDataAsync(deviceId);
            if (data == null) return null;

            return ConvertToDto(deviceId, data);
        }

        /// <summary>
        /// 태그 데이터 읽기
        /// </summary>
        public async Task<TagDataDto?> GetTagDataAsync(string deviceId, string tagName, CancellationToken cancellationToken = default)
        {
            var config = new DeviceTagConfig { DeviceId = deviceId };
            var nodeId = config.GetNodeId(tagName);

            object? value = tagName switch
            {
                DeviceTagConfig.TAG_POS_X => await _opcUaService.ReadTagAsync<short>(nodeId),
                DeviceTagConfig.TAG_POS_Y => await _opcUaService.ReadTagAsync<short>(nodeId),
                DeviceTagConfig.TAG_STATE => await _opcUaService.ReadTagAsync<bool>(nodeId),
                DeviceTagConfig.TAG_TO => await _opcUaService.ReadTagAsync<bool>(nodeId),
                _ => null
            };

            if (value == null) return null;

            return new TagDataDto
            {
                DeviceId = deviceId,
                TagName = tagName,
                NodeId = nodeId,
                Value = value,
                DataType = GetTagDataType(tagName),
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 태그 쓰기
        /// </summary>
        public async Task<bool> WriteTagAsync(string deviceId, string tagName, object value, CancellationToken cancellationToken = default)
        {
            return tagName switch
            {
                DeviceTagConfig.TAG_POS_X => await _opcUaService.WritePosXAsync(Convert.ToInt16(value), deviceId),
                DeviceTagConfig.TAG_POS_Y => await _opcUaService.WritePosYAsync(Convert.ToInt16(value), deviceId),
                DeviceTagConfig.TAG_STATE => await _opcUaService.WriteStateAsync(Convert.ToBoolean(value), deviceId),
                DeviceTagConfig.TAG_TO => await _opcUaService.WriteToAsync(Convert.ToBoolean(value), deviceId),
                _ => false
            };
        }

        /// <summary>
        /// 연결 상태 조회
        /// </summary>
        public ConnectionStatusResponse GetConnectionStatus()
        {
            var status = _opcUaService.ConnectionStatus;
            return new ConnectionStatusResponse
            {
                IsConnected = _opcUaService.IsConnected,
                State = status.State.ToString(),
                EndpointUrl = status.EndpointUrl,
                ConnectedSince = status.ConnectedSince,
                ReconnectAttempts = status.ReconnectAttempts,
                LastError = status.LastError
            };
        }

        #region Private Methods

        private void OnOpcUaDataChanged(object? sender, Events.DataChangedEventArgs e)
        {
            if (e.Data == null) return;

            var dto = ConvertToDto(e.DeviceId, e.Data);
            DataChanged?.Invoke(this, new DeviceDataChangedEventArgs(e.DeviceId, dto));
        }

        private static DeviceDataDto ConvertToDto(string deviceId, ESP32Data data)
        {
            return new DeviceDataDto
            {
                DeviceId = deviceId,
                ChannelName = DeviceTagConfig.DefaultChannelName,
                PosX = data.PositionX,
                PosY = data.PositionY,
                State = data.State,
                To = data.To,
                Timestamp = data.Timestamp,
                IsGoodQuality = data.IsGoodQuality
            };
        }

        private static TagDataType GetTagDataType(string tagName)
        {
            return tagName switch
            {
                DeviceTagConfig.TAG_POS_X or DeviceTagConfig.TAG_POS_Y => TagDataType.Word,
                DeviceTagConfig.TAG_STATE or DeviceTagConfig.TAG_TO => TagDataType.Boolean,
                _ => TagDataType.Word
            };
        }

        #endregion
    }
}
