using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitsubishiOpcUaServer.Models;

namespace MitsubishiOpcUaServer.PlcService
{
    /// <summary>
    /// PLC 데이터 주기적 수집 서비스
    /// </summary>
    public class PlcDataCollectorService : BackgroundService
    {
        private readonly ILogger<PlcDataCollectorService> _logger;
        private readonly MxPlcClient _plcClient;
        private readonly DeviceConfig _config;
        private readonly Action<DeviceData> _onDataReceived;

        public DeviceData? LatestData { get; private set; }

        public PlcDataCollectorService(
            MxPlcClient plcClient,
            DeviceConfig config,
            Action<DeviceData> onDataReceived,
            ILogger<PlcDataCollectorService> logger)
        {
            _plcClient = plcClient;
            _config = config;
            _onDataReceived = onDataReceived;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PLC 데이터 수집 서비스 시작. Scan Rate: {ScanRate}ms", _config.ScanRate);

            // PLC 연결
            while (!_plcClient.IsConnected && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("PLC 연결 시도 중...");
                if (_plcClient.Connect())
                {
                    break;
                }
                await Task.Delay(3000, stoppingToken);
            }

            // 데이터 수집 루프
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_plcClient.IsConnected)
                    {
                        // 모든 태그 읽기
                        var deviceData = _plcClient.ReadAllTags();
                        LatestData = deviceData;

                        // 콜백 호출 (OPC UA 노드 업데이트용)
                        _onDataReceived?.Invoke(deviceData);

                        if (deviceData.ErrorCode != 0)
                        {
                            _logger.LogWarning("데이터 읽기 오류. ErrorCode: {ErrorCode}", deviceData.ErrorCode);
                        }
                    }
                    else
                    {
                        // 재연결 시도
                        _logger.LogWarning("PLC 연결 끊김. 재연결 시도...");
                        _plcClient.Connect();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "데이터 수집 중 예외 발생");
                }

                await Task.Delay(_config.ScanRate, stoppingToken);
            }

            _plcClient.Disconnect();
            _logger.LogInformation("PLC 데이터 수집 서비스 종료");
        }
    }
}
