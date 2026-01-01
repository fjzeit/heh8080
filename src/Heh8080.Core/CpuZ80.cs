namespace Heh8080.Core;

/// <summary>
/// Zilog Z80 CPU emulator with full instruction set.
/// Backward-compatible with Intel 8080.
/// </summary>
public sealed class CpuZ80 : ICpu
{
    // Main registers (8080-compatible)
    public byte A;
    public byte B, C;
    public byte D, E;
    public byte H, L;

    // Alternate register set (Z80-specific)
    public byte A_, F_;
    public byte B_, C_;
    public byte D_, E_;
    public byte H_, L_;

    // Index registers (Z80-specific)
    public ushort IX;
    public ushort IY;

    // System registers
    public ushort SP { get; set; }
    public ushort PC { get; set; }

    // Interrupt/refresh registers (Z80-specific)
    public byte I;  // Interrupt vector base
    public byte R;  // Refresh counter

    // Flags (stored individually for speed)
    public bool FlagS;   // Sign (bit 7)
    public bool FlagZ;   // Zero (bit 6)
    public bool FlagY;   // Undocumented (bit 5, copy of bit 5 of result)
    public bool FlagH;   // Half-carry (bit 4) - called AC on 8080
    public bool FlagX;   // Undocumented (bit 3, copy of bit 3 of result)
    public bool FlagPV;  // Parity/Overflow (bit 2)
    public bool FlagN;   // Add/Subtract (bit 1) - Z80 only
    public bool FlagC;   // Carry (bit 0)

    // Interrupt state
    public bool IFF1;    // Interrupt flip-flop 1
    public bool IFF2;    // Interrupt flip-flop 2
    public byte InterruptMode;  // 0, 1, or 2
    public bool Halted { get; private set; }

    public bool InterruptsEnabled => IFF1;

    // Memory and I/O
    private readonly IMemory _memory;
    private readonly IIoBus _ioBus;

    // Opcode dispatch tables
    private readonly Func<int>[] _mainOps;
    private readonly Func<int>[] _cbOps;
    private readonly Func<int>[] _edOps;
    private readonly Func<int>?[] _ddOps;
    private readonly Func<int>?[] _fdOps;

    // Parity lookup table (true = even parity)
    private static readonly bool[] ParityTable = new bool[256];

    static CpuZ80()
    {
        for (int i = 0; i < 256; i++)
        {
            int bits = 0;
            int v = i;
            while (v != 0)
            {
                bits += v & 1;
                v >>= 1;
            }
            ParityTable[i] = (bits & 1) == 0;
        }
    }

    public CpuZ80(IMemory memory, IIoBus ioBus)
    {
        _memory = memory;
        _ioBus = ioBus;

        _mainOps = new Func<int>[256];
        _cbOps = new Func<int>[256];
        _edOps = new Func<int>[256];
        _ddOps = new Func<int>?[256];
        _fdOps = new Func<int>?[256];

        InitMainOps();
        InitCbOps();
        InitEdOps();
        InitDdOps();
        InitFdOps();

        Reset();
    }

    public void Reset()
    {
        A = B = C = D = E = H = L = 0;
        A_ = F_ = B_ = C_ = D_ = E_ = H_ = L_ = 0;
        IX = IY = 0;
        SP = 0xFFFF;
        PC = 0;
        I = R = 0;
        FlagS = FlagZ = FlagY = FlagH = FlagX = FlagPV = FlagN = FlagC = false;
        IFF1 = IFF2 = false;
        InterruptMode = 0;
        Halted = false;
    }

    #region Register Pair Accessors

    public ushort BC
    {
        get => (ushort)((B << 8) | C);
        set { B = (byte)(value >> 8); C = (byte)value; }
    }

    public ushort DE
    {
        get => (ushort)((D << 8) | E);
        set { D = (byte)(value >> 8); E = (byte)value; }
    }

    public ushort HL
    {
        get => (ushort)((H << 8) | L);
        set { H = (byte)(value >> 8); L = (byte)value; }
    }

    public ushort AF
    {
        get => (ushort)((A << 8) | GetFlags());
        set { A = (byte)(value >> 8); SetFlags((byte)value); }
    }

    public ushort BC_
    {
        get => (ushort)((B_ << 8) | C_);
        set { B_ = (byte)(value >> 8); C_ = (byte)value; }
    }

    public ushort DE_
    {
        get => (ushort)((D_ << 8) | E_);
        set { D_ = (byte)(value >> 8); E_ = (byte)value; }
    }

    public ushort HL_
    {
        get => (ushort)((H_ << 8) | L_);
        set { H_ = (byte)(value >> 8); L_ = (byte)value; }
    }

    public ushort AF_
    {
        get => (ushort)((A_ << 8) | F_);
        set { A_ = (byte)(value >> 8); F_ = (byte)value; }
    }

    #endregion

    #region Flag Accessors

    public byte GetFlags()
    {
        byte f = 0;
        if (FlagS) f |= 0x80;
        if (FlagZ) f |= 0x40;
        if (FlagY) f |= 0x20;
        if (FlagH) f |= 0x10;
        if (FlagX) f |= 0x08;
        if (FlagPV) f |= 0x04;
        if (FlagN) f |= 0x02;
        if (FlagC) f |= 0x01;
        return f;
    }

    public void SetFlags(byte f)
    {
        FlagS = (f & 0x80) != 0;
        FlagZ = (f & 0x40) != 0;
        FlagY = (f & 0x20) != 0;
        FlagH = (f & 0x10) != 0;
        FlagX = (f & 0x08) != 0;
        FlagPV = (f & 0x04) != 0;
        FlagN = (f & 0x02) != 0;
        FlagC = (f & 0x01) != 0;
    }

    #endregion

    #region Memory Access

    private byte ReadByte(ushort addr) => _memory.Read(addr);
    private void WriteByte(ushort addr, byte value) => _memory.Write(addr, value);

    private ushort ReadWord(ushort addr)
    {
        byte lo = _memory.Read(addr);
        byte hi = _memory.Read((ushort)(addr + 1));
        return (ushort)((hi << 8) | lo);
    }

    private void WriteWord(ushort addr, ushort value)
    {
        _memory.Write(addr, (byte)value);
        _memory.Write((ushort)(addr + 1), (byte)(value >> 8));
    }

    private byte FetchByte()
    {
        R = (byte)((R & 0x80) | ((R + 1) & 0x7F)); // Increment lower 7 bits
        return _memory.Read(PC++);
    }

    private ushort FetchWord()
    {
        byte lo = _memory.Read(PC++);
        byte hi = _memory.Read(PC++);
        return (ushort)((hi << 8) | lo);
    }

    private void Push(ushort value)
    {
        SP -= 2;
        WriteWord(SP, value);
    }

    private ushort Pop()
    {
        ushort value = ReadWord(SP);
        SP += 2;
        return value;
    }

    #endregion

    #region Execution

    public int Step()
    {
        if (Halted) return 4;

        byte opcode = FetchByte();
        return opcode switch
        {
            0xCB => ExecuteCB(),
            0xDD => ExecuteDD(),
            0xED => ExecuteED(),
            0xFD => ExecuteFD(),
            _ => _mainOps[opcode]()
        };
    }

    private int ExecuteCB()
    {
        byte op = FetchByte();
        return _cbOps[op]();
    }

    private int ExecuteDD()
    {
        byte op = FetchByte();
        if (op == 0xCB)
        {
            return ExecuteDDCB();
        }
        return _ddOps[op]?.Invoke() ?? ExecuteMainWithIX(op);
    }

    private int ExecuteFD()
    {
        byte op = FetchByte();
        if (op == 0xCB)
        {
            return ExecuteFDCB();
        }
        return _fdOps[op]?.Invoke() ?? ExecuteMainWithIY(op);
    }

    private int ExecuteED()
    {
        byte op = FetchByte();
        return _edOps[op]();
    }

    private int ExecuteDDCB()
    {
        sbyte d = (sbyte)FetchByte();
        byte op = FetchByte();
        return ExecuteIndexedBitOp(IX, d, op);
    }

    private int ExecuteFDCB()
    {
        sbyte d = (sbyte)FetchByte();
        byte op = FetchByte();
        return ExecuteIndexedBitOp(IY, d, op);
    }

    private int ExecuteMainWithIX(byte op)
    {
        // Default: execute main opcode with IX substituted for HL
        // This handles opcodes that don't have explicit DD entries
        return _mainOps[op]();
    }

    private int ExecuteMainWithIY(byte op)
    {
        // Default: execute main opcode with IY substituted for HL
        return _mainOps[op]();
    }

    private int ExecuteIndexedBitOp(ushort indexReg, sbyte displacement, byte op)
    {
        ushort addr = (ushort)(indexReg + displacement);
        byte value = ReadByte(addr);
        int bit = (op >> 3) & 7;
        int reg = op & 7;

        if (op < 0x40)
        {
            // Rotate/shift operations
            byte result = op switch
            {
                var x when x < 0x08 => Rlc(value),
                var x when x < 0x10 => Rrc(value),
                var x when x < 0x18 => Rl(value),
                var x when x < 0x20 => Rr(value),
                var x when x < 0x28 => Sla(value),
                var x when x < 0x30 => Sra(value),
                var x when x < 0x38 => Sll(value),
                _ => Srl(value)
            };
            WriteByte(addr, result);
            if (reg != 6) SetRegister(reg, result); // Undocumented: copy to register
            return 23;
        }
        else if (op < 0x80)
        {
            // BIT b,(IX+d)
            BitTest(value, bit);
            FlagY = (addr >> 8 & 0x20) != 0;
            FlagX = (addr >> 8 & 0x08) != 0;
            return 20;
        }
        else if (op < 0xC0)
        {
            // RES b,(IX+d)
            byte result = (byte)(value & ~(1 << bit));
            WriteByte(addr, result);
            if (reg != 6) SetRegister(reg, result);
            return 23;
        }
        else
        {
            // SET b,(IX+d)
            byte result = (byte)(value | (1 << bit));
            WriteByte(addr, result);
            if (reg != 6) SetRegister(reg, result);
            return 23;
        }
    }

    public void Interrupt(byte vector)
    {
        if (!IFF1) return;
        IFF1 = IFF2 = false;
        Halted = false;

        switch (InterruptMode)
        {
            case 0:
                // Execute instruction on data bus (usually RST)
                Push(PC);
                PC = (ushort)(vector * 8);
                break;
            case 1:
                // RST 38h
                Push(PC);
                PC = 0x0038;
                break;
            case 2:
                // Vector table: address = (I << 8) | vector
                Push(PC);
                ushort tableAddr = (ushort)((I << 8) | (vector & 0xFE));
                PC = ReadWord(tableAddr);
                break;
        }
    }

    #endregion

    #region ALU Helpers

    private void SetSZP(byte value)
    {
        FlagS = (value & 0x80) != 0;
        FlagZ = value == 0;
        FlagPV = ParityTable[value];
        FlagY = (value & 0x20) != 0;
        FlagX = (value & 0x08) != 0;
    }

    private void SetSZXY(byte value)
    {
        FlagS = (value & 0x80) != 0;
        FlagZ = value == 0;
        FlagY = (value & 0x20) != 0;
        FlagX = (value & 0x08) != 0;
    }

    private byte Inc(byte value)
    {
        byte result = (byte)(value + 1);
        FlagS = (result & 0x80) != 0;
        FlagZ = result == 0;
        FlagH = (value & 0x0F) == 0x0F;
        FlagPV = value == 0x7F;
        FlagN = false;
        FlagY = (result & 0x20) != 0;
        FlagX = (result & 0x08) != 0;
        return result;
    }

    private byte Dec(byte value)
    {
        byte result = (byte)(value - 1);
        FlagS = (result & 0x80) != 0;
        FlagZ = result == 0;
        FlagH = (value & 0x0F) == 0;
        FlagPV = value == 0x80;
        FlagN = true;
        FlagY = (result & 0x20) != 0;
        FlagX = (result & 0x08) != 0;
        return result;
    }

    private void Add(byte value)
    {
        int result = A + value;
        FlagH = ((A & 0x0F) + (value & 0x0F)) > 0x0F;
        FlagPV = ((A ^ ~value) & (A ^ result) & 0x80) != 0;
        FlagC = result > 0xFF;
        FlagN = false;
        A = (byte)result;
        SetSZXY(A);
    }

    private void Adc(byte value)
    {
        int carry = FlagC ? 1 : 0;
        int result = A + value + carry;
        FlagH = ((A & 0x0F) + (value & 0x0F) + carry) > 0x0F;
        FlagPV = ((A ^ ~value) & (A ^ result) & 0x80) != 0;
        FlagC = result > 0xFF;
        FlagN = false;
        A = (byte)result;
        SetSZXY(A);
    }

    private void Sub(byte value)
    {
        int result = A - value;
        FlagH = (A & 0x0F) < (value & 0x0F);
        FlagPV = ((A ^ value) & (A ^ result) & 0x80) != 0;
        FlagC = result < 0;
        FlagN = true;
        A = (byte)result;
        SetSZXY(A);
    }

    private void Sbc(byte value)
    {
        int borrow = FlagC ? 1 : 0;
        int result = A - value - borrow;
        FlagH = (A & 0x0F) < ((value & 0x0F) + borrow);
        FlagPV = ((A ^ value) & (A ^ result) & 0x80) != 0;
        FlagC = result < 0;
        FlagN = true;
        A = (byte)result;
        SetSZXY(A);
    }

    private void And(byte value)
    {
        A &= value;
        FlagC = false;
        FlagN = false;
        FlagH = true;
        SetSZP(A);
    }

    private void Xor(byte value)
    {
        A ^= value;
        FlagC = false;
        FlagN = false;
        FlagH = false;
        SetSZP(A);
    }

    private void Or(byte value)
    {
        A |= value;
        FlagC = false;
        FlagN = false;
        FlagH = false;
        SetSZP(A);
    }

    private void Cp(byte value)
    {
        int result = A - value;
        FlagS = (result & 0x80) != 0;
        FlagZ = (byte)result == 0;
        FlagH = (A & 0x0F) < (value & 0x0F);
        FlagPV = ((A ^ value) & (A ^ result) & 0x80) != 0;
        FlagN = true;
        FlagC = result < 0;
        // X and Y flags from operand, not result
        FlagY = (value & 0x20) != 0;
        FlagX = (value & 0x08) != 0;
    }

    private byte Rlc(byte value)
    {
        FlagC = (value & 0x80) != 0;
        byte result = (byte)((value << 1) | (FlagC ? 1 : 0));
        FlagN = false;
        FlagH = false;
        SetSZP(result);
        return result;
    }

    private byte Rrc(byte value)
    {
        FlagC = (value & 0x01) != 0;
        byte result = (byte)((value >> 1) | (FlagC ? 0x80 : 0));
        FlagN = false;
        FlagH = false;
        SetSZP(result);
        return result;
    }

    private byte Rl(byte value)
    {
        bool carry = FlagC;
        FlagC = (value & 0x80) != 0;
        byte result = (byte)((value << 1) | (carry ? 1 : 0));
        FlagN = false;
        FlagH = false;
        SetSZP(result);
        return result;
    }

    private byte Rr(byte value)
    {
        bool carry = FlagC;
        FlagC = (value & 0x01) != 0;
        byte result = (byte)((value >> 1) | (carry ? 0x80 : 0));
        FlagN = false;
        FlagH = false;
        SetSZP(result);
        return result;
    }

    private byte Sla(byte value)
    {
        FlagC = (value & 0x80) != 0;
        byte result = (byte)(value << 1);
        FlagN = false;
        FlagH = false;
        SetSZP(result);
        return result;
    }

    private byte Sra(byte value)
    {
        FlagC = (value & 0x01) != 0;
        byte result = (byte)((value >> 1) | (value & 0x80));
        FlagN = false;
        FlagH = false;
        SetSZP(result);
        return result;
    }

    private byte Sll(byte value)
    {
        // Undocumented: shift left, set bit 0
        FlagC = (value & 0x80) != 0;
        byte result = (byte)((value << 1) | 1);
        FlagN = false;
        FlagH = false;
        SetSZP(result);
        return result;
    }

    private byte Srl(byte value)
    {
        FlagC = (value & 0x01) != 0;
        byte result = (byte)(value >> 1);
        FlagN = false;
        FlagH = false;
        SetSZP(result);
        return result;
    }

    private void BitTest(byte value, int bit)
    {
        bool isSet = (value & (1 << bit)) != 0;
        FlagZ = !isSet;
        FlagPV = !isSet;
        FlagS = bit == 7 && isSet;
        FlagH = true;
        FlagN = false;
        if (!FlagZ)
        {
            FlagY = bit == 5;
            FlagX = bit == 3;
        }
    }

    private byte GetRegister(int reg) => reg switch
    {
        0 => B, 1 => C, 2 => D, 3 => E,
        4 => H, 5 => L, 6 => ReadByte(HL), 7 => A,
        _ => 0
    };

    private void SetRegister(int reg, byte value)
    {
        switch (reg)
        {
            case 0: B = value; break;
            case 1: C = value; break;
            case 2: D = value; break;
            case 3: E = value; break;
            case 4: H = value; break;
            case 5: L = value; break;
            case 6: WriteByte(HL, value); break;
            case 7: A = value; break;
        }
    }

    private void AdcHl(ushort value)
    {
        int carry = FlagC ? 1 : 0;
        int result = HL + value + carry;
        FlagH = ((HL & 0x0FFF) + (value & 0x0FFF) + carry) > 0x0FFF;
        FlagPV = ((HL ^ ~value) & (HL ^ result) & 0x8000) != 0;
        FlagC = result > 0xFFFF;
        FlagN = false;
        HL = (ushort)result;
        FlagS = (HL & 0x8000) != 0;
        FlagZ = HL == 0;
        FlagY = (H & 0x20) != 0;
        FlagX = (H & 0x08) != 0;
    }

    private void SbcHl(ushort value)
    {
        int borrow = FlagC ? 1 : 0;
        int result = HL - value - borrow;
        FlagH = (HL & 0x0FFF) < ((value & 0x0FFF) + borrow);
        FlagPV = ((HL ^ value) & (HL ^ result) & 0x8000) != 0;
        FlagC = result < 0;
        FlagN = true;
        HL = (ushort)result;
        FlagS = (HL & 0x8000) != 0;
        FlagZ = HL == 0;
        FlagY = (H & 0x20) != 0;
        FlagX = (H & 0x08) != 0;
    }

    #endregion

    #region Opcode Table Initialization

    private void InitMainOps()
    {
        // Initialize all to NOP first
        for (int i = 0; i < 256; i++)
        {
            int op = i;
            _mainOps[i] = () => 4; // NOP default
        }

        // NOP
        _mainOps[0x00] = () => 4;

        // LXI - Load register pair immediate
        _mainOps[0x01] = () => { BC = FetchWord(); return 10; };
        _mainOps[0x11] = () => { DE = FetchWord(); return 10; };
        _mainOps[0x21] = () => { HL = FetchWord(); return 10; };
        _mainOps[0x31] = () => { SP = FetchWord(); return 10; };

        // STAX/LDAX
        _mainOps[0x02] = () => { WriteByte(BC, A); return 7; };
        _mainOps[0x12] = () => { WriteByte(DE, A); return 7; };
        _mainOps[0x0A] = () => { A = ReadByte(BC); return 7; };
        _mainOps[0x1A] = () => { A = ReadByte(DE); return 7; };

        // INX/DCX
        _mainOps[0x03] = () => { BC++; return 6; };
        _mainOps[0x13] = () => { DE++; return 6; };
        _mainOps[0x23] = () => { HL++; return 6; };
        _mainOps[0x33] = () => { SP++; return 6; };
        _mainOps[0x0B] = () => { BC--; return 6; };
        _mainOps[0x1B] = () => { DE--; return 6; };
        _mainOps[0x2B] = () => { HL--; return 6; };
        _mainOps[0x3B] = () => { SP--; return 6; };

        // INR/DCR
        _mainOps[0x04] = () => { B = Inc(B); return 4; };
        _mainOps[0x0C] = () => { C = Inc(C); return 4; };
        _mainOps[0x14] = () => { D = Inc(D); return 4; };
        _mainOps[0x1C] = () => { E = Inc(E); return 4; };
        _mainOps[0x24] = () => { H = Inc(H); return 4; };
        _mainOps[0x2C] = () => { L = Inc(L); return 4; };
        _mainOps[0x34] = () => { WriteByte(HL, Inc(ReadByte(HL))); return 11; };
        _mainOps[0x3C] = () => { A = Inc(A); return 4; };
        _mainOps[0x05] = () => { B = Dec(B); return 4; };
        _mainOps[0x0D] = () => { C = Dec(C); return 4; };
        _mainOps[0x15] = () => { D = Dec(D); return 4; };
        _mainOps[0x1D] = () => { E = Dec(E); return 4; };
        _mainOps[0x25] = () => { H = Dec(H); return 4; };
        _mainOps[0x2D] = () => { L = Dec(L); return 4; };
        _mainOps[0x35] = () => { WriteByte(HL, Dec(ReadByte(HL))); return 11; };
        _mainOps[0x3D] = () => { A = Dec(A); return 4; };

        // MVI
        _mainOps[0x06] = () => { B = FetchByte(); return 7; };
        _mainOps[0x0E] = () => { C = FetchByte(); return 7; };
        _mainOps[0x16] = () => { D = FetchByte(); return 7; };
        _mainOps[0x1E] = () => { E = FetchByte(); return 7; };
        _mainOps[0x26] = () => { H = FetchByte(); return 7; };
        _mainOps[0x2E] = () => { L = FetchByte(); return 7; };
        _mainOps[0x36] = () => { WriteByte(HL, FetchByte()); return 10; };
        _mainOps[0x3E] = () => { A = FetchByte(); return 7; };

        // Rotate A instructions
        _mainOps[0x07] = () => { // RLCA
            FlagC = (A & 0x80) != 0;
            A = (byte)((A << 1) | (FlagC ? 1 : 0));
            FlagH = false; FlagN = false;
            FlagY = (A & 0x20) != 0; FlagX = (A & 0x08) != 0;
            return 4;
        };
        _mainOps[0x0F] = () => { // RRCA
            FlagC = (A & 0x01) != 0;
            A = (byte)((A >> 1) | (FlagC ? 0x80 : 0));
            FlagH = false; FlagN = false;
            FlagY = (A & 0x20) != 0; FlagX = (A & 0x08) != 0;
            return 4;
        };
        _mainOps[0x17] = () => { // RLA
            bool carry = FlagC;
            FlagC = (A & 0x80) != 0;
            A = (byte)((A << 1) | (carry ? 1 : 0));
            FlagH = false; FlagN = false;
            FlagY = (A & 0x20) != 0; FlagX = (A & 0x08) != 0;
            return 4;
        };
        _mainOps[0x1F] = () => { // RRA
            bool carry = FlagC;
            FlagC = (A & 0x01) != 0;
            A = (byte)((A >> 1) | (carry ? 0x80 : 0));
            FlagH = false; FlagN = false;
            FlagY = (A & 0x20) != 0; FlagX = (A & 0x08) != 0;
            return 4;
        };

        // Z80-specific: EX AF,AF' and relative jumps
        _mainOps[0x08] = () => { // EX AF,AF'
            ushort temp = AF;
            AF = AF_;
            AF_ = temp;
            return 4;
        };
        _mainOps[0x10] = () => { // DJNZ
            sbyte d = (sbyte)FetchByte();
            B--;
            if (B != 0) { PC = (ushort)(PC + d); return 13; }
            return 8;
        };
        _mainOps[0x18] = () => { // JR
            sbyte d = (sbyte)FetchByte();
            PC = (ushort)(PC + d);
            return 12;
        };
        _mainOps[0x20] = () => { // JR NZ
            sbyte d = (sbyte)FetchByte();
            if (!FlagZ) { PC = (ushort)(PC + d); return 12; }
            return 7;
        };
        _mainOps[0x28] = () => { // JR Z
            sbyte d = (sbyte)FetchByte();
            if (FlagZ) { PC = (ushort)(PC + d); return 12; }
            return 7;
        };
        _mainOps[0x30] = () => { // JR NC
            sbyte d = (sbyte)FetchByte();
            if (!FlagC) { PC = (ushort)(PC + d); return 12; }
            return 7;
        };
        _mainOps[0x38] = () => { // JR C
            sbyte d = (sbyte)FetchByte();
            if (FlagC) { PC = (ushort)(PC + d); return 12; }
            return 7;
        };

        // DAD (ADD HL,rr)
        _mainOps[0x09] = () => { int r = HL + BC; FlagH = ((HL & 0x0FFF) + (BC & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; HL = (ushort)r; FlagY = (H & 0x20) != 0; FlagX = (H & 0x08) != 0; return 11; };
        _mainOps[0x19] = () => { int r = HL + DE; FlagH = ((HL & 0x0FFF) + (DE & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; HL = (ushort)r; FlagY = (H & 0x20) != 0; FlagX = (H & 0x08) != 0; return 11; };
        _mainOps[0x29] = () => { int r = HL + HL; FlagH = ((HL & 0x0FFF) + (HL & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; HL = (ushort)r; FlagY = (H & 0x20) != 0; FlagX = (H & 0x08) != 0; return 11; };
        _mainOps[0x39] = () => { int r = HL + SP; FlagH = ((HL & 0x0FFF) + (SP & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; HL = (ushort)r; FlagY = (H & 0x20) != 0; FlagX = (H & 0x08) != 0; return 11; };

        // SHLD/LHLD
        _mainOps[0x22] = () => { WriteWord(FetchWord(), HL); return 16; };
        _mainOps[0x2A] = () => { HL = ReadWord(FetchWord()); return 16; };

        // STA/LDA
        _mainOps[0x32] = () => { WriteByte(FetchWord(), A); return 13; };
        _mainOps[0x3A] = () => { A = ReadByte(FetchWord()); return 13; };

        // DAA, CPL, SCF, CCF
        _mainOps[0x27] = () => { Daa(); return 4; };
        _mainOps[0x2F] = () => { // CPL
            A = (byte)~A;
            FlagH = true; FlagN = true;
            FlagY = (A & 0x20) != 0; FlagX = (A & 0x08) != 0;
            return 4;
        };
        _mainOps[0x37] = () => { // SCF
            FlagC = true; FlagH = false; FlagN = false;
            FlagY = (A & 0x20) != 0; FlagX = (A & 0x08) != 0;
            return 4;
        };
        _mainOps[0x3F] = () => { // CCF
            FlagH = FlagC; FlagC = !FlagC; FlagN = false;
            FlagY = (A & 0x20) != 0; FlagX = (A & 0x08) != 0;
            return 4;
        };

        // MOV instructions (0x40-0x7F)
        InitMovOps();

        // ALU operations (0x80-0xBF)
        InitAluOps();

        // Control flow and misc (0xC0-0xFF)
        InitControlOps();
    }

    private void InitMovOps()
    {
        // MOV B,r
        _mainOps[0x40] = () => 4; // B = B
        _mainOps[0x41] = () => { B = C; return 4; };
        _mainOps[0x42] = () => { B = D; return 4; };
        _mainOps[0x43] = () => { B = E; return 4; };
        _mainOps[0x44] = () => { B = H; return 4; };
        _mainOps[0x45] = () => { B = L; return 4; };
        _mainOps[0x46] = () => { B = ReadByte(HL); return 7; };
        _mainOps[0x47] = () => { B = A; return 4; };

        // MOV C,r
        _mainOps[0x48] = () => { C = B; return 4; };
        _mainOps[0x49] = () => 4; // C = C
        _mainOps[0x4A] = () => { C = D; return 4; };
        _mainOps[0x4B] = () => { C = E; return 4; };
        _mainOps[0x4C] = () => { C = H; return 4; };
        _mainOps[0x4D] = () => { C = L; return 4; };
        _mainOps[0x4E] = () => { C = ReadByte(HL); return 7; };
        _mainOps[0x4F] = () => { C = A; return 4; };

        // MOV D,r
        _mainOps[0x50] = () => { D = B; return 4; };
        _mainOps[0x51] = () => { D = C; return 4; };
        _mainOps[0x52] = () => 4; // D = D
        _mainOps[0x53] = () => { D = E; return 4; };
        _mainOps[0x54] = () => { D = H; return 4; };
        _mainOps[0x55] = () => { D = L; return 4; };
        _mainOps[0x56] = () => { D = ReadByte(HL); return 7; };
        _mainOps[0x57] = () => { D = A; return 4; };

        // MOV E,r
        _mainOps[0x58] = () => { E = B; return 4; };
        _mainOps[0x59] = () => { E = C; return 4; };
        _mainOps[0x5A] = () => { E = D; return 4; };
        _mainOps[0x5B] = () => 4; // E = E
        _mainOps[0x5C] = () => { E = H; return 4; };
        _mainOps[0x5D] = () => { E = L; return 4; };
        _mainOps[0x5E] = () => { E = ReadByte(HL); return 7; };
        _mainOps[0x5F] = () => { E = A; return 4; };

        // MOV H,r
        _mainOps[0x60] = () => { H = B; return 4; };
        _mainOps[0x61] = () => { H = C; return 4; };
        _mainOps[0x62] = () => { H = D; return 4; };
        _mainOps[0x63] = () => { H = E; return 4; };
        _mainOps[0x64] = () => 4; // H = H
        _mainOps[0x65] = () => { H = L; return 4; };
        _mainOps[0x66] = () => { H = ReadByte(HL); return 7; };
        _mainOps[0x67] = () => { H = A; return 4; };

        // MOV L,r
        _mainOps[0x68] = () => { L = B; return 4; };
        _mainOps[0x69] = () => { L = C; return 4; };
        _mainOps[0x6A] = () => { L = D; return 4; };
        _mainOps[0x6B] = () => { L = E; return 4; };
        _mainOps[0x6C] = () => { L = H; return 4; };
        _mainOps[0x6D] = () => 4; // L = L
        _mainOps[0x6E] = () => { L = ReadByte(HL); return 7; };
        _mainOps[0x6F] = () => { L = A; return 4; };

        // MOV (HL),r
        _mainOps[0x70] = () => { WriteByte(HL, B); return 7; };
        _mainOps[0x71] = () => { WriteByte(HL, C); return 7; };
        _mainOps[0x72] = () => { WriteByte(HL, D); return 7; };
        _mainOps[0x73] = () => { WriteByte(HL, E); return 7; };
        _mainOps[0x74] = () => { WriteByte(HL, H); return 7; };
        _mainOps[0x75] = () => { WriteByte(HL, L); return 7; };
        _mainOps[0x76] = () => { Halted = true; return 4; }; // HALT
        _mainOps[0x77] = () => { WriteByte(HL, A); return 7; };

        // MOV A,r
        _mainOps[0x78] = () => { A = B; return 4; };
        _mainOps[0x79] = () => { A = C; return 4; };
        _mainOps[0x7A] = () => { A = D; return 4; };
        _mainOps[0x7B] = () => { A = E; return 4; };
        _mainOps[0x7C] = () => { A = H; return 4; };
        _mainOps[0x7D] = () => { A = L; return 4; };
        _mainOps[0x7E] = () => { A = ReadByte(HL); return 7; };
        _mainOps[0x7F] = () => 4; // A = A
    }

    private void InitAluOps()
    {
        // ADD A,r
        _mainOps[0x80] = () => { Add(B); return 4; };
        _mainOps[0x81] = () => { Add(C); return 4; };
        _mainOps[0x82] = () => { Add(D); return 4; };
        _mainOps[0x83] = () => { Add(E); return 4; };
        _mainOps[0x84] = () => { Add(H); return 4; };
        _mainOps[0x85] = () => { Add(L); return 4; };
        _mainOps[0x86] = () => { Add(ReadByte(HL)); return 7; };
        _mainOps[0x87] = () => { Add(A); return 4; };

        // ADC A,r
        _mainOps[0x88] = () => { Adc(B); return 4; };
        _mainOps[0x89] = () => { Adc(C); return 4; };
        _mainOps[0x8A] = () => { Adc(D); return 4; };
        _mainOps[0x8B] = () => { Adc(E); return 4; };
        _mainOps[0x8C] = () => { Adc(H); return 4; };
        _mainOps[0x8D] = () => { Adc(L); return 4; };
        _mainOps[0x8E] = () => { Adc(ReadByte(HL)); return 7; };
        _mainOps[0x8F] = () => { Adc(A); return 4; };

        // SUB r
        _mainOps[0x90] = () => { Sub(B); return 4; };
        _mainOps[0x91] = () => { Sub(C); return 4; };
        _mainOps[0x92] = () => { Sub(D); return 4; };
        _mainOps[0x93] = () => { Sub(E); return 4; };
        _mainOps[0x94] = () => { Sub(H); return 4; };
        _mainOps[0x95] = () => { Sub(L); return 4; };
        _mainOps[0x96] = () => { Sub(ReadByte(HL)); return 7; };
        _mainOps[0x97] = () => { Sub(A); return 4; };

        // SBC A,r
        _mainOps[0x98] = () => { Sbc(B); return 4; };
        _mainOps[0x99] = () => { Sbc(C); return 4; };
        _mainOps[0x9A] = () => { Sbc(D); return 4; };
        _mainOps[0x9B] = () => { Sbc(E); return 4; };
        _mainOps[0x9C] = () => { Sbc(H); return 4; };
        _mainOps[0x9D] = () => { Sbc(L); return 4; };
        _mainOps[0x9E] = () => { Sbc(ReadByte(HL)); return 7; };
        _mainOps[0x9F] = () => { Sbc(A); return 4; };

        // AND r
        _mainOps[0xA0] = () => { And(B); return 4; };
        _mainOps[0xA1] = () => { And(C); return 4; };
        _mainOps[0xA2] = () => { And(D); return 4; };
        _mainOps[0xA3] = () => { And(E); return 4; };
        _mainOps[0xA4] = () => { And(H); return 4; };
        _mainOps[0xA5] = () => { And(L); return 4; };
        _mainOps[0xA6] = () => { And(ReadByte(HL)); return 7; };
        _mainOps[0xA7] = () => { And(A); return 4; };

        // XOR r
        _mainOps[0xA8] = () => { Xor(B); return 4; };
        _mainOps[0xA9] = () => { Xor(C); return 4; };
        _mainOps[0xAA] = () => { Xor(D); return 4; };
        _mainOps[0xAB] = () => { Xor(E); return 4; };
        _mainOps[0xAC] = () => { Xor(H); return 4; };
        _mainOps[0xAD] = () => { Xor(L); return 4; };
        _mainOps[0xAE] = () => { Xor(ReadByte(HL)); return 7; };
        _mainOps[0xAF] = () => { Xor(A); return 4; };

        // OR r
        _mainOps[0xB0] = () => { Or(B); return 4; };
        _mainOps[0xB1] = () => { Or(C); return 4; };
        _mainOps[0xB2] = () => { Or(D); return 4; };
        _mainOps[0xB3] = () => { Or(E); return 4; };
        _mainOps[0xB4] = () => { Or(H); return 4; };
        _mainOps[0xB5] = () => { Or(L); return 4; };
        _mainOps[0xB6] = () => { Or(ReadByte(HL)); return 7; };
        _mainOps[0xB7] = () => { Or(A); return 4; };

        // CP r
        _mainOps[0xB8] = () => { Cp(B); return 4; };
        _mainOps[0xB9] = () => { Cp(C); return 4; };
        _mainOps[0xBA] = () => { Cp(D); return 4; };
        _mainOps[0xBB] = () => { Cp(E); return 4; };
        _mainOps[0xBC] = () => { Cp(H); return 4; };
        _mainOps[0xBD] = () => { Cp(L); return 4; };
        _mainOps[0xBE] = () => { Cp(ReadByte(HL)); return 7; };
        _mainOps[0xBF] = () => { Cp(A); return 4; };
    }

    private void InitControlOps()
    {
        // Conditional returns
        _mainOps[0xC0] = () => { if (!FlagZ) { PC = Pop(); return 11; } return 5; }; // RET NZ
        _mainOps[0xC8] = () => { if (FlagZ) { PC = Pop(); return 11; } return 5; };  // RET Z
        _mainOps[0xD0] = () => { if (!FlagC) { PC = Pop(); return 11; } return 5; }; // RET NC
        _mainOps[0xD8] = () => { if (FlagC) { PC = Pop(); return 11; } return 5; };  // RET C
        _mainOps[0xE0] = () => { if (!FlagPV) { PC = Pop(); return 11; } return 5; }; // RET PO
        _mainOps[0xE8] = () => { if (FlagPV) { PC = Pop(); return 11; } return 5; };  // RET PE
        _mainOps[0xF0] = () => { if (!FlagS) { PC = Pop(); return 11; } return 5; }; // RET P
        _mainOps[0xF8] = () => { if (FlagS) { PC = Pop(); return 11; } return 5; };  // RET M

        // POP
        _mainOps[0xC1] = () => { BC = Pop(); return 10; };
        _mainOps[0xD1] = () => { DE = Pop(); return 10; };
        _mainOps[0xE1] = () => { HL = Pop(); return 10; };
        _mainOps[0xF1] = () => { AF = Pop(); return 10; };

        // Conditional jumps
        _mainOps[0xC2] = () => { ushort addr = FetchWord(); if (!FlagZ) PC = addr; return 10; }; // JP NZ
        _mainOps[0xCA] = () => { ushort addr = FetchWord(); if (FlagZ) PC = addr; return 10; };  // JP Z
        _mainOps[0xD2] = () => { ushort addr = FetchWord(); if (!FlagC) PC = addr; return 10; }; // JP NC
        _mainOps[0xDA] = () => { ushort addr = FetchWord(); if (FlagC) PC = addr; return 10; };  // JP C
        _mainOps[0xE2] = () => { ushort addr = FetchWord(); if (!FlagPV) PC = addr; return 10; }; // JP PO
        _mainOps[0xEA] = () => { ushort addr = FetchWord(); if (FlagPV) PC = addr; return 10; };  // JP PE
        _mainOps[0xF2] = () => { ushort addr = FetchWord(); if (!FlagS) PC = addr; return 10; }; // JP P
        _mainOps[0xFA] = () => { ushort addr = FetchWord(); if (FlagS) PC = addr; return 10; };  // JP M

        // JP
        _mainOps[0xC3] = () => { PC = FetchWord(); return 10; };

        // OUT (n),A
        _mainOps[0xD3] = () => { _ioBus.Out(FetchByte(), A); return 11; };

        // IN A,(n)
        _mainOps[0xDB] = () => { A = _ioBus.In(FetchByte()); return 11; };

        // EX (SP),HL
        _mainOps[0xE3] = () => {
            ushort temp = ReadWord(SP);
            WriteWord(SP, HL);
            HL = temp;
            return 19;
        };

        // JP (HL)
        _mainOps[0xE9] = () => { PC = HL; return 4; };

        // EX DE,HL
        _mainOps[0xEB] = () => { ushort temp = DE; DE = HL; HL = temp; return 4; };

        // DI/EI
        _mainOps[0xF3] = () => { IFF1 = IFF2 = false; return 4; };
        _mainOps[0xFB] = () => { IFF1 = IFF2 = true; return 4; };

        // LD SP,HL
        _mainOps[0xF9] = () => { SP = HL; return 6; };

        // Conditional calls
        _mainOps[0xC4] = () => { ushort addr = FetchWord(); if (!FlagZ) { Push(PC); PC = addr; return 17; } return 10; }; // CALL NZ
        _mainOps[0xCC] = () => { ushort addr = FetchWord(); if (FlagZ) { Push(PC); PC = addr; return 17; } return 10; };  // CALL Z
        _mainOps[0xD4] = () => { ushort addr = FetchWord(); if (!FlagC) { Push(PC); PC = addr; return 17; } return 10; }; // CALL NC
        _mainOps[0xDC] = () => { ushort addr = FetchWord(); if (FlagC) { Push(PC); PC = addr; return 17; } return 10; };  // CALL C
        _mainOps[0xE4] = () => { ushort addr = FetchWord(); if (!FlagPV) { Push(PC); PC = addr; return 17; } return 10; }; // CALL PO
        _mainOps[0xEC] = () => { ushort addr = FetchWord(); if (FlagPV) { Push(PC); PC = addr; return 17; } return 10; };  // CALL PE
        _mainOps[0xF4] = () => { ushort addr = FetchWord(); if (!FlagS) { Push(PC); PC = addr; return 17; } return 10; }; // CALL P
        _mainOps[0xFC] = () => { ushort addr = FetchWord(); if (FlagS) { Push(PC); PC = addr; return 17; } return 10; };  // CALL M

        // PUSH
        _mainOps[0xC5] = () => { Push(BC); return 11; };
        _mainOps[0xD5] = () => { Push(DE); return 11; };
        _mainOps[0xE5] = () => { Push(HL); return 11; };
        _mainOps[0xF5] = () => { Push(AF); return 11; };

        // Immediate arithmetic
        _mainOps[0xC6] = () => { Add(FetchByte()); return 7; }; // ADD A,n
        _mainOps[0xCE] = () => { Adc(FetchByte()); return 7; }; // ADC A,n
        _mainOps[0xD6] = () => { Sub(FetchByte()); return 7; }; // SUB n
        _mainOps[0xDE] = () => { Sbc(FetchByte()); return 7; }; // SBC A,n
        _mainOps[0xE6] = () => { And(FetchByte()); return 7; }; // AND n
        _mainOps[0xEE] = () => { Xor(FetchByte()); return 7; }; // XOR n
        _mainOps[0xF6] = () => { Or(FetchByte()); return 7; };  // OR n
        _mainOps[0xFE] = () => { Cp(FetchByte()); return 7; };  // CP n

        // RST
        _mainOps[0xC7] = () => { Push(PC); PC = 0x00; return 11; };
        _mainOps[0xCF] = () => { Push(PC); PC = 0x08; return 11; };
        _mainOps[0xD7] = () => { Push(PC); PC = 0x10; return 11; };
        _mainOps[0xDF] = () => { Push(PC); PC = 0x18; return 11; };
        _mainOps[0xE7] = () => { Push(PC); PC = 0x20; return 11; };
        _mainOps[0xEF] = () => { Push(PC); PC = 0x28; return 11; };
        _mainOps[0xF7] = () => { Push(PC); PC = 0x30; return 11; };
        _mainOps[0xFF] = () => { Push(PC); PC = 0x38; return 11; };

        // CALL
        _mainOps[0xCD] = () => { ushort addr = FetchWord(); Push(PC); PC = addr; return 17; };

        // RET
        _mainOps[0xC9] = () => { PC = Pop(); return 10; };

        // EXX
        _mainOps[0xD9] = () => {
            ushort tempBC = BC; BC = BC_; BC_ = tempBC;
            ushort tempDE = DE; DE = DE_; DE_ = tempDE;
            ushort tempHL = HL; HL = HL_; HL_ = tempHL;
            return 4;
        };
    }

    private void InitCbOps()
    {
        // Initialize all CB opcodes
        for (int i = 0; i < 256; i++)
        {
            int op = i;
            int reg = op & 7;
            int bit = (op >> 3) & 7;

            if (op < 0x08) // RLC r
            {
                _cbOps[op] = () => { SetRegister(reg, Rlc(GetRegister(reg))); return reg == 6 ? 15 : 8; };
            }
            else if (op < 0x10) // RRC r
            {
                _cbOps[op] = () => { SetRegister(reg, Rrc(GetRegister(reg))); return reg == 6 ? 15 : 8; };
            }
            else if (op < 0x18) // RL r
            {
                _cbOps[op] = () => { SetRegister(reg, Rl(GetRegister(reg))); return reg == 6 ? 15 : 8; };
            }
            else if (op < 0x20) // RR r
            {
                _cbOps[op] = () => { SetRegister(reg, Rr(GetRegister(reg))); return reg == 6 ? 15 : 8; };
            }
            else if (op < 0x28) // SLA r
            {
                _cbOps[op] = () => { SetRegister(reg, Sla(GetRegister(reg))); return reg == 6 ? 15 : 8; };
            }
            else if (op < 0x30) // SRA r
            {
                _cbOps[op] = () => { SetRegister(reg, Sra(GetRegister(reg))); return reg == 6 ? 15 : 8; };
            }
            else if (op < 0x38) // SLL r (undocumented)
            {
                _cbOps[op] = () => { SetRegister(reg, Sll(GetRegister(reg))); return reg == 6 ? 15 : 8; };
            }
            else if (op < 0x40) // SRL r
            {
                _cbOps[op] = () => { SetRegister(reg, Srl(GetRegister(reg))); return reg == 6 ? 15 : 8; };
            }
            else if (op < 0x80) // BIT b,r
            {
                _cbOps[op] = () => {
                    byte val = GetRegister(reg);
                    BitTest(val, bit);
                    if (reg != 6) { FlagY = (val & 0x20) != 0; FlagX = (val & 0x08) != 0; }
                    return reg == 6 ? 12 : 8;
                };
            }
            else if (op < 0xC0) // RES b,r
            {
                _cbOps[op] = () => {
                    byte val = GetRegister(reg);
                    SetRegister(reg, (byte)(val & ~(1 << bit)));
                    return reg == 6 ? 15 : 8;
                };
            }
            else // SET b,r
            {
                _cbOps[op] = () => {
                    byte val = GetRegister(reg);
                    SetRegister(reg, (byte)(val | (1 << bit)));
                    return reg == 6 ? 15 : 8;
                };
            }
        }
    }

    private void InitEdOps()
    {
        // Initialize all ED opcodes to NOP (8 cycles)
        for (int i = 0; i < 256; i++)
        {
            _edOps[i] = () => 8;
        }

        // IN r,(C)
        _edOps[0x40] = () => { B = InC(); return 12; };
        _edOps[0x48] = () => { C = InC(); return 12; };
        _edOps[0x50] = () => { D = InC(); return 12; };
        _edOps[0x58] = () => { E = InC(); return 12; };
        _edOps[0x60] = () => { H = InC(); return 12; };
        _edOps[0x68] = () => { L = InC(); return 12; };
        _edOps[0x70] = () => { InC(); return 12; }; // Affects flags only
        _edOps[0x78] = () => { A = InC(); return 12; };

        // OUT (C),r
        _edOps[0x41] = () => { _ioBus.Out(C, B); return 12; };
        _edOps[0x49] = () => { _ioBus.Out(C, C); return 12; };
        _edOps[0x51] = () => { _ioBus.Out(C, D); return 12; };
        _edOps[0x59] = () => { _ioBus.Out(C, E); return 12; };
        _edOps[0x61] = () => { _ioBus.Out(C, H); return 12; };
        _edOps[0x69] = () => { _ioBus.Out(C, L); return 12; };
        _edOps[0x71] = () => { _ioBus.Out(C, 0); return 12; }; // Outputs 0
        _edOps[0x79] = () => { _ioBus.Out(C, A); return 12; };

        // SBC HL,rr
        _edOps[0x42] = () => { SbcHl(BC); return 15; };
        _edOps[0x52] = () => { SbcHl(DE); return 15; };
        _edOps[0x62] = () => { SbcHl(HL); return 15; };
        _edOps[0x72] = () => { SbcHl(SP); return 15; };

        // ADC HL,rr
        _edOps[0x4A] = () => { AdcHl(BC); return 15; };
        _edOps[0x5A] = () => { AdcHl(DE); return 15; };
        _edOps[0x6A] = () => { AdcHl(HL); return 15; };
        _edOps[0x7A] = () => { AdcHl(SP); return 15; };

        // LD (nn),rr
        _edOps[0x43] = () => { WriteWord(FetchWord(), BC); return 20; };
        _edOps[0x53] = () => { WriteWord(FetchWord(), DE); return 20; };
        _edOps[0x63] = () => { WriteWord(FetchWord(), HL); return 20; };
        _edOps[0x73] = () => { WriteWord(FetchWord(), SP); return 20; };

        // LD rr,(nn)
        _edOps[0x4B] = () => { BC = ReadWord(FetchWord()); return 20; };
        _edOps[0x5B] = () => { DE = ReadWord(FetchWord()); return 20; };
        _edOps[0x6B] = () => { HL = ReadWord(FetchWord()); return 20; };
        _edOps[0x7B] = () => { SP = ReadWord(FetchWord()); return 20; };

        // NEG
        _edOps[0x44] = () => { Neg(); return 8; };
        _edOps[0x4C] = () => { Neg(); return 8; }; // Undocumented
        _edOps[0x54] = () => { Neg(); return 8; }; // Undocumented
        _edOps[0x5C] = () => { Neg(); return 8; }; // Undocumented
        _edOps[0x64] = () => { Neg(); return 8; }; // Undocumented
        _edOps[0x6C] = () => { Neg(); return 8; }; // Undocumented
        _edOps[0x74] = () => { Neg(); return 8; }; // Undocumented
        _edOps[0x7C] = () => { Neg(); return 8; }; // Undocumented

        // RETN/RETI
        _edOps[0x45] = () => { IFF1 = IFF2; PC = Pop(); return 14; }; // RETN
        _edOps[0x4D] = () => { IFF1 = IFF2; PC = Pop(); return 14; }; // RETI
        _edOps[0x55] = () => { IFF1 = IFF2; PC = Pop(); return 14; }; // RETN
        _edOps[0x5D] = () => { IFF1 = IFF2; PC = Pop(); return 14; }; // RETN
        _edOps[0x65] = () => { IFF1 = IFF2; PC = Pop(); return 14; }; // RETN
        _edOps[0x6D] = () => { IFF1 = IFF2; PC = Pop(); return 14; }; // RETN
        _edOps[0x75] = () => { IFF1 = IFF2; PC = Pop(); return 14; }; // RETN
        _edOps[0x7D] = () => { IFF1 = IFF2; PC = Pop(); return 14; }; // RETN

        // IM n
        _edOps[0x46] = () => { InterruptMode = 0; return 8; };
        _edOps[0x4E] = () => { InterruptMode = 0; return 8; }; // Undocumented
        _edOps[0x56] = () => { InterruptMode = 1; return 8; };
        _edOps[0x5E] = () => { InterruptMode = 2; return 8; };
        _edOps[0x66] = () => { InterruptMode = 0; return 8; }; // Undocumented
        _edOps[0x6E] = () => { InterruptMode = 0; return 8; }; // Undocumented
        _edOps[0x76] = () => { InterruptMode = 1; return 8; }; // Undocumented
        _edOps[0x7E] = () => { InterruptMode = 2; return 8; }; // Undocumented

        // LD I,A / LD R,A / LD A,I / LD A,R
        _edOps[0x47] = () => { I = A; return 9; };
        _edOps[0x4F] = () => { R = A; return 9; };
        _edOps[0x57] = () => { // LD A,I
            A = I;
            FlagS = (A & 0x80) != 0;
            FlagZ = A == 0;
            FlagH = false;
            FlagPV = IFF2;
            FlagN = false;
            FlagY = (A & 0x20) != 0;
            FlagX = (A & 0x08) != 0;
            return 9;
        };
        _edOps[0x5F] = () => { // LD A,R
            A = R;
            FlagS = (A & 0x80) != 0;
            FlagZ = A == 0;
            FlagH = false;
            FlagPV = IFF2;
            FlagN = false;
            FlagY = (A & 0x20) != 0;
            FlagX = (A & 0x08) != 0;
            return 9;
        };

        // RRD / RLD
        _edOps[0x67] = () => { Rrd(); return 18; };
        _edOps[0x6F] = () => { Rld(); return 18; };

        // Block transfer/compare/IO
        _edOps[0xA0] = () => { Ldi(); return 16; };
        _edOps[0xA1] = () => { Cpi(); return 16; };
        _edOps[0xA2] = () => { Ini(); return 16; };
        _edOps[0xA3] = () => { Outi(); return 16; };
        _edOps[0xA8] = () => { Ldd(); return 16; };
        _edOps[0xA9] = () => { Cpd(); return 16; };
        _edOps[0xAA] = () => { Ind(); return 16; };
        _edOps[0xAB] = () => { Outd(); return 16; };

        // Repeat block operations
        _edOps[0xB0] = () => { Ldi(); if (BC != 0) { PC -= 2; return 21; } return 16; }; // LDIR
        _edOps[0xB1] = () => { Cpi(); if (BC != 0 && !FlagZ) { PC -= 2; return 21; } return 16; }; // CPIR
        _edOps[0xB2] = () => { Ini(); if (B != 0) { PC -= 2; return 21; } return 16; }; // INIR
        _edOps[0xB3] = () => { Outi(); if (B != 0) { PC -= 2; return 21; } return 16; }; // OTIR
        _edOps[0xB8] = () => { Ldd(); if (BC != 0) { PC -= 2; return 21; } return 16; }; // LDDR
        _edOps[0xB9] = () => { Cpd(); if (BC != 0 && !FlagZ) { PC -= 2; return 21; } return 16; }; // CPDR
        _edOps[0xBA] = () => { Ind(); if (B != 0) { PC -= 2; return 21; } return 16; }; // INDR
        _edOps[0xBB] = () => { Outd(); if (B != 0) { PC -= 2; return 21; } return 16; }; // OTDR
    }

    private void InitDdOps()
    {
        // DD prefix: IX instructions
        // Most opcodes fall through to main with IX substituted for HL
        // Only explicit entries here for IX-specific behavior

        _ddOps[0x21] = () => { IX = FetchWord(); return 14; }; // LD IX,nn
        _ddOps[0x22] = () => { WriteWord(FetchWord(), IX); return 20; }; // LD (nn),IX
        _ddOps[0x23] = () => { IX++; return 10; }; // INC IX
        _ddOps[0x2A] = () => { IX = ReadWord(FetchWord()); return 20; }; // LD IX,(nn)
        _ddOps[0x2B] = () => { IX--; return 10; }; // DEC IX

        // INC/DEC IXH/IXL (undocumented)
        _ddOps[0x24] = () => { IX = (ushort)((Inc((byte)(IX >> 8)) << 8) | (IX & 0xFF)); return 8; }; // INC IXH
        _ddOps[0x25] = () => { IX = (ushort)((Dec((byte)(IX >> 8)) << 8) | (IX & 0xFF)); return 8; }; // DEC IXH
        _ddOps[0x2C] = () => { IX = (ushort)((IX & 0xFF00) | Inc((byte)IX)); return 8; }; // INC IXL
        _ddOps[0x2D] = () => { IX = (ushort)((IX & 0xFF00) | Dec((byte)IX)); return 8; }; // DEC IXL

        // LD IXH/IXL,n (undocumented)
        _ddOps[0x26] = () => { IX = (ushort)((FetchByte() << 8) | (IX & 0xFF)); return 11; }; // LD IXH,n
        _ddOps[0x2E] = () => { IX = (ushort)((IX & 0xFF00) | FetchByte()); return 11; }; // LD IXL,n

        // ADD IX,rr
        _ddOps[0x09] = () => { int r = IX + BC; FlagH = ((IX & 0x0FFF) + (BC & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; IX = (ushort)r; FlagY = ((byte)(IX >> 8) & 0x20) != 0; FlagX = ((byte)(IX >> 8) & 0x08) != 0; return 15; };
        _ddOps[0x19] = () => { int r = IX + DE; FlagH = ((IX & 0x0FFF) + (DE & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; IX = (ushort)r; FlagY = ((byte)(IX >> 8) & 0x20) != 0; FlagX = ((byte)(IX >> 8) & 0x08) != 0; return 15; };
        _ddOps[0x29] = () => { int r = IX + IX; FlagH = ((IX & 0x0FFF) + (IX & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; IX = (ushort)r; FlagY = ((byte)(IX >> 8) & 0x20) != 0; FlagX = ((byte)(IX >> 8) & 0x08) != 0; return 15; };
        _ddOps[0x39] = () => { int r = IX + SP; FlagH = ((IX & 0x0FFF) + (SP & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; IX = (ushort)r; FlagY = ((byte)(IX >> 8) & 0x20) != 0; FlagX = ((byte)(IX >> 8) & 0x08) != 0; return 15; };

        // LD r,(IX+d) / LD (IX+d),r
        _ddOps[0x34] = () => { sbyte d = (sbyte)FetchByte(); ushort addr = (ushort)(IX + d); WriteByte(addr, Inc(ReadByte(addr))); return 23; }; // INC (IX+d)
        _ddOps[0x35] = () => { sbyte d = (sbyte)FetchByte(); ushort addr = (ushort)(IX + d); WriteByte(addr, Dec(ReadByte(addr))); return 23; }; // DEC (IX+d)
        _ddOps[0x36] = () => { sbyte d = (sbyte)FetchByte(); byte n = FetchByte(); WriteByte((ushort)(IX + d), n); return 19; }; // LD (IX+d),n

        // LD r,(IX+d)
        _ddOps[0x46] = () => { B = ReadByte((ushort)(IX + (sbyte)FetchByte())); return 19; };
        _ddOps[0x4E] = () => { C = ReadByte((ushort)(IX + (sbyte)FetchByte())); return 19; };
        _ddOps[0x56] = () => { D = ReadByte((ushort)(IX + (sbyte)FetchByte())); return 19; };
        _ddOps[0x5E] = () => { E = ReadByte((ushort)(IX + (sbyte)FetchByte())); return 19; };
        _ddOps[0x66] = () => { H = ReadByte((ushort)(IX + (sbyte)FetchByte())); return 19; };
        _ddOps[0x6E] = () => { L = ReadByte((ushort)(IX + (sbyte)FetchByte())); return 19; };
        _ddOps[0x7E] = () => { A = ReadByte((ushort)(IX + (sbyte)FetchByte())); return 19; };

        // LD (IX+d),r
        _ddOps[0x70] = () => { WriteByte((ushort)(IX + (sbyte)FetchByte()), B); return 19; };
        _ddOps[0x71] = () => { WriteByte((ushort)(IX + (sbyte)FetchByte()), C); return 19; };
        _ddOps[0x72] = () => { WriteByte((ushort)(IX + (sbyte)FetchByte()), D); return 19; };
        _ddOps[0x73] = () => { WriteByte((ushort)(IX + (sbyte)FetchByte()), E); return 19; };
        _ddOps[0x74] = () => { WriteByte((ushort)(IX + (sbyte)FetchByte()), H); return 19; };
        _ddOps[0x75] = () => { WriteByte((ushort)(IX + (sbyte)FetchByte()), L); return 19; };
        _ddOps[0x77] = () => { WriteByte((ushort)(IX + (sbyte)FetchByte()), A); return 19; };

        // ALU A,(IX+d)
        _ddOps[0x86] = () => { Add(ReadByte((ushort)(IX + (sbyte)FetchByte()))); return 19; };
        _ddOps[0x8E] = () => { Adc(ReadByte((ushort)(IX + (sbyte)FetchByte()))); return 19; };
        _ddOps[0x96] = () => { Sub(ReadByte((ushort)(IX + (sbyte)FetchByte()))); return 19; };
        _ddOps[0x9E] = () => { Sbc(ReadByte((ushort)(IX + (sbyte)FetchByte()))); return 19; };
        _ddOps[0xA6] = () => { And(ReadByte((ushort)(IX + (sbyte)FetchByte()))); return 19; };
        _ddOps[0xAE] = () => { Xor(ReadByte((ushort)(IX + (sbyte)FetchByte()))); return 19; };
        _ddOps[0xB6] = () => { Or(ReadByte((ushort)(IX + (sbyte)FetchByte()))); return 19; };
        _ddOps[0xBE] = () => { Cp(ReadByte((ushort)(IX + (sbyte)FetchByte()))); return 19; };

        // PUSH/POP IX
        _ddOps[0xE1] = () => { IX = Pop(); return 14; };
        _ddOps[0xE5] = () => { Push(IX); return 15; };

        // EX (SP),IX
        _ddOps[0xE3] = () => { ushort temp = ReadWord(SP); WriteWord(SP, IX); IX = temp; return 23; };

        // JP (IX)
        _ddOps[0xE9] = () => { PC = IX; return 8; };

        // LD SP,IX
        _ddOps[0xF9] = () => { SP = IX; return 10; };
    }

    private void InitFdOps()
    {
        // FD prefix: IY instructions (mirror of DD but for IY)
        _fdOps[0x21] = () => { IY = FetchWord(); return 14; };
        _fdOps[0x22] = () => { WriteWord(FetchWord(), IY); return 20; };
        _fdOps[0x23] = () => { IY++; return 10; };
        _fdOps[0x2A] = () => { IY = ReadWord(FetchWord()); return 20; };
        _fdOps[0x2B] = () => { IY--; return 10; };

        // INC/DEC IYH/IYL (undocumented)
        _fdOps[0x24] = () => { IY = (ushort)((Inc((byte)(IY >> 8)) << 8) | (IY & 0xFF)); return 8; };
        _fdOps[0x25] = () => { IY = (ushort)((Dec((byte)(IY >> 8)) << 8) | (IY & 0xFF)); return 8; };
        _fdOps[0x2C] = () => { IY = (ushort)((IY & 0xFF00) | Inc((byte)IY)); return 8; };
        _fdOps[0x2D] = () => { IY = (ushort)((IY & 0xFF00) | Dec((byte)IY)); return 8; };

        // LD IYH/IYL,n (undocumented)
        _fdOps[0x26] = () => { IY = (ushort)((FetchByte() << 8) | (IY & 0xFF)); return 11; };
        _fdOps[0x2E] = () => { IY = (ushort)((IY & 0xFF00) | FetchByte()); return 11; };

        // ADD IY,rr
        _fdOps[0x09] = () => { int r = IY + BC; FlagH = ((IY & 0x0FFF) + (BC & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; IY = (ushort)r; FlagY = ((byte)(IY >> 8) & 0x20) != 0; FlagX = ((byte)(IY >> 8) & 0x08) != 0; return 15; };
        _fdOps[0x19] = () => { int r = IY + DE; FlagH = ((IY & 0x0FFF) + (DE & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; IY = (ushort)r; FlagY = ((byte)(IY >> 8) & 0x20) != 0; FlagX = ((byte)(IY >> 8) & 0x08) != 0; return 15; };
        _fdOps[0x29] = () => { int r = IY + IY; FlagH = ((IY & 0x0FFF) + (IY & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; IY = (ushort)r; FlagY = ((byte)(IY >> 8) & 0x20) != 0; FlagX = ((byte)(IY >> 8) & 0x08) != 0; return 15; };
        _fdOps[0x39] = () => { int r = IY + SP; FlagH = ((IY & 0x0FFF) + (SP & 0x0FFF)) > 0x0FFF; FlagC = r > 0xFFFF; FlagN = false; IY = (ushort)r; FlagY = ((byte)(IY >> 8) & 0x20) != 0; FlagX = ((byte)(IY >> 8) & 0x08) != 0; return 15; };

        _fdOps[0x34] = () => { sbyte d = (sbyte)FetchByte(); ushort addr = (ushort)(IY + d); WriteByte(addr, Inc(ReadByte(addr))); return 23; };
        _fdOps[0x35] = () => { sbyte d = (sbyte)FetchByte(); ushort addr = (ushort)(IY + d); WriteByte(addr, Dec(ReadByte(addr))); return 23; };
        _fdOps[0x36] = () => { sbyte d = (sbyte)FetchByte(); byte n = FetchByte(); WriteByte((ushort)(IY + d), n); return 19; };

        // LD r,(IY+d)
        _fdOps[0x46] = () => { B = ReadByte((ushort)(IY + (sbyte)FetchByte())); return 19; };
        _fdOps[0x4E] = () => { C = ReadByte((ushort)(IY + (sbyte)FetchByte())); return 19; };
        _fdOps[0x56] = () => { D = ReadByte((ushort)(IY + (sbyte)FetchByte())); return 19; };
        _fdOps[0x5E] = () => { E = ReadByte((ushort)(IY + (sbyte)FetchByte())); return 19; };
        _fdOps[0x66] = () => { H = ReadByte((ushort)(IY + (sbyte)FetchByte())); return 19; };
        _fdOps[0x6E] = () => { L = ReadByte((ushort)(IY + (sbyte)FetchByte())); return 19; };
        _fdOps[0x7E] = () => { A = ReadByte((ushort)(IY + (sbyte)FetchByte())); return 19; };

        // LD (IY+d),r
        _fdOps[0x70] = () => { WriteByte((ushort)(IY + (sbyte)FetchByte()), B); return 19; };
        _fdOps[0x71] = () => { WriteByte((ushort)(IY + (sbyte)FetchByte()), C); return 19; };
        _fdOps[0x72] = () => { WriteByte((ushort)(IY + (sbyte)FetchByte()), D); return 19; };
        _fdOps[0x73] = () => { WriteByte((ushort)(IY + (sbyte)FetchByte()), E); return 19; };
        _fdOps[0x74] = () => { WriteByte((ushort)(IY + (sbyte)FetchByte()), H); return 19; };
        _fdOps[0x75] = () => { WriteByte((ushort)(IY + (sbyte)FetchByte()), L); return 19; };
        _fdOps[0x77] = () => { WriteByte((ushort)(IY + (sbyte)FetchByte()), A); return 19; };

        // ALU A,(IY+d)
        _fdOps[0x86] = () => { Add(ReadByte((ushort)(IY + (sbyte)FetchByte()))); return 19; };
        _fdOps[0x8E] = () => { Adc(ReadByte((ushort)(IY + (sbyte)FetchByte()))); return 19; };
        _fdOps[0x96] = () => { Sub(ReadByte((ushort)(IY + (sbyte)FetchByte()))); return 19; };
        _fdOps[0x9E] = () => { Sbc(ReadByte((ushort)(IY + (sbyte)FetchByte()))); return 19; };
        _fdOps[0xA6] = () => { And(ReadByte((ushort)(IY + (sbyte)FetchByte()))); return 19; };
        _fdOps[0xAE] = () => { Xor(ReadByte((ushort)(IY + (sbyte)FetchByte()))); return 19; };
        _fdOps[0xB6] = () => { Or(ReadByte((ushort)(IY + (sbyte)FetchByte()))); return 19; };
        _fdOps[0xBE] = () => { Cp(ReadByte((ushort)(IY + (sbyte)FetchByte()))); return 19; };

        // PUSH/POP IY
        _fdOps[0xE1] = () => { IY = Pop(); return 14; };
        _fdOps[0xE5] = () => { Push(IY); return 15; };

        // EX (SP),IY
        _fdOps[0xE3] = () => { ushort temp = ReadWord(SP); WriteWord(SP, IY); IY = temp; return 23; };

        // JP (IY)
        _fdOps[0xE9] = () => { PC = IY; return 8; };

        // LD SP,IY
        _fdOps[0xF9] = () => { SP = IY; return 10; };
    }

    #endregion

    #region ED Instruction Helpers

    private byte InC()
    {
        byte result = _ioBus.In(C);
        FlagS = (result & 0x80) != 0;
        FlagZ = result == 0;
        FlagH = false;
        FlagPV = ParityTable[result];
        FlagN = false;
        FlagY = (result & 0x20) != 0;
        FlagX = (result & 0x08) != 0;
        return result;
    }

    private void Neg()
    {
        int result = 0 - A;
        FlagH = (A & 0x0F) != 0;
        FlagPV = A == 0x80;
        FlagC = A != 0;
        FlagN = true;
        A = (byte)result;
        SetSZXY(A);
    }

    private void Rrd()
    {
        byte mem = ReadByte(HL);
        byte newMem = (byte)((A << 4) | (mem >> 4));
        A = (byte)((A & 0xF0) | (mem & 0x0F));
        WriteByte(HL, newMem);
        FlagH = false;
        FlagN = false;
        SetSZP(A);
    }

    private void Rld()
    {
        byte mem = ReadByte(HL);
        byte newMem = (byte)((mem << 4) | (A & 0x0F));
        A = (byte)((A & 0xF0) | (mem >> 4));
        WriteByte(HL, newMem);
        FlagH = false;
        FlagN = false;
        SetSZP(A);
    }

    private void Ldi()
    {
        byte val = ReadByte(HL);
        WriteByte(DE, val);
        HL++;
        DE++;
        BC--;
        FlagH = false;
        FlagPV = BC != 0;
        FlagN = false;
        byte n = (byte)(val + A);
        FlagY = (n & 0x02) != 0;
        FlagX = (n & 0x08) != 0;
    }

    private void Ldd()
    {
        byte val = ReadByte(HL);
        WriteByte(DE, val);
        HL--;
        DE--;
        BC--;
        FlagH = false;
        FlagPV = BC != 0;
        FlagN = false;
        byte n = (byte)(val + A);
        FlagY = (n & 0x02) != 0;
        FlagX = (n & 0x08) != 0;
    }

    private void Cpi()
    {
        byte val = ReadByte(HL);
        int result = A - val;
        HL++;
        BC--;
        FlagS = (result & 0x80) != 0;
        FlagZ = (byte)result == 0;
        FlagH = (A & 0x0F) < (val & 0x0F);
        FlagPV = BC != 0;
        FlagN = true;
        byte n = (byte)(result - (FlagH ? 1 : 0));
        FlagY = (n & 0x02) != 0;
        FlagX = (n & 0x08) != 0;
    }

    private void Cpd()
    {
        byte val = ReadByte(HL);
        int result = A - val;
        HL--;
        BC--;
        FlagS = (result & 0x80) != 0;
        FlagZ = (byte)result == 0;
        FlagH = (A & 0x0F) < (val & 0x0F);
        FlagPV = BC != 0;
        FlagN = true;
        byte n = (byte)(result - (FlagH ? 1 : 0));
        FlagY = (n & 0x02) != 0;
        FlagX = (n & 0x08) != 0;
    }

    private void Ini()
    {
        byte val = _ioBus.In(C);
        WriteByte(HL, val);
        HL++;
        B--;
        FlagZ = B == 0;
        FlagN = (val & 0x80) != 0;
    }

    private void Ind()
    {
        byte val = _ioBus.In(C);
        WriteByte(HL, val);
        HL--;
        B--;
        FlagZ = B == 0;
        FlagN = (val & 0x80) != 0;
    }

    private void Outi()
    {
        byte val = ReadByte(HL);
        B--;
        _ioBus.Out(C, val);
        HL++;
        FlagZ = B == 0;
        FlagN = (val & 0x80) != 0;
    }

    private void Outd()
    {
        byte val = ReadByte(HL);
        B--;
        _ioBus.Out(C, val);
        HL--;
        FlagZ = B == 0;
        FlagN = (val & 0x80) != 0;
    }

    private void Daa()
    {
        byte correction = 0;
        bool carry = FlagC;

        if (FlagH || (!FlagN && (A & 0x0F) > 9))
        {
            correction = 0x06;
        }

        if (FlagC || (!FlagN && A > 0x99))
        {
            correction |= 0x60;
            carry = true;
        }

        if (FlagN)
        {
            FlagH = FlagH && (A & 0x0F) < 6;
            A -= correction;
        }
        else
        {
            FlagH = (A & 0x0F) > 9;
            A += correction;
        }

        FlagC = carry;
        SetSZP(A);
    }

    #endregion
}
