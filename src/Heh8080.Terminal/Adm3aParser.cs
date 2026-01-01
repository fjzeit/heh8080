namespace Heh8080.Terminal;

/// <summary>
/// ADM-3A terminal escape sequence parser with Kaypro/Osborne extensions.
/// </summary>
public class Adm3aParser
{
    private readonly TerminalBuffer _buffer;
    private ParserState _state = ParserState.Normal;
    private int _savedRow;

    public Adm3aParser(TerminalBuffer buffer)
    {
        _buffer = buffer;
    }

    /// <summary>
    /// Process a single byte from the CPU.
    /// </summary>
    public void ProcessByte(byte b)
    {
        switch (_state)
        {
            case ParserState.Normal:
                ProcessNormal(b);
                break;
            case ParserState.Escape:
                ProcessEscape(b);
                break;
            case ParserState.CursorRow:
                ProcessCursorRow(b);
                break;
            case ParserState.CursorCol:
                ProcessCursorCol(b);
                break;
        }
    }

    private void ProcessNormal(byte b)
    {
        switch (b)
        {
            case 0x00: // NUL - ignore
                break;
            case 0x07: // BEL - bell (ignore for now)
                break;
            case 0x08: // BS - backspace / cursor left
                _buffer.CursorLeft();
                break;
            case 0x09: // HT - tab (move to next 8-column boundary)
                int nextTab = ((_buffer.CursorX / 8) + 1) * 8;
                if (nextTab < TerminalBuffer.Width)
                    _buffer.SetCursorPosition(nextTab, _buffer.CursorY);
                break;
            case 0x0A: // LF - line feed
                _buffer.LineFeed();
                break;
            case 0x0B: // VT - cursor up (ADM-3A extension)
                _buffer.CursorUp();
                break;
            case 0x0C: // FF - cursor right (ADM-3A extension)
                _buffer.CursorRight();
                break;
            case 0x0D: // CR - carriage return
                _buffer.CarriageReturn();
                break;
            case 0x1A: // SUB (Ctrl+Z) - clear screen and home
                _buffer.Clear();
                break;
            case 0x1B: // ESC - start escape sequence
                _state = ParserState.Escape;
                break;
            case 0x1E: // RS (Ctrl+^) - home cursor
                _buffer.Home();
                break;
            default:
                // Printable character (0x20-0x7E)
                if (b >= 0x20 && b <= 0x7E)
                {
                    _buffer.WriteChar((char)b);
                }
                break;
        }
    }

    private void ProcessEscape(byte b)
    {
        switch (b)
        {
            case (byte)'=': // ESC = - cursor positioning (next two bytes are row, col)
                _state = ParserState.CursorRow;
                break;
            case (byte)'T': // ESC T - clear to end of line (Kaypro extension)
                _buffer.ClearToEndOfLine();
                _state = ParserState.Normal;
                break;
            case (byte)'Y': // ESC Y - clear to end of screen (Kaypro extension)
                _buffer.ClearToEndOfScreen();
                _state = ParserState.Normal;
                break;
            case (byte)'*': // ESC * - clear screen (Kaypro extension)
            case (byte)':': // ESC : - clear screen (alternative)
                _buffer.Clear();
                _state = ParserState.Normal;
                break;
            case (byte)'(': // ESC ( - enter inverse video mode (some terminals)
            case (byte)')': // ESC ) - exit inverse video mode (some terminals)
                // Not implementing inverse video for now
                _state = ParserState.Normal;
                break;
            default:
                // Unknown escape sequence - return to normal
                _state = ParserState.Normal;
                break;
        }
    }

    private void ProcessCursorRow(byte b)
    {
        // Row is encoded as ASCII value + 0x20 (space)
        // So row 0 = 0x20 (' '), row 1 = 0x21 ('!'), etc.
        _savedRow = b - 0x20;
        _state = ParserState.CursorCol;
    }

    private void ProcessCursorCol(byte b)
    {
        // Column is encoded the same way
        int col = b - 0x20;
        _buffer.SetCursorPosition(col, _savedRow);
        _state = ParserState.Normal;
    }

    private enum ParserState
    {
        Normal,
        Escape,
        CursorRow,
        CursorCol
    }
}
