using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Heh8080.UI.ViewModels;
using Heh8080.UI.Views;

namespace Heh8080.Browser;

public partial class App : Application
{
    private MainViewModel? _viewModel;
    private MemoryDiskImageProvider? _diskProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
        {
            _diskProvider = new MemoryDiskImageProvider();
            _viewModel = new MainViewModel(_diskProvider);

            var mainView = new MainView
            {
                DataContext = _viewModel
            };

            mainView.LogoClicked += async () =>
            {
                // Save current disk to IndexedDB
                await SaveDiskToStorageAsync(0);
            };

            singleViewLifetime.MainView = mainView;

            // Start async boot process
            _ = LoadAndBootAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task LoadAndBootAsync()
    {
        if (_diskProvider == null || _viewModel == null) return;

        try
        {
            // First try to load saved disk from IndexedDB
            var (savedName, savedData) = await DiskStorageInterop.LoadDiskDataAsync(0);
            if (savedData != null && savedData.Length > 0)
            {
                Console.WriteLine($"Loaded saved disk from IndexedDB: {savedName}");
                _diskProvider.MountFromBytes(0, savedData, savedName ?? "disk.dsk", readOnly: false);
                _viewModel.UpdateDriveStatus(0, savedName ?? "disk.dsk");
                _viewModel.Boot();
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexedDB load failed: {ex.Message}");
        }

        // Fall back to bundled disk from wwwroot
        try
        {
            using var httpClient = new HttpClient();
            var origin = DiskStorageInterop.GetOrigin();
            var diskUri = new Uri(new Uri(origin + "/"), "lolos.dsk");

            Console.WriteLine($"Loading disk from: {diskUri}");
            var diskData = await httpClient.GetByteArrayAsync(diskUri);
            Console.WriteLine($"Loaded {diskData.Length} bytes");

            if (diskData.Length > 0)
            {
                _diskProvider.MountFromBytes(0, diskData, "lolos.dsk", readOnly: false);
                _viewModel.UpdateDriveStatus(0, "lolos.dsk");
                _viewModel.Boot();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load bundled disk: {ex.Message}");
            // Boot without disk
            _viewModel.Boot();
        }
    }

    private async Task SaveDiskToStorageAsync(int drive)
    {
        if (_diskProvider == null) return;

        var data = _diskProvider.GetDiskData(drive);
        if (data == null)
        {
            Console.WriteLine($"No disk in drive {drive} to save");
            return;
        }

        try
        {
            var name = $"disk{drive}.dsk";
            await DiskStorageInterop.SaveDiskAsync(drive, name, data);
            Console.WriteLine($"Saved disk {drive} to IndexedDB");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save disk: {ex.Message}");
        }
    }
}
