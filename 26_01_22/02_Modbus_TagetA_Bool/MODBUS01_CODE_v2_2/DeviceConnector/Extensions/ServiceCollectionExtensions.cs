namespace DeviceConnector.Extensions;

using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using DeviceConnector.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// DI 컨테이너 확장 메서드
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// DeviceConnector 서비스 등록
    /// </summary>
    public static IServiceCollection AddDeviceConnector(
        this IServiceCollection services,
        OpcUaConnectionInfo connectionInfo)
    {
        services.AddSingleton(connectionInfo);
        services.AddSingleton<IOpcUaClientService>(sp =>
        {
            var logger = sp.GetService<ILogger<OpcUaClientService>>();
            return new OpcUaClientService(connectionInfo, logger);
        });

        return services;
    }

    /// <summary>
    /// DeviceConnector 서비스 등록 (설정 액션)
    /// </summary>
    public static IServiceCollection AddDeviceConnector(
        this IServiceCollection services,
        Action<OpcUaConnectionInfo> configure)
    {
        var connectionInfo = new OpcUaConnectionInfo();
        configure(connectionInfo);

        return services.AddDeviceConnector(connectionInfo);
    }
}
