namespace Heh8080.Core;

/// <summary>
/// A single trace entry capturing CPU state before instruction execution.
/// Uses readonly struct for minimal allocation overhead.
/// </summary>
public readonly struct TraceEntry
{
    public readonly ushort PC;
    public readonly byte Opcode;
    public readonly byte Op1;
    public readonly byte Op2;
    public readonly byte A, B, C, D, E, H, L;
    public readonly ushort SP;
    public readonly byte Flags;

    public TraceEntry(
        ushort pc, byte opcode, byte op1, byte op2,
        byte a, byte b, byte c, byte d, byte e, byte h, byte l,
        ushort sp, byte flags)
    {
        PC = pc;
        Opcode = opcode;
        Op1 = op1;
        Op2 = op2;
        A = a;
        B = b;
        C = c;
        D = d;
        E = e;
        H = h;
        L = l;
        SP = sp;
        Flags = flags;
    }

    /// <summary>
    /// Create from CPU trace state tuple and memory for opcode bytes.
    /// </summary>
    public TraceEntry(
        (byte A, byte B, byte C, byte D, byte E, byte H, byte L, ushort SP, ushort PC, byte Flags) state,
        IMemory memory)
    {
        PC = state.PC;
        Opcode = memory.Read(state.PC);
        Op1 = memory.Read((ushort)(state.PC + 1));
        Op2 = memory.Read((ushort)(state.PC + 2));
        A = state.A;
        B = state.B;
        C = state.C;
        D = state.D;
        E = state.E;
        H = state.H;
        L = state.L;
        SP = state.SP;
        Flags = state.Flags;
    }
}
