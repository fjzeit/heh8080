using System;
using Avalonia.Controls;
using Heh8080.UI.ViewModels;

namespace Heh8080.UI.Views;

public partial class MainView : UserControl
{
    /// <summary>
    /// Event fired when the FJM-3A logo is clicked.
    /// Platform-specific code should handle this to show configuration UI.
    /// </summary>
    public event Action? LogoClicked;

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

        // Forward logo click to platform code
        Terminal.LogoClicked += () => LogoClicked?.Invoke();
    }

    /// <summary>
    /// Focus the terminal control for keyboard input.
    /// </summary>
    public void FocusTerminal()
    {
        Terminal.Focus();
    }
}
