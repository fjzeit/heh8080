using Avalonia.Controls;
using Heh8080.Terminal;

namespace Heh8080.Views;

public partial class MainView : UserControl
{
    private readonly Adm3aTerminal _terminal;

    public MainView()
    {
        InitializeComponent();

        // Create and attach terminal
        _terminal = new Adm3aTerminal();
        Terminal.Terminal = _terminal;

        // Display welcome message
        WriteString("heh8080 - Intel 8080 Emulator\r\n");
        WriteString("FJM-3A Terminal Ready\r\n");
        WriteString("\r\n");
        WriteString(">");
    }

    private void WriteString(string s)
    {
        foreach (char c in s)
            _terminal.WriteChar((byte)c);
    }
}
