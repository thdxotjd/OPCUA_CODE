using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua.Security.Certificates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace ROS_ControlHub.TestData
{
    public class FakeDataWriterService : IDisposable
    {
        private readonly Session _session;
        private readonly string _opcUaEndpoint;
        private readonly Dictionary<string, DeviceConfig> _deviceConfigs;
        private readonly Random _random = new Random();
        private CancellationTokenSource? _autoUpdateCts;

        public FakeDataWriterService(Session session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _opcUaEndpoint = session.Endpoint.EndpointUrl;
            _deviceConfigs = InitializeDeviceConfigs();
        }

        public FakeDataWriterService(string opcUaEndpoint)
        {
            _opcUaEndpoint = opcUaEndpoint;
            _deviceConfigs = InitializeDeviceConfigs();
            _session = ConnectAsync().GetAwaiter().GetResult();
        }

        private Dictionary<string, DeviceConfig> InitializeDeviceConfigs()
        {
            return new Dictionary<string, DeviceConfig>
            {
                ["ESP32_01"] = new DeviceConfig
                {
                    DeviceName = "ESP32_01",
                    ChannelName = "ModbusTCP",
                    Tags = new Dictionary<string, TagConfig>
                    {
                        ["Connected"] = new TagConfig { DataType = typeof(bool), DefaultValue = true },
                        ["Running"] = new TagConfig { DataType = typeof(bool), DefaultValue = false },
                        ["Speed"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0, MinValue = 0, MaxValue = 200 },
                        ["PositionX"] = new TagConfig { DataType = typeof(float), DefaultValue = 0.0f },
                        ["Temperature"] = new TagConfig { DataType = typeof(float), DefaultValue = 25.0f },
                        ["ErrorCode"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0 },
                        ["Status"] = new TagConfig { DataType = typeof(short), DefaultValue = (short)0 }
                    }
                }
            };
        }

        private async Task<Session> ConnectAsync()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FakeDataWriter", "pki");

            var config = new ApplicationConfiguration
            {
                ApplicationName = "FakeDataWriter",
                ApplicationUri = "urn:FakeDataWriter",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(appDataPath, "own"),
                        SubjectName = "CN=FakeDataWriter, O=Test, C=KR"
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 }
            };

            await config.Validate(ApplicationType.Client);
            var certId = config.SecurityConfiguration.ApplicationCertificate;

            // 1. 기존 인증서 로드 시도
            X509Certificate2 existingCert = await certId.Find(true);

            if (existingCert == null)
            {
                Console.WriteLine("[FakeDataWriter] Creating application certificate...");
                existingCert = CertificateFactory.CreateCertificate(
                    certId.StoreType,
                    certId.StorePath,
                    null,
                    config.ApplicationUri,
                    config.ApplicationName,
                    certId.SubjectName,
                    null,
                    2048,
                    DateTime.UtcNow.AddDays(-1),
                    12,
                    256,
                    false,
                    null,
                    null);

                // 2. 저장소에 저장 (중복 에러 방지를 위해 Try-Catch 또는 존재 확인)
                using (ICertificateStore store = certId.OpenStore())
                {
                    try
                    {
                        // 이미 같은 지문이 있는지 다시 한 번 확인
                        var check = await store.FindByThumbprint(existingCert.Thumbprint);
                        if (check.Count == 0)
                        {
                            await store.Add(existingCert);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FakeDataWriter] Certificate store note: {ex.Message}");
                    }
                }
            }

            // 3. 비공개 키 로드 (기존에 있다면 바로 사용)
            await certId.LoadPrivateKeyEx(null);
            config.CertificateValidator = new CertificateValidator();
            config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = true; };

            var endpoint = CoreClientUtils.SelectEndpoint(_opcUaEndpoint, false);
            return await Session.Create(config, new ConfiguredEndpoint(null, endpoint), false, "FakeDataWriterSession", 60000, new UserIdentity(), null);
        }

        #region 데이터 쓰기 및 시뮬레이션 메서드 (기존과 동일)

        public async Task<WriteResult> WriteTagAsync(string deviceName, string tagName, object value)
        {
            try
            {
                if (!_deviceConfigs.TryGetValue(deviceName, out var config)) throw new Exception("Unknown device");
                var nodeId = $"ns=2;s={config.ChannelName}.{deviceName}.{tagName}";
                var writeValue = new WriteValue { NodeId = nodeId, AttributeId = Attributes.Value, Value = new DataValue(new Variant(value)) };
                var response = await _session.WriteAsync(null, new WriteValueCollection { writeValue }, CancellationToken.None);
                return new WriteResult { Success = StatusCode.IsGood(response.Results[0]), Message = response.Results[0].ToString() };
            }
            catch (Exception ex) { return new WriteResult { Success = false, Message = ex.Message }; }
        }

        public async Task<WriteResult> WriteTagsAsync(string deviceName, Dictionary<string, object> tags)
        {
            try
            {
                if (!_deviceConfigs.TryGetValue(deviceName, out var config)) throw new Exception("Unknown device");
                var writeValues = new WriteValueCollection();
                foreach (var tag in tags)
                {
                    writeValues.Add(new WriteValue { NodeId = $"ns=2;s={config.ChannelName}.{deviceName}.{tag.Key}", AttributeId = Attributes.Value, Value = new DataValue(new Variant(tag.Value)) });
                }
                var response = await _session.WriteAsync(null, writeValues, CancellationToken.None);
                return new WriteResult { Success = response.Results.All(StatusCode.IsGood), Message = "Batch OK" };
            }
            catch (Exception ex) { return new WriteResult { Success = false, Message = ex.Message }; }
        }

        public async Task WriteInitialValuesAsync()
        {
            foreach (var device in _deviceConfigs)
            {
                var initialTags = device.Value.Tags.ToDictionary(t => t.Key, t => t.Value.DefaultValue);
                await WriteTagsAsync(device.Key, initialTags);
            }
        }

        public async Task WriteRandomDataAsync(string deviceName)
        {
            if (_deviceConfigs.TryGetValue(deviceName, out var config))
            {
                var tags = config.Tags.ToDictionary(t => t.Key, t => t.Value.DefaultValue);
                await WriteTagsAsync(deviceName, tags);
            }
        }

        public async Task WriteRandomDataToAllAsync()
        {
            foreach (var name in _deviceConfigs.Keys) await WriteRandomDataAsync(name);
        }

        public async Task SimulateEsp32StartAsync() => await WriteTagsAsync("ESP32_01", new Dictionary<string, object> { ["Running"] = true, ["Status"] = (short)1 });
        public async Task SimulateEsp32StopAsync() => await WriteTagsAsync("ESP32_01", new Dictionary<string, object> { ["Running"] = false, ["Status"] = (short)0 });
        public async Task SimulateEsp32ErrorAsync(short code = 101) => await WriteTagsAsync("ESP32_01", new Dictionary<string, object> { ["Running"] = false, ["ErrorCode"] = code });
        public async Task SimulateAgvMoveAsync(float x, float y) => await WriteTagsAsync("AGV_01", new Dictionary<string, object> { ["CurrentX"] = x, ["CurrentY"] = y });
        public async Task SimulatePlcProductionAsync(int count = 10) => await WriteTagAsync("PLC_01", "D100", (short)count);

        #endregion

        public void StartAutoUpdate(int intervalMs = 1000)
        {
            StopAutoUpdate();
            _autoUpdateCts = new CancellationTokenSource();
            Task.Run(async () => {
                while (!_autoUpdateCts.Token.IsCancellationRequested)
                {
                    try { await WriteRandomDataToAllAsync(); await Task.Delay(intervalMs, _autoUpdateCts.Token); }
                    catch { break; }
                }
            });
        }

        public void StopAutoUpdate() { _autoUpdateCts?.Cancel(); _autoUpdateCts?.Dispose(); _autoUpdateCts = null; }
        public void Dispose() { StopAutoUpdate(); _session?.Dispose(); }
    }

    public class DeviceConfig { public string DeviceName { get; set; } = ""; public string ChannelName { get; set; } = ""; public Dictionary<string, TagConfig> Tags { get; set; } = new(); }
    public class TagConfig { public Type DataType { get; set; } = typeof(object); public object DefaultValue { get; set; } = null!; public double? MinValue { get; set; } public double? MaxValue { get; set; } }
    public class WriteResult { public bool Success { get; set; } public string Message { get; set; } = ""; }
}