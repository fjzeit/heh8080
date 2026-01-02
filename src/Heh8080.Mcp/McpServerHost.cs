using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Heh8080.Mcp;

/// <summary>
/// Hosts the MCP server for CP/M machine interaction.
/// </summary>
public static class McpServerHost
{
    /// <summary>
    /// Run the MCP server with stdio transport.
    /// </summary>
    public static async Task RunAsync(string? diskPath = null, CancellationToken cancellationToken = default)
    {
        // Create the CP/M machine
        using var machine = new CpmMachine(diskPath);

        // Boot if disk is mounted
        if (machine.DiskProvider.IsMounted(0))
        {
            machine.Boot();
        }

        // Build the host
        var builder = Host.CreateApplicationBuilder();

        // Configure logging to stderr (stdout is for MCP protocol)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Register the machine as a singleton
        builder.Services.AddSingleton(machine);

        // Add MCP server with stdio transport
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<CpmTools>();

        var host = builder.Build();

        await host.RunAsync(cancellationToken);
    }
}
