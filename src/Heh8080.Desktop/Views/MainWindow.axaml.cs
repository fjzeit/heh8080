using Avalonia.Controls;
using Heh8080.UI.ViewModels;

namespace Heh8080.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Handle logo click to open config dialog
        MainView.LogoClicked += OnLogoClicked;
    }

    private async void OnLogoClicked()
    {
        if (DataContext is not MainViewModel vm) return;

        var dialog = new ConfigDialog();
        dialog.SetViewModel(vm);
        await dialog.ShowDialog(this);

        // Re-focus terminal after dialog closes
        MainView.FocusTerminal();
    }
}
