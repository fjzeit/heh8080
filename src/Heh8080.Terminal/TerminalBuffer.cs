using System;

namespace Heh8080.Terminal;

/// <summary>
/// Fixed 80x24 terminal buffer with cursor tracking.
/// </summary>
public class TerminalBuffer
{
    public const int Width = 80;
    public const int Height = 24;

    private readonly TerminalCell[] _cells = new TerminalCell[Width * Height];

    /// <summary>
    /// Cursor column (0-79).
    /// </summary>
    public int CursorX { get; set; }

    /// <summary>
    /// Cursor row (0-23).
    /// </summary>
    public int CursorY { get; set; }

    /// <summary>
    /// True if cursor should be visible.
    /// </summary>
    public bool CursorVisible { get; set; } = true;

    /// <summary>
    /// Fired when the buffer content changes.
    /// </summary>
    public event Action? ContentChanged;

    public TerminalBuffer()
    {
        Clear();
    }

    /// <summary>
    /// Get cell at position.
    /// </summary>
    public ref TerminalCell this[int x, int y]
    {
        get
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException();
            return ref _cells[y * Width + x];
        }
    }

    /// <summary>
    /// Clear the entire screen and home cursor.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _cells.Length; i++)
            _cells[i] = TerminalCell.Empty;
        CursorX = 0;
        CursorY = 0;
        RaiseContentChanged();
    }

    /// <summary>
    /// Clear from cursor to end of line.
    /// </summary>
    public void ClearToEndOfLine()
    {
        for (int x = CursorX; x < Width; x++)
            _cells[CursorY * Width + x] = TerminalCell.Empty;
        RaiseContentChanged();
    }

    /// <summary>
    /// Clear from cursor to end of screen.
    /// </summary>
    public void ClearToEndOfScreen()
    {
        // Clear rest of current line
        ClearToEndOfLine();
        // Clear all lines below
        for (int y = CursorY + 1; y < Height; y++)
            for (int x = 0; x < Width; x++)
                _cells[y * Width + x] = TerminalCell.Empty;
        RaiseContentChanged();
    }

    /// <summary>
    /// Write a character at the cursor position and advance cursor.
    /// </summary>
    public void WriteChar(char c)
    {
        if (CursorX >= Width)
        {
            // Wrap to next line
            CursorX = 0;
            CursorY++;
        }

        if (CursorY >= Height)
        {
            // Scroll up
            ScrollUp();
            CursorY = Height - 1;
        }

        _cells[CursorY * Width + CursorX].Character = c;
        CursorX++;
        RaiseContentChanged();
    }

    /// <summary>
    /// Move cursor to home position (0, 0).
    /// </summary>
    public void Home()
    {
        CursorX = 0;
        CursorY = 0;
    }

    /// <summary>
    /// Set cursor position (clamped to valid range).
    /// </summary>
    public void SetCursorPosition(int x, int y)
    {
        CursorX = Math.Clamp(x, 0, Width - 1);
        CursorY = Math.Clamp(y, 0, Height - 1);
    }

    /// <summary>
    /// Move cursor left (backspace).
    /// </summary>
    public void CursorLeft()
    {
        if (CursorX > 0)
            CursorX--;
    }

    /// <summary>
    /// Move cursor right.
    /// </summary>
    public void CursorRight()
    {
        if (CursorX < Width - 1)
            CursorX++;
    }

    /// <summary>
    /// Move cursor up.
    /// </summary>
    public void CursorUp()
    {
        if (CursorY > 0)
            CursorY--;
    }

    /// <summary>
    /// Move cursor down (may scroll).
    /// </summary>
    public void CursorDown()
    {
        CursorY++;
        if (CursorY >= Height)
        {
            ScrollUp();
            CursorY = Height - 1;
        }
    }

    /// <summary>
    /// Carriage return (move to column 0).
    /// </summary>
    public void CarriageReturn()
    {
        CursorX = 0;
    }

    /// <summary>
    /// Line feed (move down, scroll if at bottom).
    /// </summary>
    public void LineFeed()
    {
        CursorDown();
        RaiseContentChanged();
    }

    /// <summary>
    /// Scroll the screen up by one line.
    /// </summary>
    private void ScrollUp()
    {
        // Move all lines up
        Array.Copy(_cells, Width, _cells, 0, Width * (Height - 1));
        // Clear bottom line
        for (int x = 0; x < Width; x++)
            _cells[(Height - 1) * Width + x] = TerminalCell.Empty;
        RaiseContentChanged();
    }

    private void RaiseContentChanged()
    {
        ContentChanged?.Invoke();
    }
}
