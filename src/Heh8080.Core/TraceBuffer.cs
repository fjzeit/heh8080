namespace Heh8080.Core;

/// <summary>
/// Fixed-size ring buffer for trace entries.
/// Single-writer design: Add() is called from emulator thread only.
/// GetEntries() returns a copy for safe cross-thread reading.
/// </summary>
public sealed class TraceBuffer
{
    private readonly TraceEntry[] _buffer;
    private readonly int _capacity;
    private int _writeIndex;
    private int _count;

    public TraceBuffer(int capacity = 256)
    {
        _capacity = capacity;
        _buffer = new TraceEntry[capacity];
    }

    /// <summary>
    /// Number of entries currently in the buffer.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Add an entry to the buffer. Overwrites oldest entry when full.
    /// </summary>
    public void Add(in TraceEntry entry)
    {
        _buffer[_writeIndex] = entry;
        _writeIndex = (_writeIndex + 1) % _capacity;
        if (_count < _capacity)
            _count++;
    }

    /// <summary>
    /// Get all entries in chronological order (oldest first).
    /// Returns a copy for thread-safe reading.
    /// </summary>
    public TraceEntry[] GetEntries()
    {
        var result = new TraceEntry[_count];
        if (_count == 0)
            return result;

        if (_count < _capacity)
        {
            // Buffer not yet wrapped
            Array.Copy(_buffer, 0, result, 0, _count);
        }
        else
        {
            // Buffer has wrapped - oldest is at _writeIndex
            int firstPart = _capacity - _writeIndex;
            Array.Copy(_buffer, _writeIndex, result, 0, firstPart);
            Array.Copy(_buffer, 0, result, firstPart, _writeIndex);
        }

        return result;
    }

    /// <summary>
    /// Clear all entries.
    /// </summary>
    public void Clear()
    {
        _writeIndex = 0;
        _count = 0;
    }
}
