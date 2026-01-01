using Heh8080.Terminal;

namespace Heh8080.Tests;

public class Adm3aParserTests
{
    private readonly TerminalBuffer _buffer;
    private readonly Adm3aParser _parser;

    public Adm3aParserTests()
    {
        _buffer = new TerminalBuffer();
        _parser = new Adm3aParser(_buffer);
    }

    private void Send(string s)
    {
        foreach (char c in s)
            _parser.ProcessByte((byte)c);
    }

    private void Send(params byte[] bytes)
    {
        foreach (byte b in bytes)
            _parser.ProcessByte(b);
    }

    [Fact]
    public void PrintableCharacters_WrittenToBuffer()
    {
        Send("Hello");

        Assert.Equal('H', _buffer[0, 0].Character);
        Assert.Equal('e', _buffer[1, 0].Character);
        Assert.Equal('l', _buffer[2, 0].Character);
        Assert.Equal('l', _buffer[3, 0].Character);
        Assert.Equal('o', _buffer[4, 0].Character);
        Assert.Equal(5, _buffer.CursorX);
        Assert.Equal(0, _buffer.CursorY);
    }

    [Fact]
    public void CarriageReturn_MovesCursorToColumn0()
    {
        Send("Hello\r");

        Assert.Equal(0, _buffer.CursorX);
        Assert.Equal(0, _buffer.CursorY);
    }

    [Fact]
    public void LineFeed_MovesCursorDown()
    {
        Send("A\n");

        Assert.Equal(1, _buffer.CursorX);
        Assert.Equal(1, _buffer.CursorY);
    }

    [Fact]
    public void CrLf_MovesToStartOfNextLine()
    {
        Send("Hello\r\nWorld");

        Assert.Equal('H', _buffer[0, 0].Character);
        Assert.Equal('W', _buffer[0, 1].Character);
        Assert.Equal(5, _buffer.CursorX);
        Assert.Equal(1, _buffer.CursorY);
    }

    [Fact]
    public void Backspace_MovesCursorLeft()
    {
        Send("AB\bC");

        Assert.Equal('A', _buffer[0, 0].Character);
        Assert.Equal('C', _buffer[1, 0].Character); // Overwrote B
        Assert.Equal(2, _buffer.CursorX);
    }

    [Fact]
    public void CtrlZ_ClearsScreenAndHomesCursor()
    {
        Send("Hello");
        Send(0x1A); // Ctrl+Z

        Assert.True(_buffer[0, 0].IsEmpty);
        Assert.Equal(0, _buffer.CursorX);
        Assert.Equal(0, _buffer.CursorY);
    }

    [Fact]
    public void CtrlCaret_HomesCursor()
    {
        Send("Hello");
        Send(0x1E); // Ctrl+^

        Assert.Equal(0, _buffer.CursorX);
        Assert.Equal(0, _buffer.CursorY);
        // Screen not cleared
        Assert.Equal('H', _buffer[0, 0].Character);
    }

    [Fact]
    public void CtrlK_MovesCursorUp()
    {
        Send("A\nB");
        Send(0x0B); // Ctrl+K (cursor up)

        Assert.Equal(0, _buffer.CursorY);
    }

    [Fact]
    public void CtrlL_MovesCursorRight()
    {
        Send("A");
        Send(0x0C); // Ctrl+L (cursor right)

        Assert.Equal(2, _buffer.CursorX);
    }

    [Fact]
    public void EscEquals_SetsCursorPosition()
    {
        // ESC = row col (row and col are ASCII + 0x20)
        // Position (10, 5) = ESC = 0x25 0x2A
        Send(0x1B, (byte)'=', (byte)' ' + 5, (byte)' ' + 10);

        Assert.Equal(10, _buffer.CursorX);
        Assert.Equal(5, _buffer.CursorY);
    }

    [Fact]
    public void EscEquals_ClampsToValidRange()
    {
        // Try to position outside screen
        Send(0x1B, (byte)'=', (byte)' ' + 100, (byte)' ' + 100);

        Assert.Equal(TerminalBuffer.Width - 1, _buffer.CursorX);
        Assert.Equal(TerminalBuffer.Height - 1, _buffer.CursorY);
    }

    [Fact]
    public void EscT_ClearsToEndOfLine()
    {
        Send("Hello World");
        // Move cursor to position 5
        Send(0x1B, (byte)'=', (byte)' ', (byte)' ' + 5);
        // Clear to end of line
        Send("\x1bT");

        Assert.Equal('H', _buffer[0, 0].Character);
        Assert.Equal('o', _buffer[4, 0].Character);
        Assert.True(_buffer[5, 0].IsEmpty);
        Assert.True(_buffer[10, 0].IsEmpty);
    }

    [Fact]
    public void EscY_ClearsToEndOfScreen()
    {
        Send("Line 1\r\nLine 2\r\nLine 3");
        // Move to middle of line 2
        Send(0x1B, (byte)'=', (byte)' ' + 1, (byte)' ' + 3);
        // Clear to end of screen
        Send("\x1bY");

        Assert.Equal('L', _buffer[0, 0].Character); // Line 1 intact
        Assert.Equal('L', _buffer[0, 1].Character); // Start of line 2
        Assert.Equal('n', _buffer[2, 1].Character);
        Assert.True(_buffer[3, 1].IsEmpty); // Cleared from here
        Assert.True(_buffer[0, 2].IsEmpty); // Line 3 cleared
    }

    [Fact]
    public void EscStar_ClearsScreen()
    {
        Send("Hello");
        Send("\x1b*");

        Assert.True(_buffer[0, 0].IsEmpty);
        Assert.Equal(0, _buffer.CursorX);
        Assert.Equal(0, _buffer.CursorY);
    }

    [Fact]
    public void EscColon_ClearsScreen()
    {
        Send("Hello");
        Send("\x1b:");

        Assert.True(_buffer[0, 0].IsEmpty);
        Assert.Equal(0, _buffer.CursorX);
        Assert.Equal(0, _buffer.CursorY);
    }

    [Fact]
    public void Tab_MovesToNextTabStop()
    {
        Send("A\tB");

        Assert.Equal('A', _buffer[0, 0].Character);
        Assert.Equal('B', _buffer[8, 0].Character);
        Assert.Equal(9, _buffer.CursorX);
    }

    [Fact]
    public void LineWrap_AtColumn80()
    {
        // Fill first line
        for (int i = 0; i < 80; i++)
            _parser.ProcessByte((byte)'X');

        Assert.Equal(80, _buffer.CursorX);
        Assert.Equal(0, _buffer.CursorY);

        // One more character should wrap
        _parser.ProcessByte((byte)'Y');

        Assert.Equal(1, _buffer.CursorX);
        Assert.Equal(1, _buffer.CursorY);
        Assert.Equal('Y', _buffer[0, 1].Character);
    }

    [Fact]
    public void Scroll_WhenCursorPastBottom()
    {
        // Move to last line
        for (int i = 0; i < 23; i++)
            Send("\n");

        Assert.Equal(23, _buffer.CursorY);

        // Write something on last line
        Send("Bottom");

        // Line feed should scroll
        Send("\n");

        Assert.Equal(23, _buffer.CursorY);
        // "Bottom" should now be on line 22
        Assert.Equal('B', _buffer[0, 22].Character);
    }
}
