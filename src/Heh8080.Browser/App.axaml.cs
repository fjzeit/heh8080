using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Heh8080.UI.ViewModels;
using Heh8080.UI.Views;

namespace Heh8080.Browser;

public partial class App : Application
{
    private MainViewModel? _viewModel;
    private MemoryDiskImageProvider? _diskProvider;
    private MainView? _mainView;
    private ConfigPanel? _configPanel;

    // Base dimensions at 100% scale (must match RetroTerminalControl calculations)
    private const double BaseWidth = 1680;  // Approximate at 100% scale
    private const double BaseHeight = 1284; // Approximate at 100% scale
    private static readonly double[] AvailableScales = { 1.0, 0.8, 0.6, 0.4 };

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

            _mainView = new MainView
            {
                DataContext = _viewModel
            };

            _configPanel = new ConfigPanel();
            _configPanel.SetViewModel(_viewModel);
            _configPanel.SaveDriveBRequested += SaveDriveBAsync;

            // Create container with MainView and ConfigPanel overlay
            var container = new Panel();
            container.Children.Add(_mainView);
            container.Children.Add(_configPanel);

            _mainView.LogoClicked += () =>
            {
                _configPanel.Show();
            };

            singleViewLifetime.MainView = container;

            // Set initial scale based on viewport and register resize listener
            SetupAutoScaling();

            // Start async boot process
            _ = LoadAndBootAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupAutoScaling()
    {
        // Set initial scale
        var viewportWidth = DiskStorageInterop.GetViewportWidth();
        var viewportHeight = DiskStorageInterop.GetViewportHeight();
        UpdateScale(viewportWidth, viewportHeight);

        // Register for resize events
        DiskStorageInterop.RegisterResizeListener(OnViewportResize);
    }

    private void OnViewportResize(int width, int height)
    {
        UpdateScale(width, height);
    }

    private void UpdateScale(int viewportWidth, int viewportHeight)
    {
        if (_mainView == null) return;

        // Find the largest scale that fits
        var bestScale = 0.25; // Minimum fallback
        foreach (var scale in AvailableScales)
        {
            var scaledWidth = BaseWidth * scale;
            var scaledHeight = BaseHeight * scale;

            if (scaledWidth <= viewportWidth && scaledHeight <= viewportHeight)
            {
                bestScale = scale;
                break; // First match is the largest that fits
            }
        }

        _mainView.TerminalScale = bestScale;
    }

    private async Task LoadAndBootAsync()
    {
        if (_diskProvider == null || _viewModel == null) return;

        // Load B: drive from IndexedDB (user files persist)
        await LoadDriveBFromStorageAsync();

        // Always load A: from bundled disk (fresh on each release)
        try
        {
            var origin = DiskStorageInterop.GetOrigin();
            var diskUrl = origin + "/lolos.dsk";
            var diskData = await DiskStorageInterop.FetchFileAsync(diskUrl);

            if (diskData.Length > 0)
            {
                _diskProvider.MountFromBytes(0, diskData, "lolos.dsk", readOnly: false);
                _viewModel.UpdateDriveStatus(0, "lolos.dsk");
            }
            _viewModel.Boot();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load disk: {ex.Message}");
            _viewModel.Boot(); // Boot without disk - shows welcome message
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
            var name = drive == 1 ? "USER.DSK" : $"disk{drive}.dsk";
            await DiskStorageInterop.SaveDiskAsync(drive, name, data);
            Console.WriteLine($"Saved disk {drive} to IndexedDB");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save disk: {ex.Message}");
        }
    }

    private async Task SaveDriveBAsync()
    {
        await SaveDiskToStorageAsync(1);
    }

    private async Task LoadDriveBFromStorageAsync()
    {
        if (_diskProvider == null || _viewModel == null) return;

        try
        {
            var (savedName, savedData) = await DiskStorageInterop.LoadDiskDataAsync(1);
            if (savedData != null && savedData.Length > 0)
            {
                _diskProvider.MountFromBytes(1, savedData, savedName ?? "USER.DSK", readOnly: false);
                _viewModel.UpdateDriveStatus(1, savedName ?? "USER.DSK");
                Console.WriteLine($"Loaded B: from IndexedDB: {savedName}");
            }
        }
        catch
        {
            // No saved B: disk - that's fine
        }
    }
}
