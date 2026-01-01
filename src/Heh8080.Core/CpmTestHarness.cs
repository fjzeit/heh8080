namespace Heh8080.Core;

/// <summary>
/// Minimal CP/M BDOS emulation for running CPU test suites.
/// </summary>
/// <remarks>
/// Implements:
/// - BDOS function 2: Output character (C register to console)
/// - BDOS function 9: Output string (DE points to $-terminated string)
/// - CALL 0x0005: BDOS entry point
/// - RET from 0x0000: Program exit
/// </remarks>
public sealed class CpmTestHarness
{
    private readonly Cpu8080 _cpu;
    private readonly Memory _memory;
    private readonly IoBus _ioBus;
    private readonly Action<char> _consoleOutput;

    public bool HasExited { get; private set; }
    public long InstructionCount { get; private set; }

    public CpmTestHarness(Action<char> consoleOutput)
    {
        _memory = new Memory();
        _ioBus = new IoBus();
        _cpu = new Cpu8080(_memory, _ioBus);
        _consoleOutput = consoleOutput;

        SetupBdosTraps();
    }

    private void SetupBdosTraps()
    {
        // At address 0x0000: RET instruction for warm boot (program exit)
        _memory.Write(0x0000, 0xC9); // RET

        // At address 0x0005: BDOS entry - we'll trap CALL 0x0005
        // Put a RET there so if we don't trap it, it just returns
        _memory.Write(0x0005, 0xC9); // RET
    }

    /// <summary>
    /// Load a COM file into memory at 0x0100.
    /// </summary>
    public void LoadCom(ReadOnlySpan<byte> data)
    {
        _memory.Load(0x0100, data);
        _cpu.Reset();
        _cpu.PC = 0x0100;
        _cpu.SP = 0xFFFF; // Stack at top of memory
        HasExited = false;
        InstructionCount = 0;
    }

    /// <summary>
    /// Load a COM file from disk.
    /// </summary>
    public void LoadCom(string path)
    {
        var data = File.ReadAllBytes(path);
        LoadCom(data);
    }

    /// <summary>
    /// Run until program exits or max instructions reached.
    /// </summary>
    /// <returns>True if program exited normally, false if limit reached.</returns>
    public bool Run(long maxInstructions = long.MaxValue)
    {
        while (!HasExited && InstructionCount < maxInstructions)
        {
            Step();
        }
        return HasExited;
    }

    /// <summary>
    /// Execute one instruction with BDOS trap handling.
    /// </summary>
    public void Step()
    {
        if (HasExited) return;

        // Check for BDOS call (PC about to execute CALL 0x0005)
        // We check if PC is at an instruction that will CALL 0x0005
        // by looking at the opcode and the following bytes
        ushort pc = _cpu.PC;
        byte opcode = _memory.Read(pc);

        // CALL 0x0005 is CD 05 00
        if (opcode == 0xCD)
        {
            ushort addr = (ushort)(_memory.Read((ushort)(pc + 1)) |
                                   (_memory.Read((ushort)(pc + 2)) << 8));
            if (addr == 0x0005)
            {
                // Skip the CALL instruction
                _cpu.PC = (ushort)(pc + 3);
                HandleBdos();
                InstructionCount++;
                return;
            }
        }

        // Check for program exit (RET to 0x0000 or JP 0x0000)
        // The test programs typically end by jumping or returning to 0x0000
        if (_cpu.PC == 0x0000)
        {
            HasExited = true;
            return;
        }

        _cpu.Step();
        InstructionCount++;

        // Check if we just returned to or jumped to 0x0000
        if (_cpu.PC == 0x0000)
        {
            HasExited = true;
        }
    }

    private void HandleBdos()
    {
        byte function = _cpu.C;

        switch (function)
        {
            case 2: // C_WRITE - Output character
                _consoleOutput((char)_cpu.E);
                break;

            case 9: // C_WRITESTR - Output $-terminated string
                ushort addr = _cpu.DE;
                while (true)
                {
                    char c = (char)_memory.Read(addr++);
                    if (c == '$') break;
                    _consoleOutput(c);
                }
                break;

            default:
                // Unsupported function - ignore
                break;
        }
    }

    /// <summary>
    /// Get the CPU for inspection.
    /// </summary>
    public Cpu8080 Cpu => _cpu;

    /// <summary>
    /// Get memory for inspection.
    /// </summary>
    public Memory Memory => _memory;
}
