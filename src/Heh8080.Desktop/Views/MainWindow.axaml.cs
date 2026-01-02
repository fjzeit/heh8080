using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Heh8080.UI.ViewModels;

namespace Heh8080.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Set initial scale to 60% for desktop
        MainView.TerminalScale = 0.6;

        // Resize window when terminal scale changes
        MainView.TerminalScaleChanged += OnTerminalScaleChanged;

        // Handle logo click to open config dialog
        MainView.LogoClicked += OnLogoClicked;

        // Handle drag move for borderless window
        MainView.DragMoveRequested += OnDragMoveRequested;

        // Handle exit button click
        MainView.ExitRequested += () => Close();

        // Ensure terminal always has focus for keyboard input
        Activated += (s, e) => MainView.FocusTerminal();
        GotFocus += (s, e) => MainView.FocusTerminal();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Forward all key events to terminal
        MainView.FocusTerminal();
        base.OnKeyDown(e);
    }

    private void OnDragMoveRequested(PointerPressedEventArgs e)
    {
        // Only drag on left mouse button
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTerminalScaleChanged(double scale)
    {
        // Directly set window size from terminal dimensions
        // Use Post to ensure terminal has updated its Width/Height
        Dispatcher.UIThread.Post(() =>
        {
            var termWidth = MainView.TerminalWidth;
            var termHeight = MainView.TerminalHeight;

            if (termWidth > 0 && termHeight > 0)
            {
                Width = termWidth;
                Height = termHeight;
            }
        });
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
