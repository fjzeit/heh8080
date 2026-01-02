using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Heh8080.Core;
using Heh8080.UI.ViewModels;

namespace Heh8080.Browser;

public partial class ConfigPanel : UserControl
{
    private MainViewModel? _viewModel;
    private MemoryDiskImageProvider? _diskProvider;

    /// <summary>
    /// Event fired when B: disk should be saved to IndexedDB.
    /// </summary>
    public event Func<System.Threading.Tasks.Task>? SaveDriveBRequested;

    public ConfigPanel()
    {
        InitializeComponent();
    }

    public void SetViewModel(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        _diskProvider = viewModel.DiskProvider as MemoryDiskImageProvider;
        UpdateUI();
    }

    public void Show()
    {
        UpdateUI();
        IsVisible = true;
    }

    public void Hide()
    {
        IsVisible = false;
    }

    private void UpdateUI()
    {
        if (_viewModel == null) return;

        // Update CPU selection
        CpuZ80.IsChecked = _viewModel.CpuType == CpuType.ZilogZ80;
        Cpu8080.IsChecked = _viewModel.CpuType == CpuType.Intel8080;

        // Update drive A label
        DriveALabel.Text = _viewModel.DriveAPath ?? "(no disk)";

        // Update drive B file list
        UpdateFileList();

        StatusLabel.Text = "";
    }

    private void UpdateFileList()
    {
        // Clear existing file entries (keep NoFilesLabel)
        while (FileListPanel.Children.Count > 1)
        {
            FileListPanel.Children.RemoveAt(1);
        }

        if (_diskProvider == null || !_diskProvider.IsMounted(1))
        {
            NoFilesLabel.IsVisible = true;
            NoFilesLabel.Text = "(empty - upload files to use)";
            return;
        }

        var files = _diskProvider.ListFiles(1);
        if (files.Count == 0)
        {
            NoFilesLabel.IsVisible = true;
            NoFilesLabel.Text = "(empty - upload files to use)";
            return;
        }

        NoFilesLabel.IsVisible = false;

        foreach (var (name, size) in files)
        {
            var sizeStr = size < 1024 ? $"{size}B" : $"{size / 1024}KB";
            var text = new TextBlock
            {
                Text = $"{name,-12} {sizeStr,6}",
                FontFamily = new FontFamily("Consolas,monospace"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#33FF33"))
            };
            FileListPanel.Children.Add(text);
        }
    }

    private void Backdrop_Click(object? sender, PointerPressedEventArgs e)
    {
        Hide();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private async void UploadFile_Click(object? sender, RoutedEventArgs e)
    {
        if (_diskProvider == null) return;

        try
        {
            StatusLabel.Text = "Select a file...";

            var result = await DiskStorageInterop.PickFileDataAsync(".com,.COM,.bas,.BAS,.txt,.TXT,*");
            if (result == null)
            {
                StatusLabel.Text = "";
                return;
            }

            var (filename, data) = result.Value;

            // Ensure B: drive exists
            if (!_diskProvider.IsMounted(1))
            {
                _diskProvider.CreateEmptyDisk(1, "USER.DSK");
                _viewModel?.UpdateDriveStatus(1, "USER.DSK");
            }

            // Write file to disk
            if (_diskProvider.WriteFile(1, filename, data))
            {
                StatusLabel.Text = $"Uploaded: {filename.ToUpperInvariant()}";
                UpdateFileList();

                // Save to IndexedDB
                if (SaveDriveBRequested != null)
                {
                    await SaveDriveBRequested.Invoke();
                }
            }
            else
            {
                StatusLabel.Text = "Upload failed - disk full or file too large";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void Reset_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        await _viewModel.ResetCommand.ExecuteAsync(null);
        StatusLabel.Text = "System reset";
    }

    private async void CpuType_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var newType = CpuZ80.IsChecked == true ? CpuType.ZilogZ80 : CpuType.Intel8080;
        if (newType != _viewModel.CpuType)
        {
            await _viewModel.SwitchCpuType(newType);
            StatusLabel.Text = $"Switched to {_viewModel.CpuTypeName}";
        }
    }
}
