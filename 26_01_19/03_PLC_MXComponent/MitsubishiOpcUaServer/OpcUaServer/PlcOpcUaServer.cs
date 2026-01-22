using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using MitsubishiOpcUaServer.Models;
using Microsoft.Extensions.Logging;

namespace MitsubishiOpcUaServer.OpcUaServer
{
    /// <summary>
    /// OPC UA 서버 구현
    /// </summary>
    public class PlcOpcUaServer : StandardServer
    {
        private readonly DeviceConfig _deviceConfig;
        private readonly OpcUaServerConfig _serverConfig;
        private readonly ILogger _logger;
        private PlcNodeManager? _nodeManager;

        public PlcNodeManager? NodeManager => _nodeManager;

        public PlcOpcUaServer(
            DeviceConfig deviceConfig,
            OpcUaServerConfig serverConfig,
            ILogger logger)
        {
            _deviceConfig = deviceConfig;
            _serverConfig = serverConfig;
            _logger = logger;
        }

        protected override MasterNodeManager CreateMasterNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration)
        {
            _logger.LogInformation("MasterNodeManager 생성 중...");

            var nodeManagers = new List<INodeManager>();
            
            _nodeManager = new PlcNodeManager(server, configuration, _deviceConfig, _logger);
            nodeManagers.Add(_nodeManager);

            return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
        }

        protected override ServerProperties LoadServerProperties()
        {
            return new ServerProperties
            {
                ManufacturerName = "MitsubishiOpcUaServer",
                ProductName = "Mitsubishi PLC OPC UA Server",
                ProductUri = "http://mitsubishi.opcua.server",
                SoftwareVersion = Utils.GetAssemblySoftwareVersion(),
                BuildNumber = Utils.GetAssemblyBuildNumber(),
                BuildDate = Utils.GetAssemblyTimestamp()
            };
        }

        /// <summary>
        /// DeviceData로 OPC UA 노드 업데이트
        /// </summary>
        public void UpdateData(DeviceData deviceData)
        {
            _nodeManager?.UpdateNodes(deviceData);
        }
    }

    /// <summary>
    /// OPC UA 서버 호스트 - 서버 시작/중지 관리
    /// </summary>
    public class OpcUaServerHost : IDisposable
    {
        private readonly DeviceConfig _deviceConfig;
        private readonly OpcUaServerConfig _serverConfig;
        private readonly ILogger _logger;
        private ApplicationInstance? _application;
        private PlcOpcUaServer? _server;
        private bool _disposed;

        public PlcOpcUaServer? Server => _server;
        public bool IsRunning { get; private set; }

        public OpcUaServerHost(
            DeviceConfig deviceConfig,
            OpcUaServerConfig serverConfig,
            ILogger logger)
        {
            _deviceConfig = deviceConfig;
            _serverConfig = serverConfig;
            _logger = logger;
        }

        /// <summary>
        /// OPC UA 서버 시작
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                _logger.LogInformation("OPC UA 서버 시작 중... Port: {Port}", _serverConfig.Port);

                // 애플리케이션 구성 생성
                var config = CreateApplicationConfiguration();

                // 인증서 검증 (자동 생성)
                await config.Validate(ApplicationType.Server);
                
                if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    config.CertificateValidator.CertificateValidation += (s, e) =>
                    {
                        e.Accept = true;
                    };
                }

                // 애플리케이션 인스턴스 생성
                _application = new ApplicationInstance
                {
                    ApplicationName = _serverConfig.ServerName,
                    ApplicationType = ApplicationType.Server,
                    ApplicationConfiguration = config
                };

                // 인증서 확인/생성
                bool haveAppCertificate = await _application.CheckApplicationInstanceCertificate(
                    silent: true, 
                    minimumKeySize: 2048);

                if (!haveAppCertificate)
                {
                    _logger.LogWarning("애플리케이션 인증서가 없습니다.");
                }

                // 서버 생성 및 시작
                _server = new PlcOpcUaServer(_deviceConfig, _serverConfig, _logger);
                await _application.Start(_server);

                IsRunning = true;
                _logger.LogInformation("OPC UA 서버 시작됨. Endpoint: opc.tcp://localhost:{Port}", _serverConfig.Port);

                // 엔드포인트 출력
                foreach (var endpoint in _server.CurrentInstance.EndpointAddresses)
                {
                    _logger.LogInformation("  Endpoint: {Endpoint}", endpoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OPC UA 서버 시작 실패");
                throw;
            }
        }

        /// <summary>
        /// OPC UA 서버 중지
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_server != null && IsRunning)
                {
                    _server.Stop();
                    IsRunning = false;
                    _logger.LogInformation("OPC UA 서버 중지됨");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OPC UA 서버 중지 중 오류");
            }
        }

        /// <summary>
        /// DeviceData로 OPC UA 노드 업데이트
        /// </summary>
        public void UpdateData(DeviceData deviceData)
        {
            _server?.UpdateData(deviceData);
        }

        private ApplicationConfiguration CreateApplicationConfiguration()
        {
            var config = new ApplicationConfiguration
            {
                ApplicationName = _serverConfig.ServerName,
                ApplicationUri = $"urn:localhost:{_serverConfig.ServerName}",
                ApplicationType = ApplicationType.Server,
                ProductUri = "http://mitsubishi.opcua.server",

                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "./pki/own",
                        SubjectName = _serverConfig.ServerName
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "./pki/issuer"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "./pki/trusted"
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "./pki/rejected"
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },

                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },

                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = new StringCollection
                    {
                        $"opc.tcp://localhost:{_serverConfig.Port}"
                    },
                    MinRequestThreadCount = 5,
                    MaxRequestThreadCount = 100,
                    MaxQueuedRequestCount = 200,

                    SecurityPolicies = new ServerSecurityPolicyCollection
                    {
                        new ServerSecurityPolicy
                        {
                            SecurityMode = MessageSecurityMode.None,
                            SecurityPolicyUri = SecurityPolicies.None
                        }
                    },

                    UserTokenPolicies = new UserTokenPolicyCollection
                    {
                        new UserTokenPolicy(UserTokenType.Anonymous)
                    }
                },

                TraceConfiguration = new TraceConfiguration()
            };

            return config;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
