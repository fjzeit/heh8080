using Avalonia.Controls;
using Avalonia.VisualTree;
using Heh8080.ViewModels;

namespace Heh8080.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        // Wire terminal when DataContext is set
        DataContextChanged += (s, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                Terminal.Terminal = vm.Terminal;
            }
        };

        // Handle logo click to open config dialog
        Terminal.LogoClicked += OnLogoClicked;
    }

    private async void OnLogoClicked()
    {
        if (DataContext is not MainViewModel vm) return;

        var window = this.GetVisualRoot() as Window;
        if (window == null) return;

        var dialog = new ConfigDialog();
        dialog.SetViewModel(vm);
        await dialog.ShowDialog(window);

        // Re-focus terminal after dialog closes
        Terminal.Focus();
    }
}
