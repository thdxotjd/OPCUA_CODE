using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using DeviceConnector.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeviceConnector.Extensions
{
    /// <summary>
    /// ASP.NET Core 서비스 등록 확장 메서드
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        #region OPC UA 전용 등록

        /// <summary>
        /// DeviceConnector OPC UA 서비스를 DI 컨테이너에 등록 (appsettings.json 사용)
        /// </summary>
        public static IServiceCollection AddDeviceConnector(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 설정 바인딩
            var connectionInfo = new OpcUaConnectionInfo();
            configuration.GetSection("OpcUa").Bind(connectionInfo);

            var deviceConfig = new DeviceTagConfig();
            configuration.GetSection("DeviceTag").Bind(deviceConfig);

            services.AddSingleton(connectionInfo);
            services.AddSingleton(deviceConfig);

            // OPC UA 클라이언트 서비스 등록 (Singleton)
            services.AddSingleton<IOpcUaClientService>(provider =>
            {
                var connInfo = provider.GetRequiredService<OpcUaConnectionInfo>();
                var devConfig = provider.GetRequiredService<DeviceTagConfig>();
                return new OpcUaClientService(connInfo, devConfig);
            });

            return services;
        }

        /// <summary>
        /// DeviceConnector OPC UA 서비스를 DI 컨테이너에 등록 (수동 설정)
        /// </summary>
        public static IServiceCollection AddDeviceConnector(
            this IServiceCollection services,
            Action<OpcUaConnectionInfo> configureConnection,
            Action<DeviceTagConfig>? configureDevice = null)
        {
            var connectionInfo = new OpcUaConnectionInfo();
            configureConnection(connectionInfo);

            var deviceConfig = new DeviceTagConfig();
            configureDevice?.Invoke(deviceConfig);

            services.AddSingleton(connectionInfo);
            services.AddSingleton(deviceConfig);

            services.AddSingleton<IOpcUaClientService>(provider =>
            {
                var connInfo = provider.GetRequiredService<OpcUaConnectionInfo>();
                var devConfig = provider.GetRequiredService<DeviceTagConfig>();
                return new OpcUaClientService(connInfo, devConfig);
            });

            return services;
        }

        #endregion

        #region ROS_ControlHub 통합 등록

        /// <summary>
        /// DeviceConnector + ROS_ControlHub 통합 서비스 등록 (appsettings.json 사용)
        /// 
        /// appsettings.json 예시:
        /// {
        ///   "OpcUa": { ... },
        ///   "DeviceTag": { ... },
        ///   "RosControlHub": {
        ///     "ServerUrl": "http://localhost:5178",
        ///     "AutoReconnect": true,
        ///     "ReconnectIntervalSeconds": 5
        ///   }
        /// }
        /// </summary>
        public static IServiceCollection AddDeviceConnectorWithRosHub(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // OPC UA 설정
            var connectionInfo = new OpcUaConnectionInfo();
            configuration.GetSection("OpcUa").Bind(connectionInfo);

            var deviceConfig = new DeviceTagConfig();
            configuration.GetSection("DeviceTag").Bind(deviceConfig);

            // ROS_ControlHub 설정
            var rosHubConfig = new RosControlHubConfig();
            configuration.GetSection("RosControlHub").Bind(rosHubConfig);

            services.AddSingleton(connectionInfo);
            services.AddSingleton(deviceConfig);
            services.AddSingleton(rosHubConfig);

            // OPC UA 클라이언트 서비스
            services.AddSingleton<IOpcUaClientService>(provider =>
            {
                var connInfo = provider.GetRequiredService<OpcUaConnectionInfo>();
                var devConfig = provider.GetRequiredService<DeviceTagConfig>();
                return new OpcUaClientService(connInfo, devConfig);
            });

            // ROS_ControlHub 클라이언트 서비스
            services.AddSingleton<IRosControlHubClient>(provider =>
            {
                var config = provider.GetRequiredService<RosControlHubConfig>();
                var logger = provider.GetService<ILogger<RosControlHubClient>>();
                return new RosControlHubClient(config, logger);
            });

            // 통합 디바이스 허브
            services.AddSingleton<IntegratedDeviceHub>(provider =>
            {
                var opcService = provider.GetRequiredService<IOpcUaClientService>();
                var rosClient = provider.GetRequiredService<IRosControlHubClient>();
                var logger = provider.GetService<ILogger<IntegratedDeviceHub>>();
                return new IntegratedDeviceHub(opcService, rosClient, logger);
            });

            // ROS_ControlHub 호환 어댑터 (선택적)
            services.AddSingleton<RosCompatibleOpcUaAdapter>(provider =>
            {
                var opcService = provider.GetRequiredService<IOpcUaClientService>();
                var devConfig = provider.GetRequiredService<DeviceTagConfig>();
                var logger = provider.GetService<ILogger<RosCompatibleOpcUaAdapter>>();
                return new RosCompatibleOpcUaAdapter(opcService, devConfig, logger);
            });

            return services;
        }

        /// <summary>
        /// DeviceConnector + ROS_ControlHub 통합 서비스 등록 (수동 설정)
        /// </summary>
        public static IServiceCollection AddDeviceConnectorWithRosHub(
            this IServiceCollection services,
            Action<OpcUaConnectionInfo> configureOpc,
            Action<RosControlHubConfig> configureRosHub,
            Action<DeviceTagConfig>? configureDevice = null)
        {
            var connectionInfo = new OpcUaConnectionInfo();
            configureOpc(connectionInfo);

            var deviceConfig = new DeviceTagConfig();
            configureDevice?.Invoke(deviceConfig);

            var rosHubConfig = new RosControlHubConfig();
            configureRosHub(rosHubConfig);

            services.AddSingleton(connectionInfo);
            services.AddSingleton(deviceConfig);
            services.AddSingleton(rosHubConfig);

            // OPC UA 클라이언트 서비스
            services.AddSingleton<IOpcUaClientService>(provider =>
            {
                var connInfo = provider.GetRequiredService<OpcUaConnectionInfo>();
                var devConfig = provider.GetRequiredService<DeviceTagConfig>();
                return new OpcUaClientService(connInfo, devConfig);
            });

            // ROS_ControlHub 클라이언트 서비스
            services.AddSingleton<IRosControlHubClient>(provider =>
            {
                var config = provider.GetRequiredService<RosControlHubConfig>();
                var logger = provider.GetService<ILogger<RosControlHubClient>>();
                return new RosControlHubClient(config, logger);
            });

            // 통합 디바이스 허브
            services.AddSingleton<IntegratedDeviceHub>(provider =>
            {
                var opcService = provider.GetRequiredService<IOpcUaClientService>();
                var rosClient = provider.GetRequiredService<IRosControlHubClient>();
                var logger = provider.GetService<ILogger<IntegratedDeviceHub>>();
                return new IntegratedDeviceHub(opcService, rosClient, logger);
            });

            // ROS_ControlHub 호환 어댑터
            services.AddSingleton<RosCompatibleOpcUaAdapter>(provider =>
            {
                var opcService = provider.GetRequiredService<IOpcUaClientService>();
                var devConfig = provider.GetRequiredService<DeviceTagConfig>();
                var logger = provider.GetService<ILogger<RosCompatibleOpcUaAdapter>>();
                return new RosCompatibleOpcUaAdapter(opcService, devConfig, logger);
            });

            return services;
        }

        #endregion

        #region RoboDK OPC UA 서비스 등록

        /// <summary>
        /// RoboDK OPC UA 서비스를 DI 컨테이너에 등록 (appsettings.json 사용)
        /// 
        /// appsettings.json 예시:
        /// {
        ///   "RoboDk": {
        ///     "EndpointUrl": "opc.tcp://localhost:4840",
        ///     "DefaultRobotName": "ABB CRB 1300-7/1.4",
        ///     "AutoReconnect": true
        ///   }
        /// }
        /// </summary>
        public static IServiceCollection AddRoboDkOpcUaService(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionInfo = new RoboDkConnectionInfo();
            configuration.GetSection("RoboDk").Bind(connectionInfo);

            services.AddSingleton(connectionInfo);

            services.AddSingleton<IRoboDkOpcUaService>(provider =>
            {
                var connInfo = provider.GetRequiredService<RoboDkConnectionInfo>();
                return new RoboDkOpcUaService(connInfo);
            });

            return services;
        }

        /// <summary>
        /// RoboDK OPC UA 서비스를 DI 컨테이너에 등록 (수동 설정)
        /// </summary>
        public static IServiceCollection AddRoboDkOpcUaService(
            this IServiceCollection services,
            Action<RoboDkConnectionInfo> configure)
        {
            var connectionInfo = new RoboDkConnectionInfo();
            configure(connectionInfo);

            services.AddSingleton(connectionInfo);

            services.AddSingleton<IRoboDkOpcUaService>(provider =>
            {
                var connInfo = provider.GetRequiredService<RoboDkConnectionInfo>();
                return new RoboDkOpcUaService(connInfo);
            });

            return services;
        }

        /// <summary>
        /// DeviceConnector + RoboDK 통합 서비스 등록
        /// ESP32/PLC + RoboDK 로봇 제어를 동시에 사용할 때
        /// </summary>
        public static IServiceCollection AddDeviceConnectorWithRoboDk(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // ESP32/PLC용 OPC UA 설정
            var opcConnectionInfo = new OpcUaConnectionInfo();
            configuration.GetSection("OpcUa").Bind(opcConnectionInfo);

            var deviceConfig = new DeviceTagConfig();
            configuration.GetSection("DeviceTag").Bind(deviceConfig);

            // RoboDK 설정
            var roboDkConnectionInfo = new RoboDkConnectionInfo();
            configuration.GetSection("RoboDk").Bind(roboDkConnectionInfo);

            services.AddSingleton(opcConnectionInfo);
            services.AddSingleton(deviceConfig);
            services.AddSingleton(roboDkConnectionInfo);

            // ESP32/PLC용 OPC UA 클라이언트
            services.AddSingleton<IOpcUaClientService>(provider =>
            {
                var connInfo = provider.GetRequiredService<OpcUaConnectionInfo>();
                var devConfig = provider.GetRequiredService<DeviceTagConfig>();
                return new OpcUaClientService(connInfo, devConfig);
            });

            // RoboDK OPC UA 클라이언트
            services.AddSingleton<IRoboDkOpcUaService>(provider =>
            {
                var connInfo = provider.GetRequiredService<RoboDkConnectionInfo>();
                return new RoboDkOpcUaService(connInfo);
            });

            return services;
        }

        #endregion

        #region ROS_ControlHub 클라이언트만 등록

        /// <summary>
        /// ROS_ControlHub 클라이언트만 등록 (OPC UA 없이 ROS_ControlHub와만 통신)
        /// </summary>
        public static IServiceCollection AddRosControlHubClient(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var rosHubConfig = new RosControlHubConfig();
            configuration.GetSection("RosControlHub").Bind(rosHubConfig);

            services.AddSingleton(rosHubConfig);

            services.AddSingleton<IRosControlHubClient>(provider =>
            {
                var config = provider.GetRequiredService<RosControlHubConfig>();
                var logger = provider.GetService<ILogger<RosControlHubClient>>();
                return new RosControlHubClient(config, logger);
            });

            return services;
        }

        /// <summary>
        /// ROS_ControlHub 클라이언트만 등록 (수동 설정)
        /// </summary>
        public static IServiceCollection AddRosControlHubClient(
            this IServiceCollection services,
            Action<RosControlHubConfig> configure)
        {
            var rosHubConfig = new RosControlHubConfig();
            configure(rosHubConfig);

            services.AddSingleton(rosHubConfig);

            services.AddSingleton<IRosControlHubClient>(provider =>
            {
                var config = provider.GetRequiredService<RosControlHubConfig>();
                var logger = provider.GetService<ILogger<RosControlHubClient>>();
                return new RosControlHubClient(config, logger);
            });

            return services;
        }

        #endregion
    }
}
