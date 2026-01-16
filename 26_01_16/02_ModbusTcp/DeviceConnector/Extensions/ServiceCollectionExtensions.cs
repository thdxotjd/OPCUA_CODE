using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using DeviceConnector.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace DeviceConnector.Extensions
{
    /// <summary>
    /// ASP.NET Core 서비스 등록 확장 메서드
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// DeviceConnector 서비스를 DI 컨테이너에 등록
        /// </summary>
        /// <param name="services">서비스 컬렉션</param>
        /// <param name="configuration">설정</param>
        /// <returns>서비스 컬렉션</returns>
        public static IServiceCollection AddDeviceConnector(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 설정 바인딩
            var connectionInfo = new OpcUaConnectionInfo();
            configuration.GetSection("OpcUa").Bind(connectionInfo);

            services.AddSingleton(connectionInfo);

            // OPC UA 클라이언트 서비스 등록 (Singleton)
            services.AddSingleton<IOpcUaClientService>(provider =>
            {
                var info = provider.GetRequiredService<OpcUaConnectionInfo>();
                return new OpcUaClientService(info);
            });

            return services;
        }

        /// <summary>
        /// DeviceConnector 서비스를 DI 컨테이너에 등록 (수동 설정)
        /// </summary>
        /// <param name="services">서비스 컬렉션</param>
        /// <param name="configure">설정 액션</param>
        /// <returns>서비스 컬렉션</returns>
        public static IServiceCollection AddDeviceConnector(
            this IServiceCollection services,
            Action<OpcUaConnectionInfo> configure)
        {
            var connectionInfo = new OpcUaConnectionInfo();
            configure(connectionInfo);

            services.AddSingleton(connectionInfo);
            services.AddSingleton<IOpcUaClientService>(provider =>
            {
                var info = provider.GetRequiredService<OpcUaConnectionInfo>();
                return new OpcUaClientService(info);
            });

            return services;
        }
    }
}
