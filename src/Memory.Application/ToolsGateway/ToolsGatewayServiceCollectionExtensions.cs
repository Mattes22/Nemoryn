namespace Memory.Application.ToolsGateway;

using Microsoft.Extensions.DependencyInjection;
using Memory.Application.ToolsGateway.Web;

internal static class ToolsGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddNemorynToolsGateway(this IServiceCollection services)
    {
        services.AddSingleton<ICapabilityAuthorizer, CapabilityAuthorizer>();
        services.AddSingleton<IToolInvocationAuditor, LoggingToolInvocationAuditor>();
        services.AddScoped<IWebContentFetcher, WebContentFetcher>();
        services.AddScoped<ITool, WebSearchTool>();
        services.AddScoped<ITool, WebFetchTool>();
        services.AddScoped<IToolRegistry, ToolRegistry>();
        services.AddScoped<IToolExecutor, ToolExecutor>();

        return services;
    }
}
