using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using MitsubishiOpcUaServer.Models;
using MitsubishiOpcUaServer.PlcService;
using MitsubishiOpcUaServer.OpcUaServer;

namespace MitsubishiOpcUaServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Serilog 설정
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/server-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("===========================================");
                Log.Information("Mitsubishi OPC UA Server 시작");
                Log.Information("===========================================");

                // 설정 로드
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                var appSettings = configuration.Get<AppSettings>() ?? new AppSettings();

                Log.Information("설정 로드 완료:");
                Log.Information("  - Channel: {Channel}", appSettings.Device.ChannelName);
                Log.Information("  - Device: {Device}", appSettings.Device.DeviceName);
                Log.Information("  - Logical Station: {Station}", appSettings.Device.LogicalStationNumber);
                Log.Information("  - OPC UA Port: {Port}", appSettings.OpcUaServer.Port);
                Log.Information("  - Tags: {Count}개", appSettings.Device.Tags.Count);

                // 서비스 생성
                using var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddSerilog();
                });

                var plcLogger = loggerFactory.CreateLogger<MxPlcClient>();
                var collectorLogger = loggerFactory.CreateLogger<PlcDataCollectorService>();
                var serverLogger = loggerFactory.CreateLogger<OpcUaServerHost>();

                // OPC UA 서버 시작
                using var opcUaServer = new OpcUaServerHost(
                    appSettings.Device,
                    appSettings.OpcUaServer,
                    serverLogger);

                await opcUaServer.StartAsync();

                // PLC 클라이언트 생성
                using var plcClient = new MxPlcClient(appSettings.Device, plcLogger);

                // 데이터 수집 시작
                var cts = new CancellationTokenSource();
                
                var collectorTask = Task.Run(async () =>
                {
                    // PLC 연결
                    while (!plcClient.IsConnected && !cts.Token.IsCancellationRequested)
                    {
                        Log.Information("PLC 연결 시도 중...");
                        if (plcClient.Connect())
                        {
                            Log.Information("PLC 연결 성공!");
                            break;
                        }
                        await Task.Delay(3000, cts.Token);
                    }

                    // 데이터 수집 루프
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            if (plcClient.IsConnected)
                            {
                                // 데이터 읽기
                                var deviceData = plcClient.ReadAllTags();

                                // OPC UA 노드 업데이트
                                opcUaServer.UpdateData(deviceData);

                                // 로그 출력 (10초마다)
                                if (DateTime.Now.Second % 10 == 0)
                                {
                                    Log.Debug("데이터 수집: {Data}", deviceData);
                                }
                            }
                            else
                            {
                                Log.Warning("PLC 연결 끊김. 재연결 시도...");
                                plcClient.Connect();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "데이터 수집 중 오류");
                        }

                        await Task.Delay(appSettings.Device.ScanRate, cts.Token);
                    }
                }, cts.Token);

                // 종료 대기
                Log.Information("");
                Log.Information("서버 실행 중. 종료하려면 Ctrl+C를 누르세요.");
                Log.Information("");

                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                await collectorTask;
            }
            catch (OperationCanceledException)
            {
                Log.Information("서버 종료 요청됨");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "서버 실행 중 치명적 오류 발생");
            }
            finally
            {
                Log.Information("Mitsubishi OPC UA Server 종료");
                Log.CloseAndFlush();
            }
        }
    }
}
