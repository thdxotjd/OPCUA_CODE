using Opc.Ua;
using Opc.Ua.Client;

namespace RoboDkWebTest.Services
{
    /// <summary>
    /// RoboDK OPC UA 서비스 (테스트용 간소화 버전)
    /// </summary>
    public class RoboDkService : IDisposable
    {
        private Session? _session;
        private readonly string _endpointUrl = "opc.tcp://localhost:4840";

        public bool IsConnected => _session?.Connected ?? false;

        /// <summary>
        /// RoboDK OPC UA 서버에 연결
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (IsConnected) return true;

                // 1. 설정 생성
                var config = new ApplicationConfiguration
                {
                    ApplicationName = "RoboDkWebTest",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier(),
                        AutoAcceptUntrustedCertificates = true
                    },
                    ClientConfiguration = new ClientConfiguration
                    {
                        DefaultSessionTimeout = 60000
                    },
                    TransportQuotas = new TransportQuotas { OperationTimeout = 30000 }
                };

                await config.Validate(ApplicationType.Client);

                config.CertificateValidator.CertificateValidation += (s, e) =>
                {
                    e.Accept = true;  // 인증서 자동 수락
                };

                // 2. 엔드포인트 구성
                var endpoint = new EndpointDescription
                {
                    EndpointUrl = _endpointUrl,
                    SecurityMode = MessageSecurityMode.None,
                    SecurityPolicyUri = SecurityPolicies.None,
                    UserIdentityTokens = new UserTokenPolicyCollection
                    {
                        new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
                    },
                    TransportProfileUri = Profiles.UaTcpTransport
                };

                var configuredEndpoint = new ConfiguredEndpoint(null, endpoint,
                    EndpointConfiguration.Create(config));

                // 3. 세션 생성
                _session = await Session.Create(
                    config,
                    configuredEndpoint,
                    false,
                    "RoboDkWebTest_Session",
                    60000,
                    new UserIdentity(new AnonymousIdentityToken()),
                    null);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"연결 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 연결 해제
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_session != null)
            {
                try
                {
                    if (_session.Connected)
                        await _session.CloseAsync();
                }
                catch { }
                _session.Dispose();
                _session = null;
            }
        }

        /// <summary>
        /// RoboDK 정보 읽기 (Variable 노드)
        /// </summary>
        public async Task<object> GetRoboDkInfoAsync()
        {
            if (_session == null) return new { error = "Not connected" };

            try
            {
                // Variable 노드 읽기
                var roboDkVersion = await ReadVariableAsync<string>("ns=1;s=RoboDK");
                var station = await ReadVariableAsync<string>("ns=1;s=Station");
                var simSpeed = await ReadVariableAsync<double>("ns=1;s=SimulationSpeed");
                var time = await ReadVariableAsync<DateTime>("ns=1;s=time");

                return new
                {
                    roboDkVersion,
                    station,
                    simulationSpeed = simSpeed,
                    serverTime = time
                };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }

        /// <summary>
        /// Joint 값 읽기 (Method 호출 - getJointsStr)
        /// </summary>
        public async Task<string?> GetJointsAsync(string robotName)
        {
            if (_session == null) return null;

            try
            {
                var result = await CallMethodAsync(
                    new NodeId(85),           // Objects 폴더
                    new NodeId(1002, 1),      // getJointsStr (ns=1;i=1002)
                    robotName);

                return result?.FirstOrDefault()?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Joint 읽기 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Joint 값 설정 (Method 호출 - setJointsStr)
        /// </summary>
        public async Task<bool> SetJointsAsync(string robotName, string jointsStr)
        {
            if (_session == null) return false;

            try
            {
                await CallMethodAsync(
                    new NodeId(85),           // Objects 폴더
                    new NodeId(2002, 1),      // setJointsStr (ns=1;i=2002)
                    robotName,
                    jointsStr);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Joint 설정 실패: {ex.Message}");
                return false;
            }
        }

        #region Private Methods

        private async Task<T?> ReadVariableAsync<T>(string nodeIdString)
        {
            if (_session == null) return default;

            var nodeId = new NodeId(nodeIdString);
            var value = await _session.ReadValueAsync(nodeId);

            if (StatusCode.IsGood(value.StatusCode))
            {
                return (T)Convert.ChangeType(value.Value, typeof(T));
            }
            return default;
        }

        private async Task<List<object>?> CallMethodAsync(NodeId objectId, NodeId methodId, params object[] args)
        {
            if (_session == null) return null;

            var request = new CallMethodRequest
            {
                ObjectId = objectId,
                MethodId = methodId,
                InputArguments = new VariantCollection()
            };

            foreach (var arg in args)
            {
                request.InputArguments.Add(new Variant(arg));
            }

            var response = await _session.CallAsync(null, new CallMethodRequestCollection { request }, default);

            if (response.Results?.Count > 0)
            {
                var result = response.Results[0];
                if (StatusCode.IsBad(result.StatusCode))
                    throw new Exception($"Method 호출 실패: {result.StatusCode}");

                return result.OutputArguments?.Select(v => v.Value).ToList();
            }

            return null;
        }

        #endregion

        public void Dispose()
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
    }
}
