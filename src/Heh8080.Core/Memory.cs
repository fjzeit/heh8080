namespace Heh8080.Core;

/// <summary>
/// 64KB memory with optional bank switching support.
/// </summary>
/// <remarks>
/// Memory layout with banking:
/// - Common area: 0x0000 to (segmentSize * 256 - 1) - shared across all banks
/// - Banked area: (segmentSize * 256) to 0xFFFF - switched per bank
///
/// Default: 192 pages (48KB) banked, 64 pages (16KB) common at top.
/// </remarks>
public sealed class Memory : IMemory
{
    private readonly byte[] _bank0 = new byte[65536];
    private byte[][]? _banks;
    private int _currentBank;
    private int _bankStart; // Address where banking begins
    private int _bankSize;  // Size of banked region
    private bool _commonWriteProtect;

    public Memory()
    {
        _bankStart = 0xC000; // Default: bank starts at 48KB
        _bankSize = 0x4000;  // 16KB banked region
    }

    public int BankCount => _banks?.Length ?? 1;
    public int CurrentBank => _currentBank;
    public int SegmentSizePages => _bankStart / 256;

    public byte Read(ushort address)
    {
        if (_banks == null || _currentBank == 0 || address < _bankStart)
        {
            return _bank0[address];
        }
        return _banks[_currentBank][address - _bankStart];
    }

    public void Write(ushort address, byte value)
    {
        if (_banks == null || _currentBank == 0 || address < _bankStart)
        {
            // Check write protection for common area (above bank start)
            if (_commonWriteProtect && address >= _bankStart)
                return;
            _bank0[address] = value;
        }
        else
        {
            _banks[_currentBank][address - _bankStart] = value;
        }
    }

    /// <summary>
    /// Initialize memory banks. Bank 0 is always the base 64KB.
    /// </summary>
    /// <param name="bankCount">Total number of banks including bank 0</param>
    public void InitializeBanks(int bankCount)
    {
        if (bankCount <= 1)
        {
            _banks = null;
            _currentBank = 0;
            return;
        }

        _banks = new byte[bankCount][];
        _banks[0] = Array.Empty<byte>(); // Bank 0 uses _bank0
        for (int i = 1; i < bankCount; i++)
        {
            _banks[i] = new byte[_bankSize];
        }
        _currentBank = 0;
    }

    /// <summary>
    /// Select the active memory bank.
    /// </summary>
    public void SelectBank(int bank)
    {
        if (_banks == null || bank < 0 || bank >= _banks.Length)
            return;
        _currentBank = bank;
    }

    /// <summary>
    /// Set the segment size (address where banking begins).
    /// Must be called before InitializeBanks.
    /// </summary>
    /// <param name="pages">Number of 256-byte pages in the banked area</param>
    public void SetSegmentSize(int pages)
    {
        _bankStart = pages * 256;
        _bankSize = 65536 - _bankStart;
    }

    /// <summary>
    /// Set write protection for the common memory area.
    /// </summary>
    public void SetWriteProtect(bool protect)
    {
        _commonWriteProtect = protect;
    }

    /// <summary>
    /// Load data into memory at the specified address.
    /// </summary>
    public void Load(ushort address, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length && address + i < 65536; i++)
        {
            _bank0[address + i] = data[i];
        }
    }

    /// <summary>
    /// Clear all memory.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_bank0);
        if (_banks != null)
        {
            for (int i = 1; i < _banks.Length; i++)
            {
                Array.Clear(_banks[i]);
            }
        }
        _currentBank = 0;
    }

    /// <summary>
    /// Direct access to bank 0 for bulk operations.
    /// </summary>
    public Span<byte> GetBank0() => _bank0;
}
