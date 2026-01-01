using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Heh8080.UI.ViewModels;
using Heh8080.Devices;
using Heh8080.Views;

namespace Heh8080;

public partial class App : Application
{
    private MainViewModel? _viewModel;
    private string? _extractedDiskPath;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var diskProvider = new FileDiskImageProvider();
            _viewModel = new MainViewModel(diskProvider);

            desktop.MainWindow = new MainWindow
            {
                DataContext = _viewModel
            };

            // Clean up on shutdown
            desktop.ShutdownRequested += (s, e) =>
            {
                _viewModel?.Dispose();
                CleanupTempDisk();
            };

            // Auto-boot from bundled LOLOS disk
            AutoBoot(diskProvider);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void AutoBoot(FileDiskImageProvider diskProvider)
    {
        try
        {
            // Try to extract and mount bundled LOLOS disk
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Heh8080.Desktop.Assets.disks.lolos.dsk";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                // Extract to temp file
                _extractedDiskPath = Path.Combine(Path.GetTempPath(), "heh8080_lolos.dsk");
                using (var fs = File.Create(_extractedDiskPath))
                {
                    stream.CopyTo(fs);
                }

                // Mount to drive A:
                diskProvider.Mount(0, _extractedDiskPath, readOnly: false);
                _viewModel!.UpdateDriveStatus(0, "lolos.img");

                // Boot from drive A:
                _viewModel!.Boot();
            }
            else
            {
                // No bundled disk - just show welcome
                _viewModel!.Boot();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto-boot failed: {ex.Message}");
            _viewModel?.Boot();
        }
    }

    private void CleanupTempDisk()
    {
        if (_extractedDiskPath != null && File.Exists(_extractedDiskPath))
        {
            try { File.Delete(_extractedDiskPath); }
            catch { /* Ignore cleanup errors */ }
        }
    }
}
