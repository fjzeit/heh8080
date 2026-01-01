using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Heh8080.ViewModels;
using Heh8080.Views;

namespace Heh8080;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _viewModel = new MainViewModel();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _viewModel
            };

            // Clean up on shutdown
            desktop.ShutdownRequested += (s, e) =>
            {
                _viewModel?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
