using McpToolkit.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace McpToolkit.Server;

public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Register an MCP server with dependency injection.
    /// </summary>
    /// <example>
    /// builder.Services.AddMcpServer("my-server", "1.0.0", server =>
    /// {
    ///     server.AddTool(b => b
    ///         .Create("get_time")
    ///         .WithDescription("Returns the current UTC time")
    ///         .ExecutesWith(async (_, _) =>
    ///             new McpToolResult([McpContent.FromText(DateTime.UtcNow.ToString("R"))])));
    /// });
    /// </example>
    public static IServiceCollection AddMcpServer(
        this IServiceCollection services,
        string serverName,
        string serverVersion,
        Action<McpServer>? configure = null)
    {
        services.AddLogging();
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<McpServer>>();
            var info   = new McpServerInfo(
                serverName, serverVersion,
                new McpCapabilities(Tools: true));
            var server = new McpServer(info, logger);
            configure?.Invoke(server);
            return server;
        });
        return services;
    }
}
