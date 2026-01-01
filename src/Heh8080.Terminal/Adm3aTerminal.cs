using System;
using System.Collections.Generic;
using Heh8080.Devices;

namespace Heh8080.Terminal;

/// <summary>
/// ADM-3A terminal emulator implementing IConsoleDevice.
/// Provides keyboard input queue and screen buffer for rendering.
/// </summary>
public class Adm3aTerminal : IConsoleDevice
{
    private readonly Queue<byte> _inputQueue = new();
    private readonly TerminalBuffer _buffer;
    private readonly Adm3aParser _parser;
    private readonly object _lock = new();

    public Adm3aTerminal()
    {
        _buffer = new TerminalBuffer();
        _parser = new Adm3aParser(_buffer);
    }

    /// <summary>
    /// The terminal buffer for rendering.
    /// </summary>
    public TerminalBuffer Buffer => _buffer;

    /// <summary>
    /// Event fired when buffer content changes.
    /// </summary>
    public event Action? ContentChanged
    {
        add => _buffer.ContentChanged += value;
        remove => _buffer.ContentChanged -= value;
    }

    #region IConsoleDevice

    /// <inheritdoc/>
    public bool IsInputReady
    {
        get
        {
            lock (_lock)
                return _inputQueue.Count > 0;
        }
    }

    /// <inheritdoc/>
    public byte ReadChar()
    {
        lock (_lock)
        {
            if (_inputQueue.Count > 0)
                return _inputQueue.Dequeue();
            return 0;
        }
    }

    /// <inheritdoc/>
    public void WriteChar(byte c)
    {
        _parser.ProcessByte(c);
    }

    #endregion

    /// <summary>
    /// Queue a character for keyboard input (from UI).
    /// </summary>
    public void QueueInput(byte c)
    {
        lock (_lock)
            _inputQueue.Enqueue(c);
    }

    /// <summary>
    /// Queue a string for keyboard input (from UI).
    /// </summary>
    public void QueueInput(string s)
    {
        lock (_lock)
        {
            foreach (char c in s)
            {
                if (c <= 0x7F)
                    _inputQueue.Enqueue((byte)c);
            }
        }
    }

    /// <summary>
    /// Clear the input queue.
    /// </summary>
    public void ClearInput()
    {
        lock (_lock)
            _inputQueue.Clear();
    }
}
