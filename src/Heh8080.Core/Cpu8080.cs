namespace Heh8080.Core;

/// <summary>
/// Intel 8080 CPU emulator with all 256 opcodes.
/// </summary>
public sealed class Cpu8080
{
    // 8-bit registers
    public byte A; // Accumulator
    public byte B, C; // BC pair
    public byte D, E; // DE pair
    public byte H, L; // HL pair

    // 16-bit registers
    public ushort SP; // Stack pointer
    public ushort PC; // Program counter

    // Flags (stored individually for speed)
    public bool FlagS;  // Sign (bit 7)
    public bool FlagZ;  // Zero (bit 6)
    public bool FlagAC; // Auxiliary carry (bit 4)
    public bool FlagP;  // Parity (bit 2)
    public bool FlagCY; // Carry (bit 0)

    // Interrupt state
    public bool InterruptsEnabled;
    public bool Halted;

    // Memory and I/O
    private readonly IMemory _memory;
    private readonly IIoBus _ioBus;

    // Parity lookup table (true = even parity)
    private static readonly bool[] ParityTable = new bool[256];

    static Cpu8080()
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

    public Cpu8080(IMemory memory, IIoBus ioBus)
    {
        _memory = memory;
        _ioBus = ioBus;
        Reset();
    }

    public void Reset()
    {
        A = B = C = D = E = H = L = 0;
        SP = 0;
        PC = 0;
        FlagS = FlagZ = FlagAC = FlagP = FlagCY = false;
        InterruptsEnabled = false;
        Halted = false;
    }

    // Register pair accessors
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

    // PSW (A + Flags) for PUSH/POP PSW
    public ushort PSW
    {
        get => (ushort)((A << 8) | GetFlags());
        set { A = (byte)(value >> 8); SetFlags((byte)value); }
    }

    public byte GetFlags()
    {
        byte f = 0x02; // Bit 1 always set
        if (FlagS) f |= 0x80;
        if (FlagZ) f |= 0x40;
        if (FlagAC) f |= 0x10;
        if (FlagP) f |= 0x04;
        if (FlagCY) f |= 0x01;
        return f;
    }

    public void SetFlags(byte f)
    {
        FlagS = (f & 0x80) != 0;
        FlagZ = (f & 0x40) != 0;
        FlagAC = (f & 0x10) != 0;
        FlagP = (f & 0x04) != 0;
        FlagCY = (f & 0x01) != 0;
    }

    // Memory access helpers
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

    private byte FetchByte() => _memory.Read(PC++);

    private ushort FetchWord()
    {
        byte lo = _memory.Read(PC++);
        byte hi = _memory.Read(PC++);
        return (ushort)((hi << 8) | lo);
    }

    // Stack operations
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

    // Flag helpers
    private void SetSZP(byte value)
    {
        FlagS = (value & 0x80) != 0;
        FlagZ = value == 0;
        FlagP = ParityTable[value];
    }

    // Execute one instruction, returns cycles used
    public int Step()
    {
        if (Halted) return 4;

        byte opcode = FetchByte();
        return ExecuteOpcode(opcode);
    }

    // Handle interrupt (RST instruction number 0-7)
    public void Interrupt(byte vector)
    {
        if (!InterruptsEnabled) return;
        InterruptsEnabled = false;
        Halted = false;
        Push(PC);
        PC = (ushort)(vector * 8);
    }

    private int ExecuteOpcode(byte opcode)
    {
        switch (opcode)
        {
            // NOP
            case 0x00: return 4;

            // LXI - Load register pair immediate
            case 0x01: BC = FetchWord(); return 10; // LXI B
            case 0x11: DE = FetchWord(); return 10; // LXI D
            case 0x21: HL = FetchWord(); return 10; // LXI H
            case 0x31: SP = FetchWord(); return 10; // LXI SP

            // STAX - Store A indirect
            case 0x02: WriteByte(BC, A); return 7; // STAX B
            case 0x12: WriteByte(DE, A); return 7; // STAX D

            // LDAX - Load A indirect
            case 0x0A: A = ReadByte(BC); return 7; // LDAX B
            case 0x1A: A = ReadByte(DE); return 7; // LDAX D

            // INX - Increment register pair
            case 0x03: BC++; return 5;
            case 0x13: DE++; return 5;
            case 0x23: HL++; return 5;
            case 0x33: SP++; return 5;

            // DCX - Decrement register pair
            case 0x0B: BC--; return 5;
            case 0x1B: DE--; return 5;
            case 0x2B: HL--; return 5;
            case 0x3B: SP--; return 5;

            // INR - Increment register
            case 0x04: B = Inc(B); return 5;
            case 0x0C: C = Inc(C); return 5;
            case 0x14: D = Inc(D); return 5;
            case 0x1C: E = Inc(E); return 5;
            case 0x24: H = Inc(H); return 5;
            case 0x2C: L = Inc(L); return 5;
            case 0x34: WriteByte(HL, Inc(ReadByte(HL))); return 10;
            case 0x3C: A = Inc(A); return 5;

            // DCR - Decrement register
            case 0x05: B = Dec(B); return 5;
            case 0x0D: C = Dec(C); return 5;
            case 0x15: D = Dec(D); return 5;
            case 0x1D: E = Dec(E); return 5;
            case 0x25: H = Dec(H); return 5;
            case 0x2D: L = Dec(L); return 5;
            case 0x35: WriteByte(HL, Dec(ReadByte(HL))); return 10;
            case 0x3D: A = Dec(A); return 5;

            // MVI - Move immediate
            case 0x06: B = FetchByte(); return 7;
            case 0x0E: C = FetchByte(); return 7;
            case 0x16: D = FetchByte(); return 7;
            case 0x1E: E = FetchByte(); return 7;
            case 0x26: H = FetchByte(); return 7;
            case 0x2E: L = FetchByte(); return 7;
            case 0x36: WriteByte(HL, FetchByte()); return 10;
            case 0x3E: A = FetchByte(); return 7;

            // Rotate instructions
            case 0x07: // RLC
            {
                FlagCY = (A & 0x80) != 0;
                A = (byte)((A << 1) | (FlagCY ? 1 : 0));
                return 4;
            }
            case 0x0F: // RRC
            {
                FlagCY = (A & 0x01) != 0;
                A = (byte)((A >> 1) | (FlagCY ? 0x80 : 0));
                return 4;
            }
            case 0x17: // RAL
            {
                bool carry = FlagCY;
                FlagCY = (A & 0x80) != 0;
                A = (byte)((A << 1) | (carry ? 1 : 0));
                return 4;
            }
            case 0x1F: // RAR
            {
                bool carry = FlagCY;
                FlagCY = (A & 0x01) != 0;
                A = (byte)((A >> 1) | (carry ? 0x80 : 0));
                return 4;
            }

            // DAD - Double add (add register pair to HL)
            case 0x09: { int r = HL + BC; FlagCY = r > 0xFFFF; HL = (ushort)r; return 10; }
            case 0x19: { int r = HL + DE; FlagCY = r > 0xFFFF; HL = (ushort)r; return 10; }
            case 0x29: { int r = HL + HL; FlagCY = r > 0xFFFF; HL = (ushort)r; return 10; }
            case 0x39: { int r = HL + SP; FlagCY = r > 0xFFFF; HL = (ushort)r; return 10; }

            // SHLD/LHLD - Store/Load HL direct
            case 0x22: WriteWord(FetchWord(), HL); return 16; // SHLD
            case 0x2A: HL = ReadWord(FetchWord()); return 16; // LHLD

            // STA/LDA - Store/Load A direct
            case 0x32: WriteByte(FetchWord(), A); return 13; // STA
            case 0x3A: A = ReadByte(FetchWord()); return 13; // LDA

            // Special instructions
            case 0x27: Daa(); return 4; // DAA
            case 0x2F: A = (byte)~A; return 4; // CMA
            case 0x37: FlagCY = true; return 4; // STC
            case 0x3F: FlagCY = !FlagCY; return 4; // CMC

            // MOV instructions (0x40-0x7F, except 0x76 = HLT)
            case 0x40: return 5; // MOV B,B (NOP)
            case 0x41: B = C; return 5;
            case 0x42: B = D; return 5;
            case 0x43: B = E; return 5;
            case 0x44: B = H; return 5;
            case 0x45: B = L; return 5;
            case 0x46: B = ReadByte(HL); return 7;
            case 0x47: B = A; return 5;

            case 0x48: C = B; return 5;
            case 0x49: return 5; // MOV C,C (NOP)
            case 0x4A: C = D; return 5;
            case 0x4B: C = E; return 5;
            case 0x4C: C = H; return 5;
            case 0x4D: C = L; return 5;
            case 0x4E: C = ReadByte(HL); return 7;
            case 0x4F: C = A; return 5;

            case 0x50: D = B; return 5;
            case 0x51: D = C; return 5;
            case 0x52: return 5; // MOV D,D (NOP)
            case 0x53: D = E; return 5;
            case 0x54: D = H; return 5;
            case 0x55: D = L; return 5;
            case 0x56: D = ReadByte(HL); return 7;
            case 0x57: D = A; return 5;

            case 0x58: E = B; return 5;
            case 0x59: E = C; return 5;
            case 0x5A: E = D; return 5;
            case 0x5B: return 5; // MOV E,E (NOP)
            case 0x5C: E = H; return 5;
            case 0x5D: E = L; return 5;
            case 0x5E: E = ReadByte(HL); return 7;
            case 0x5F: E = A; return 5;

            case 0x60: H = B; return 5;
            case 0x61: H = C; return 5;
            case 0x62: H = D; return 5;
            case 0x63: H = E; return 5;
            case 0x64: return 5; // MOV H,H (NOP)
            case 0x65: H = L; return 5;
            case 0x66: H = ReadByte(HL); return 7;
            case 0x67: H = A; return 5;

            case 0x68: L = B; return 5;
            case 0x69: L = C; return 5;
            case 0x6A: L = D; return 5;
            case 0x6B: L = E; return 5;
            case 0x6C: L = H; return 5;
            case 0x6D: return 5; // MOV L,L (NOP)
            case 0x6E: L = ReadByte(HL); return 7;
            case 0x6F: L = A; return 5;

            case 0x70: WriteByte(HL, B); return 7;
            case 0x71: WriteByte(HL, C); return 7;
            case 0x72: WriteByte(HL, D); return 7;
            case 0x73: WriteByte(HL, E); return 7;
            case 0x74: WriteByte(HL, H); return 7;
            case 0x75: WriteByte(HL, L); return 7;
            case 0x76: Halted = true; return 7; // HLT
            case 0x77: WriteByte(HL, A); return 7;

            case 0x78: A = B; return 5;
            case 0x79: A = C; return 5;
            case 0x7A: A = D; return 5;
            case 0x7B: A = E; return 5;
            case 0x7C: A = H; return 5;
            case 0x7D: A = L; return 5;
            case 0x7E: A = ReadByte(HL); return 7;
            case 0x7F: return 5; // MOV A,A (NOP)

            // ADD - Add register to A
            case 0x80: Add(B); return 4;
            case 0x81: Add(C); return 4;
            case 0x82: Add(D); return 4;
            case 0x83: Add(E); return 4;
            case 0x84: Add(H); return 4;
            case 0x85: Add(L); return 4;
            case 0x86: Add(ReadByte(HL)); return 7;
            case 0x87: Add(A); return 4;

            // ADC - Add with carry
            case 0x88: Adc(B); return 4;
            case 0x89: Adc(C); return 4;
            case 0x8A: Adc(D); return 4;
            case 0x8B: Adc(E); return 4;
            case 0x8C: Adc(H); return 4;
            case 0x8D: Adc(L); return 4;
            case 0x8E: Adc(ReadByte(HL)); return 7;
            case 0x8F: Adc(A); return 4;

            // SUB - Subtract register from A
            case 0x90: Sub(B); return 4;
            case 0x91: Sub(C); return 4;
            case 0x92: Sub(D); return 4;
            case 0x93: Sub(E); return 4;
            case 0x94: Sub(H); return 4;
            case 0x95: Sub(L); return 4;
            case 0x96: Sub(ReadByte(HL)); return 7;
            case 0x97: Sub(A); return 4;

            // SBB - Subtract with borrow
            case 0x98: Sbb(B); return 4;
            case 0x99: Sbb(C); return 4;
            case 0x9A: Sbb(D); return 4;
            case 0x9B: Sbb(E); return 4;
            case 0x9C: Sbb(H); return 4;
            case 0x9D: Sbb(L); return 4;
            case 0x9E: Sbb(ReadByte(HL)); return 7;
            case 0x9F: Sbb(A); return 4;

            // ANA - AND with A
            case 0xA0: Ana(B); return 4;
            case 0xA1: Ana(C); return 4;
            case 0xA2: Ana(D); return 4;
            case 0xA3: Ana(E); return 4;
            case 0xA4: Ana(H); return 4;
            case 0xA5: Ana(L); return 4;
            case 0xA6: Ana(ReadByte(HL)); return 7;
            case 0xA7: Ana(A); return 4;

            // XRA - XOR with A
            case 0xA8: Xra(B); return 4;
            case 0xA9: Xra(C); return 4;
            case 0xAA: Xra(D); return 4;
            case 0xAB: Xra(E); return 4;
            case 0xAC: Xra(H); return 4;
            case 0xAD: Xra(L); return 4;
            case 0xAE: Xra(ReadByte(HL)); return 7;
            case 0xAF: Xra(A); return 4;

            // ORA - OR with A
            case 0xB0: Ora(B); return 4;
            case 0xB1: Ora(C); return 4;
            case 0xB2: Ora(D); return 4;
            case 0xB3: Ora(E); return 4;
            case 0xB4: Ora(H); return 4;
            case 0xB5: Ora(L); return 4;
            case 0xB6: Ora(ReadByte(HL)); return 7;
            case 0xB7: Ora(A); return 4;

            // CMP - Compare with A
            case 0xB8: Cmp(B); return 4;
            case 0xB9: Cmp(C); return 4;
            case 0xBA: Cmp(D); return 4;
            case 0xBB: Cmp(E); return 4;
            case 0xBC: Cmp(H); return 4;
            case 0xBD: Cmp(L); return 4;
            case 0xBE: Cmp(ReadByte(HL)); return 7;
            case 0xBF: Cmp(A); return 4;

            // Conditional returns
            case 0xC0: if (!FlagZ) { PC = Pop(); return 11; } return 5; // RNZ
            case 0xC8: if (FlagZ) { PC = Pop(); return 11; } return 5;  // RZ
            case 0xD0: if (!FlagCY) { PC = Pop(); return 11; } return 5; // RNC
            case 0xD8: if (FlagCY) { PC = Pop(); return 11; } return 5;  // RC
            case 0xE0: if (!FlagP) { PC = Pop(); return 11; } return 5; // RPO
            case 0xE8: if (FlagP) { PC = Pop(); return 11; } return 5;  // RPE
            case 0xF0: if (!FlagS) { PC = Pop(); return 11; } return 5; // RP
            case 0xF8: if (FlagS) { PC = Pop(); return 11; } return 5;  // RM

            // POP
            case 0xC1: BC = Pop(); return 10;
            case 0xD1: DE = Pop(); return 10;
            case 0xE1: HL = Pop(); return 10;
            case 0xF1: PSW = Pop(); return 10;

            // Conditional jumps
            case 0xC2: { ushort addr = FetchWord(); if (!FlagZ) PC = addr; return 10; } // JNZ
            case 0xCA: { ushort addr = FetchWord(); if (FlagZ) PC = addr; return 10; }  // JZ
            case 0xD2: { ushort addr = FetchWord(); if (!FlagCY) PC = addr; return 10; } // JNC
            case 0xDA: { ushort addr = FetchWord(); if (FlagCY) PC = addr; return 10; }  // JC
            case 0xE2: { ushort addr = FetchWord(); if (!FlagP) PC = addr; return 10; } // JPO
            case 0xEA: { ushort addr = FetchWord(); if (FlagP) PC = addr; return 10; }  // JPE
            case 0xF2: { ushort addr = FetchWord(); if (!FlagS) PC = addr; return 10; } // JP
            case 0xFA: { ushort addr = FetchWord(); if (FlagS) PC = addr; return 10; }  // JM

            // JMP
            case 0xC3: PC = FetchWord(); return 10;

            // OUT
            case 0xD3: _ioBus.Out(FetchByte(), A); return 10;

            // IN
            case 0xDB: A = _ioBus.In(FetchByte()); return 10;

            // XTHL - Exchange top of stack with HL
            case 0xE3:
            {
                ushort temp = ReadWord(SP);
                WriteWord(SP, HL);
                HL = temp;
                return 18;
            }

            // PCHL - Jump to address in HL
            case 0xE9: PC = HL; return 5;

            // XCHG - Exchange DE and HL
            case 0xEB: { ushort temp = DE; DE = HL; HL = temp; return 5; }

            // DI/EI
            case 0xF3: InterruptsEnabled = false; return 4;
            case 0xFB: InterruptsEnabled = true; return 4;

            // SPHL - Load SP from HL
            case 0xF9: SP = HL; return 5;

            // Conditional calls
            case 0xC4: { ushort addr = FetchWord(); if (!FlagZ) { Push(PC); PC = addr; return 17; } return 11; } // CNZ
            case 0xCC: { ushort addr = FetchWord(); if (FlagZ) { Push(PC); PC = addr; return 17; } return 11; }  // CZ
            case 0xD4: { ushort addr = FetchWord(); if (!FlagCY) { Push(PC); PC = addr; return 17; } return 11; } // CNC
            case 0xDC: { ushort addr = FetchWord(); if (FlagCY) { Push(PC); PC = addr; return 17; } return 11; }  // CC
            case 0xE4: { ushort addr = FetchWord(); if (!FlagP) { Push(PC); PC = addr; return 17; } return 11; } // CPO
            case 0xEC: { ushort addr = FetchWord(); if (FlagP) { Push(PC); PC = addr; return 17; } return 11; }  // CPE
            case 0xF4: { ushort addr = FetchWord(); if (!FlagS) { Push(PC); PC = addr; return 17; } return 11; } // CP
            case 0xFC: { ushort addr = FetchWord(); if (FlagS) { Push(PC); PC = addr; return 17; } return 11; }  // CM

            // PUSH
            case 0xC5: Push(BC); return 11;
            case 0xD5: Push(DE); return 11;
            case 0xE5: Push(HL); return 11;
            case 0xF5: Push(PSW); return 11;

            // Immediate arithmetic
            case 0xC6: Add(FetchByte()); return 7; // ADI
            case 0xCE: Adc(FetchByte()); return 7; // ACI
            case 0xD6: Sub(FetchByte()); return 7; // SUI
            case 0xDE: Sbb(FetchByte()); return 7; // SBI
            case 0xE6: Ana(FetchByte()); return 7; // ANI
            case 0xEE: Xra(FetchByte()); return 7; // XRI
            case 0xF6: Ora(FetchByte()); return 7; // ORI
            case 0xFE: Cmp(FetchByte()); return 7; // CPI

            // RST - Restart
            case 0xC7: Push(PC); PC = 0x00; return 11;
            case 0xCF: Push(PC); PC = 0x08; return 11;
            case 0xD7: Push(PC); PC = 0x10; return 11;
            case 0xDF: Push(PC); PC = 0x18; return 11;
            case 0xE7: Push(PC); PC = 0x20; return 11;
            case 0xEF: Push(PC); PC = 0x28; return 11;
            case 0xF7: Push(PC); PC = 0x30; return 11;
            case 0xFF: Push(PC); PC = 0x38; return 11;

            // CALL
            case 0xCD: { ushort addr = FetchWord(); Push(PC); PC = addr; return 17; }

            // RET
            case 0xC9: PC = Pop(); return 10;

            // Undocumented NOPs (act as NOP)
            case 0x08:
            case 0x10:
            case 0x18:
            case 0x20:
            case 0x28:
            case 0x30:
            case 0x38:
                return 4;

            // Undocumented duplicates
            case 0xCB: PC = FetchWord(); return 10; // JMP (duplicate of 0xC3)
            case 0xD9: PC = Pop(); return 10; // RET (duplicate of 0xC9)
            case 0xDD: { ushort addr = FetchWord(); Push(PC); PC = addr; return 17; } // CALL (duplicate)
            case 0xED: { ushort addr = FetchWord(); Push(PC); PC = addr; return 17; } // CALL (duplicate)
            case 0xFD: { ushort addr = FetchWord(); Push(PC); PC = addr; return 17; } // CALL (duplicate)
        }
    }

    // ALU operations
    private byte Inc(byte value)
    {
        byte result = (byte)(value + 1);
        FlagAC = (value & 0x0F) == 0x0F;
        SetSZP(result);
        return result;
    }

    private byte Dec(byte value)
    {
        byte result = (byte)(value - 1);
        FlagAC = (value & 0x0F) != 0;
        SetSZP(result);
        return result;
    }

    private void Add(byte value)
    {
        int result = A + value;
        FlagAC = ((A & 0x0F) + (value & 0x0F)) > 0x0F;
        FlagCY = result > 0xFF;
        A = (byte)result;
        SetSZP(A);
    }

    private void Adc(byte value)
    {
        int carry = FlagCY ? 1 : 0;
        int result = A + value + carry;
        FlagAC = ((A & 0x0F) + (value & 0x0F) + carry) > 0x0F;
        FlagCY = result > 0xFF;
        A = (byte)result;
        SetSZP(A);
    }

    private void Sub(byte value)
    {
        int result = A - value;
        FlagAC = (A & 0x0F) >= (value & 0x0F);
        FlagCY = result < 0;
        A = (byte)result;
        SetSZP(A);
    }

    private void Sbb(byte value)
    {
        int borrow = FlagCY ? 1 : 0;
        int result = A - value - borrow;
        FlagAC = (A & 0x0F) >= ((value & 0x0F) + borrow);
        FlagCY = result < 0;
        A = (byte)result;
        SetSZP(A);
    }

    private void Ana(byte value)
    {
        // Note: 8080 sets AC to OR of bit 3 of operands
        FlagAC = ((A | value) & 0x08) != 0;
        A &= value;
        FlagCY = false;
        SetSZP(A);
    }

    private void Xra(byte value)
    {
        A ^= value;
        FlagCY = false;
        FlagAC = false;
        SetSZP(A);
    }

    private void Ora(byte value)
    {
        A |= value;
        FlagCY = false;
        FlagAC = false;
        SetSZP(A);
    }

    private void Cmp(byte value)
    {
        int result = A - value;
        FlagAC = (A & 0x0F) >= (value & 0x0F);
        FlagCY = result < 0;
        SetSZP((byte)result);
    }

    private void Daa()
    {
        byte correction = 0;
        bool carry = FlagCY;

        if (FlagAC || (A & 0x0F) > 9)
        {
            correction = 0x06;
        }

        if (FlagCY || A > 0x99 || (A > 0x8F && (A & 0x0F) > 9))
        {
            correction |= 0x60;
            carry = true;
        }

        FlagAC = ((A & 0x0F) + (correction & 0x0F)) > 0x0F;
        A += correction;
        FlagCY = carry;
        SetSZP(A);
    }
}
