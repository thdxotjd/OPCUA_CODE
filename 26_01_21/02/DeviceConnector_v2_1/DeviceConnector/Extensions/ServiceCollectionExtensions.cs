namespace DeviceConnector.Extensions;

using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using DeviceConnector.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 서비스 컬렉션 확장 메서드
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// DeviceConnector 서비스 등록 (설정 객체 직접 전달)
    /// </summary>
    public static IServiceCollection AddDeviceConnector(
        this IServiceCollection services,
        OpcUaConnectionInfo connectionInfo)
    {
        services.AddSingleton(connectionInfo);
        services.AddSingleton<IOpcUaClientService, OpcUaClientService>();
        return services;
    }

    /// <summary>
    /// DeviceConnector 서비스 등록 (IConfiguration 사용)
    /// </summary>
    public static IServiceCollection AddDeviceConnector(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "OpcUa")
    {
        var connectionInfo = new OpcUaConnectionInfo();
        configuration.GetSection(sectionName).Bind(connectionInfo);

        return services.AddDeviceConnector(connectionInfo);
    }

    /// <summary>
    /// DeviceConnector 서비스 등록 (Action 설정)
    /// </summary>
    public static IServiceCollection AddDeviceConnector(
        this IServiceCollection services,
        Action<OpcUaConnectionInfo> configure)
    {
        var connectionInfo = new OpcUaConnectionInfo();
        configure(connectionInfo);

        return services.AddDeviceConnector(connectionInfo);
    }

    /// <summary>
    /// 디바이스 설정과 함께 DeviceConnector 등록
    /// </summary>
    public static IServiceCollection AddDeviceConnectorWithDevices(
        this IServiceCollection services,
        OpcUaConnectionInfo connectionInfo,
        params DeviceTagConfig[] devices)
    {
        services.AddDeviceConnector(connectionInfo);

        // 디바이스 설정 등록
        services.AddSingleton<IEnumerable<DeviceTagConfig>>(devices);

        return services;
    }
}
