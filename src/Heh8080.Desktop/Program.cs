using System;
using System.Linq;
using Avalonia;
using Heh8080.Mcp;

namespace Heh8080.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Check for MCP server mode
        if (args.Contains("--mcp"))
        {
            // Find disk path argument if provided
            string? diskPath = null;
            int diskIndex = Array.IndexOf(args, "--disk");
            if (diskIndex >= 0 && diskIndex + 1 < args.Length)
            {
                diskPath = args[diskIndex + 1];
            }

            // Run MCP server (blocking)
            McpServerHost.RunAsync(diskPath).GetAwaiter().GetResult();
            return;
        }

        // Normal Avalonia desktop app
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
