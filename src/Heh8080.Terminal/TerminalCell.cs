namespace Heh8080.Terminal;

/// <summary>
/// A single character cell in the terminal buffer.
/// </summary>
public struct TerminalCell
{
    /// <summary>
    /// The character displayed in this cell (0 = empty/space).
    /// </summary>
    public char Character;

    /// <summary>
    /// True if this cell has inverse video attribute.
    /// </summary>
    public bool Inverse;

    /// <summary>
    /// Returns true if the cell is empty (space or null character).
    /// </summary>
    public readonly bool IsEmpty => Character == '\0' || Character == ' ';

    /// <summary>
    /// An empty cell.
    /// </summary>
    public static readonly TerminalCell Empty = new() { Character = ' ' };
}
