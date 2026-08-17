using FluentValidation;
using MerchantAdmin.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MerchantAdmin.Application;

/// <summary>应用层的依赖注入注册。</summary>
public static class DependencyInjection
{
    /// <summary>注册应用层服务，包括 FluentValidation 校验器。</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // 自动扫描并注册当前程序集中所有 IValidator<T> 实现
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // 订单超时关闭：过期事件（RedisExpiredOrderConsumer）与定时兜底扫描共用处理器
        services.AddScoped<OrderTimeoutProcessor>();

        // 超时兜底扫描（定时），间隔/时限走配置 OrderTimeout
        services.AddOptions<OrderTimeoutOptions>()
            .BindConfiguration(OrderTimeoutOptions.SectionName)
            .ValidateDataAnnotations();
        services.AddHostedService<OrderTimeoutCompensationService>();

        return services;
    }
}
