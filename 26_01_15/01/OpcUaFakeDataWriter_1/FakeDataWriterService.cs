using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ROS_ControlHub.TestData
{
    /// <summary>
    /// OPC UA 연결된 상태에서 가짜 데이터를 쓰는 서비스
    /// gRPC 개발자 테스트용
    /// 
    /// 데이터 구조 규칙 (2026-02-28까지 유지):
    /// - DeviceName: 장비 이름
    /// - ChannelName: KEPServerEX 채널명
    /// - Tags: 장비별 태그 딕셔너리
    /// </summary>
    public class FakeDataWriterService : IDisposable
    {
        private readonly Session _session;
        private readonly string _opcUaEndpoint;
        private readonly Dictionary<string, DeviceConfig> _deviceConfigs;
        private readonly Random _random = new Random();
        private CancellationTokenSource? _autoUpdateCts;

        /// <summary>
        /// 생성자 - OPC UA 세션 직접 전달
        /// </summary>
        public FakeDataWriterService(Session session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _opcUaEndpoint = session.Endpoint.EndpointUrl;
            _deviceConfigs = InitializeDeviceConfigs();
        }

        /// <summary>
        /// 생성자 - 엔드포인트 URL로 새 연결
        /// </summary>
        public FakeDataWriterService(string opcUaEndpoint)
        {
            _opcUaEndpoint = opcUaEndpoint;
            _deviceConfigs = InitializeDeviceConfigs();
            _session = ConnectAsync().GetAwaiter().GetResult();
        }

        #region 장비 설정 초기화

        /// <summary>
        /// 장비별 태그 설정 초기화
        /// KEPServerEX Node ID 형식: ns=2;s={ChannelName}.{DeviceName}.{TagName}
        /// </summary>
        private Dictionary<string, DeviceConfig> InitializeDeviceConfigs()
        {
            return new Dictionary<string, DeviceConfig>
            {
                // ESP32 장비
                ["ESP32_01"] = new DeviceConfig
                {
                    DeviceName = "ESP32_01",
                    ChannelName = "ModbusTCP",
                    Tags = new Dictionary<string, TagConfig>
                    {
                        ["Connected"] = new TagConfig { DataType = typeof(bool), DefaultValue = true },
                        ["Running"] = new TagConfig { DataType = typeof(bool), DefaultValue = false },
                        ["Speed"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0, MinValue = 0, MaxValue = 200 },
                        ["PositionX"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = -100, MaxValue = 100 },
                        ["PositionY"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = -100, MaxValue = 100 },
                        ["PositionZ"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = 0, MaxValue = 50 },
                        ["Temperature"] = new TagConfig { DataType = typeof(float), DefaultValue = 25.0f, MinValue = 20, MaxValue = 80 },
                        ["ErrorCode"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0 },
                        ["Status"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0 }
                    }
                },

                // PLC 장비 (Mitsubishi Q02UCPU)
                ["PLC_01"] = new DeviceConfig
                {
                    DeviceName = "PLC_01",
                    ChannelName = "MitsubishiSerial",
                    Tags = new Dictionary<string, TagConfig>
                    {
                        ["D100"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0, MinValue = 0, MaxValue = 9999 },
                        ["D101"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0, MinValue = 0, MaxValue = 9999 },
                        ["D102"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0, MinValue = 0, MaxValue = 9999 },
                        ["D200"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0 },
                        ["M0"] = new TagConfig { DataType = typeof(bool), DefaultValue = false },
                        ["M1"] = new TagConfig { DataType = typeof(bool), DefaultValue = false },
                        ["M100"] = new TagConfig { DataType = typeof(bool), DefaultValue = false },
                        ["Y0"] = new TagConfig { DataType = typeof(bool), DefaultValue = false },
                        ["Y1"] = new TagConfig { DataType = typeof(bool), DefaultValue = false }
                    }
                },

                // AGV 장비
                ["AGV_01"] = new DeviceConfig
                {
                    DeviceName = "AGV_01",
                    ChannelName = "ModbusTCP",
                    Tags = new Dictionary<string, TagConfig>
                    {
                        ["Connected"] = new TagConfig { DataType = typeof(bool), DefaultValue = true },
                        ["Running"] = new TagConfig { DataType = typeof(bool), DefaultValue = false },
                        ["BatteryLevel"] = new TagConfig { DataType = typeof(float), DefaultValue = 100.0f, MinValue = 0, MaxValue = 100 },
                        ["CurrentX"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = -500, MaxValue = 500 },
                        ["CurrentY"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = -500, MaxValue = 500 },
                        ["TargetX"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f },
                        ["TargetY"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f },
                        ["Speed"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = 0, MaxValue = 50 },
                        ["Status"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0 }, // 0=Idle, 1=Moving, 2=Charging, 3=Error
                        ["ErrorCode"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0 }
                    }
                },

                // 로봇 암
                ["ROBOT_01"] = new DeviceConfig
                {
                    DeviceName = "ROBOT_01",
                    ChannelName = "RobotController",
                    Tags = new Dictionary<string, TagConfig>
                    {
                        ["Connected"] = new TagConfig { DataType = typeof(bool), DefaultValue = true },
                        ["Running"] = new TagConfig { DataType = typeof(bool), DefaultValue = false },
                        ["Joint1"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = -180, MaxValue = 180 },
                        ["Joint2"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = -180, MaxValue = 180 },
                        ["Joint3"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = -180, MaxValue = 180 },
                        ["Joint4"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = -180, MaxValue = 180 },
                        ["Joint5"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = -180, MaxValue = 180 },
                        ["Joint6"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f, MinValue = -180, MaxValue = 180 },
                        ["GripperState"] = new TagConfig { DataType = typeof(bool), DefaultValue = false },
                        ["ProgramRunning"] = new TagConfig { DataType = typeof(bool), DefaultValue = false },
                        ["ErrorCode"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0 }
                    }
                }
            };
        }

        #endregion

        #region OPC UA 연결

        /// <summary>
        /// OPC UA 서버에 연결
        /// </summary>
        private async Task<Session> ConnectAsync()
        {
            var config = new ApplicationConfiguration
            {
                ApplicationName = "FakeDataWriter",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    AutoAcceptUntrustedCertificates = true
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = 60000
                }
            };

            await config.Validate(ApplicationType.Client);

            var endpoint = CoreClientUtils.SelectEndpoint(_opcUaEndpoint, false);
            var session = await Session.Create(
                config,
                new ConfiguredEndpoint(null, endpoint),
                false,
                "FakeDataWriterSession",
                60000,
                new UserIdentity(),
                null);

            Console.WriteLine($"[FakeDataWriter] Connected to {_opcUaEndpoint}");
            return session;
        }

        #endregion

        #region 단일 태그 쓰기

        /// <summary>
        /// OPC UA Node ID 생성
        /// KEPServerEX 형식: ns=2;s={ChannelName}.{DeviceName}.{TagName}
        /// </summary>
        private string GetNodeId(string deviceName, string tagName)
        {
            if (!_deviceConfigs.TryGetValue(deviceName, out var config))
                throw new ArgumentException($"Unknown device: {deviceName}");

            return $"ns=2;s={config.ChannelName}.{deviceName}.{tagName}";
        }

        /// <summary>
        /// 단일 태그 값 쓰기
        /// </summary>
        public async Task<WriteResult> WriteTagAsync(string deviceName, string tagName, object value)
        {
            try
            {
                var nodeId = GetNodeId(deviceName, tagName);
                var writeValue = new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                };

                var writeValues = new WriteValueCollection { writeValue };
                var response = await _session.WriteAsync(
                    null,
                    writeValues,
                    CancellationToken.None);

                var success = StatusCode.IsGood(response.Results[0]);
                
                Console.WriteLine($"[FakeDataWriter] {deviceName}.{tagName} = {value} ({(success ? "OK" : "FAIL")})");

                return new WriteResult
                {
                    Success = success,
                    Message = success ? "Written successfully" : $"Failed: {response.Results[0]}"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FakeDataWriter] Error writing {deviceName}.{tagName}: {ex.Message}");
                return new WriteResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 여러 태그 값 한번에 쓰기
        /// </summary>
        public async Task<WriteResult> WriteTagsAsync(string deviceName, Dictionary<string, object> tags)
        {
            try
            {
                var writeValues = new WriteValueCollection();

                foreach (var tag in tags)
                {
                    var nodeId = GetNodeId(deviceName, tag.Key);
                    writeValues.Add(new WriteValue
                    {
                        NodeId = nodeId,
                        AttributeId = Attributes.Value,
                        Value = new DataValue(new Variant(tag.Value))
                    });
                }

                var response = await _session.WriteAsync(null, writeValues, CancellationToken.None);

                var allSuccess = true;
                for (int i = 0; i < response.Results.Count; i++)
                {
                    if (!StatusCode.IsGood(response.Results[i]))
                    {
                        allSuccess = false;
                        Console.WriteLine($"[FakeDataWriter] Failed to write tag {i}: {response.Results[i]}");
                    }
                }

                Console.WriteLine($"[FakeDataWriter] {deviceName}: {tags.Count} tags written ({(allSuccess ? "OK" : "PARTIAL")})");

                return new WriteResult
                {
                    Success = allSuccess,
                    Message = allSuccess ? $"{tags.Count} tags written" : "Some tags failed"
                };
            }
            catch (Exception ex)
            {
                return new WriteResult { Success = false, Message = ex.Message };
            }
        }

        #endregion

        #region 가짜 데이터 생성 및 쓰기

        /// <summary>
        /// 모든 장비에 초기값 쓰기
        /// </summary>
        public async Task WriteInitialValuesAsync()
        {
            Console.WriteLine("[FakeDataWriter] Writing initial values to all devices...");

            foreach (var device in _deviceConfigs)
            {
                var tags = new Dictionary<string, object>();
                foreach (var tag in device.Value.Tags)
                {
                    tags[tag.Key] = tag.Value.DefaultValue;
                }
                await WriteTagsAsync(device.Key, tags);
            }

            Console.WriteLine("[FakeDataWriter] Initial values written.");
        }

        /// <summary>
        /// 특정 장비에 랜덤 가짜 데이터 쓰기
        /// </summary>
        public async Task WriteRandomDataAsync(string deviceName)
        {
            if (!_deviceConfigs.TryGetValue(deviceName, out var config))
            {
                Console.WriteLine($"[FakeDataWriter] Unknown device: {deviceName}");
                return;
            }

            var tags = new Dictionary<string, object>();

            foreach (var tag in config.Tags)
            {
                tags[tag.Key] = GenerateRandomValue(tag.Value);
            }

            await WriteTagsAsync(deviceName, tags);
        }

        /// <summary>
        /// 모든 장비에 랜덤 가짜 데이터 쓰기
        /// </summary>
        public async Task WriteRandomDataToAllAsync()
        {
            foreach (var device in _deviceConfigs.Keys)
            {
                await WriteRandomDataAsync(device);
            }
        }

        /// <summary>
        /// 랜덤 값 생성
        /// </summary>
        private object GenerateRandomValue(TagConfig config)
        {
            if (config.DataType == typeof(bool))
            {
                return _random.NextDouble() > 0.5;
            }
            else if (config.DataType == typeof(short))
            {
                var min = (int)(config.MinValue ?? 0);
                var max = (int)(config.MaxValue ?? 100);
                return (short)_random.Next(min, max + 1);
            }
            else if (config.DataType == typeof(int))
            {
                var min = (int)(config.MinValue ?? 0);
                var max = (int)(config.MaxValue ?? 1000);
                return _random.Next(min, max + 1);
            }
            else if (config.DataType == typeof(float))
            {
                var min = config.MinValue ?? 0;
                var max = config.MaxValue ?? 100;
                return (float)(min + _random.NextDouble() * (max - min));
            }
            else if (config.DataType == typeof(double))
            {
                var min = config.MinValue ?? 0;
                var max = config.MaxValue ?? 100;
                return min + _random.NextDouble() * (max - min);
            }

            return config.DefaultValue;
        }

        #endregion

        #region 자동 업데이트 (시뮬레이션)

        /// <summary>
        /// 자동 가짜 데이터 업데이트 시작
        /// </summary>
        public void StartAutoUpdate(int intervalMs = 1000)
        {
            StopAutoUpdate();
            _autoUpdateCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                Console.WriteLine($"[FakeDataWriter] Auto update started (interval: {intervalMs}ms)");

                while (!_autoUpdateCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await UpdateSimulationDataAsync();
                        await Task.Delay(intervalMs, _autoUpdateCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FakeDataWriter] Auto update error: {ex.Message}");
                    }
                }

                Console.WriteLine("[FakeDataWriter] Auto update stopped");
            });
        }

        /// <summary>
        /// 자동 업데이트 중지
        /// </summary>
        public void StopAutoUpdate()
        {
            _autoUpdateCts?.Cancel();
            _autoUpdateCts?.Dispose();
            _autoUpdateCts = null;
        }

        /// <summary>
        /// 시뮬레이션 데이터 업데이트 (현실적인 값 변화)
        /// </summary>
        private async Task UpdateSimulationDataAsync()
        {
            // ESP32: Running 상태면 Speed, Position, Temperature 변화
            if (_deviceConfigs.TryGetValue("ESP32_01", out var esp32))
            {
                var tags = new Dictionary<string, object>
                {
                    ["Speed"] = (short)_random.Next(80, 120),
                    ["Temperature"] = (float)(25.0 + _random.NextDouble() * 10),
                    ["PositionX"] = (float)(_random.NextDouble() * 50),
                    ["PositionY"] = (float)(_random.NextDouble() * 50)
                };
                await WriteTagsAsync("ESP32_01", tags);
            }

            // PLC: D레지스터 값 변화
            if (_deviceConfigs.TryGetValue("PLC_01", out var plc))
            {
                var tags = new Dictionary<string, object>
                {
                    ["D100"] = (short)_random.Next(0, 1000),
                    ["D101"] = (short)_random.Next(0, 500),
                    ["M0"] = _random.NextDouble() > 0.5
                };
                await WriteTagsAsync("PLC_01", tags);
            }

            // AGV: 배터리 감소, 위치 변화
            if (_deviceConfigs.TryGetValue("AGV_01", out var agv))
            {
                var tags = new Dictionary<string, object>
                {
                    ["BatteryLevel"] = (float)Math.Max(0, 100 - _random.NextDouble() * 5),
                    ["CurrentX"] = (float)(_random.NextDouble() * 100),
                    ["CurrentY"] = (float)(_random.NextDouble() * 100),
                    ["Speed"] = (float)(_random.NextDouble() * 10)
                };
                await WriteTagsAsync("AGV_01", tags);
            }

            // Robot: 조인트 각도 변화
            if (_deviceConfigs.TryGetValue("ROBOT_01", out var robot))
            {
                var tags = new Dictionary<string, object>
                {
                    ["Joint1"] = (float)(_random.NextDouble() * 90 - 45),
                    ["Joint2"] = (float)(_random.NextDouble() * 90 - 45),
                    ["Joint3"] = (float)(_random.NextDouble() * 90 - 45)
                };
                await WriteTagsAsync("ROBOT_01", tags);
            }
        }

        #endregion

        #region 특정 시나리오 테스트

        /// <summary>
        /// ESP32 시작 시나리오
        /// </summary>
        public async Task SimulateEsp32StartAsync()
        {
            Console.WriteLine("[Scenario] ESP32 Start");
            await WriteTagsAsync("ESP32_01", new Dictionary<string, object>
            {
                ["Connected"] = true,
                ["Running"] = true,
                ["Speed"] = (short)100,
                ["ErrorCode"] = (short)0,
                ["Status"] = (short)1  // Running
            });
        }

        /// <summary>
        /// ESP32 정지 시나리오
        /// </summary>
        public async Task SimulateEsp32StopAsync()
        {
            Console.WriteLine("[Scenario] ESP32 Stop");
            await WriteTagsAsync("ESP32_01", new Dictionary<string, object>
            {
                ["Running"] = false,
                ["Speed"] = (short)0,
                ["Status"] = (short)0  // Idle
            });
        }

        /// <summary>
        /// ESP32 에러 시나리오
        /// </summary>
        public async Task SimulateEsp32ErrorAsync(short errorCode = 101)
        {
            Console.WriteLine($"[Scenario] ESP32 Error ({errorCode})");
            await WriteTagsAsync("ESP32_01", new Dictionary<string, object>
            {
                ["Running"] = false,
                ["Speed"] = (short)0,
                ["ErrorCode"] = errorCode,
                ["Status"] = (short)3  // Error
            });
        }

        /// <summary>
        /// AGV 이동 시나리오
        /// </summary>
        public async Task SimulateAgvMoveAsync(float targetX, float targetY)
        {
            Console.WriteLine($"[Scenario] AGV Move to ({targetX}, {targetY})");
            await WriteTagsAsync("AGV_01", new Dictionary<string, object>
            {
                ["Running"] = true,
                ["TargetX"] = targetX,
                ["TargetY"] = targetY,
                ["Status"] = (short)1,  // Moving
                ["Speed"] = 5.0f
            });

            // 이동 시뮬레이션 (5단계로 목표 지점까지)
            for (int i = 1; i <= 5; i++)
            {
                await Task.Delay(500);
                await WriteTagsAsync("AGV_01", new Dictionary<string, object>
                {
                    ["CurrentX"] = targetX * i / 5,
                    ["CurrentY"] = targetY * i / 5,
                    ["BatteryLevel"] = (float)(95 - i)
                });
            }

            // 도착
            await WriteTagsAsync("AGV_01", new Dictionary<string, object>
            {
                ["Running"] = false,
                ["CurrentX"] = targetX,
                ["CurrentY"] = targetY,
                ["Status"] = (short)0,  // Idle
                ["Speed"] = 0.0f
            });
            Console.WriteLine("[Scenario] AGV Arrived");
        }

        /// <summary>
        /// PLC 생산 카운트 시나리오
        /// </summary>
        public async Task SimulatePlcProductionAsync(int count = 10)
        {
            Console.WriteLine($"[Scenario] PLC Production Count: {count}");
            
            for (int i = 1; i <= count; i++)
            {
                await WriteTagsAsync("PLC_01", new Dictionary<string, object>
                {
                    ["D100"] = (short)i,           // 생산 카운트
                    ["D101"] = (short)(i * 10),    // 누적 시간
                    ["M0"] = true,                  // 동작 중
                    ["Y0"] = i % 2 == 0            // 출력 토글
                });
                await Task.Delay(200);
            }

            await WriteTagAsync("PLC_01", "M0", false);
            Console.WriteLine("[Scenario] Production Complete");
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            StopAutoUpdate();
            _session?.Close();
            _session?.Dispose();
        }

        #endregion
    }

    #region 설정 클래스

    /// <summary>
    /// 장비 설정
    /// </summary>
    public class DeviceConfig
    {
        public string DeviceName { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
        public Dictionary<string, TagConfig> Tags { get; set; } = new();
    }

    /// <summary>
    /// 태그 설정
    /// </summary>
    public class TagConfig
    {
        public Type DataType { get; set; } = typeof(object);
        public object DefaultValue { get; set; } = null!;
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
    }

    /// <summary>
    /// 쓰기 결과
    /// </summary>
    public class WriteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    #endregion
}
