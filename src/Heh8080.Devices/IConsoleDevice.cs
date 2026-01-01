namespace Heh8080.Devices;

/// <summary>
/// Console device interface for keyboard input and display output.
/// Used by ConsoleDevice to interact with the host environment.
/// </summary>
public interface IConsoleDevice
{
    /// <summary>
    /// Check if a character is available for reading.
    /// </summary>
    bool IsInputReady { get; }

    /// <summary>
    /// Read a character from the keyboard. Blocks if no input available.
    /// </summary>
    byte ReadChar();

    /// <summary>
    /// Write a character to the display.
    /// </summary>
    void WriteChar(byte c);
}
