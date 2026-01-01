using Heh8080.Core;

namespace Heh8080.Tests;

public class Cpu8080Tests
{
    private Memory _memory = null!;
    private IoBus _ioBus = null!;
    private Cpu8080 _cpu = null!;

    private void Setup()
    {
        _memory = new Memory();
        _ioBus = new IoBus();
        _cpu = new Cpu8080(_memory, _ioBus);
    }

    [Fact]
    public void Reset_SetsAllRegistersToZero()
    {
        Setup();
        _cpu.A = 0xFF;
        _cpu.B = 0xFF;
        _cpu.PC = 0x1234;

        _cpu.Reset();

        Assert.Equal(0, _cpu.A);
        Assert.Equal(0, _cpu.B);
        Assert.Equal(0, _cpu.PC);
        Assert.Equal(0, _cpu.SP);
        Assert.False(_cpu.FlagCY);
        Assert.False(_cpu.FlagZ);
    }

    [Fact]
    public void NOP_AdvancesPCBy1()
    {
        Setup();
        _memory.Write(0, 0x00); // NOP

        _cpu.Step();

        Assert.Equal(1, _cpu.PC);
    }

    [Fact]
    public void LXI_B_LoadsImmediateToBC()
    {
        Setup();
        _memory.Write(0, 0x01); // LXI B
        _memory.Write(1, 0x34); // low byte
        _memory.Write(2, 0x12); // high byte

        _cpu.Step();

        Assert.Equal(0x1234, _cpu.BC);
        Assert.Equal(0x12, _cpu.B);
        Assert.Equal(0x34, _cpu.C);
    }

    [Fact]
    public void MVI_A_LoadsImmediateToA()
    {
        Setup();
        _memory.Write(0, 0x3E); // MVI A
        _memory.Write(1, 0x42);

        _cpu.Step();

        Assert.Equal(0x42, _cpu.A);
    }

    [Fact]
    public void MOV_B_C_CopiesCtoB()
    {
        Setup();
        _cpu.C = 0x55;
        _memory.Write(0, 0x41); // MOV B,C

        _cpu.Step();

        Assert.Equal(0x55, _cpu.B);
    }

    [Fact]
    public void ADD_B_AddsToAccumulator()
    {
        Setup();
        _cpu.A = 0x10;
        _cpu.B = 0x20;
        _memory.Write(0, 0x80); // ADD B

        _cpu.Step();

        Assert.Equal(0x30, _cpu.A);
        Assert.False(_cpu.FlagCY);
        Assert.False(_cpu.FlagZ);
    }

    [Fact]
    public void ADD_SetsCarryOnOverflow()
    {
        Setup();
        _cpu.A = 0xFF;
        _cpu.B = 0x01;
        _memory.Write(0, 0x80); // ADD B

        _cpu.Step();

        Assert.Equal(0x00, _cpu.A);
        Assert.True(_cpu.FlagCY);
        Assert.True(_cpu.FlagZ);
    }

    [Fact]
    public void SUB_SubtractsFromAccumulator()
    {
        Setup();
        _cpu.A = 0x30;
        _cpu.B = 0x10;
        _memory.Write(0, 0x90); // SUB B

        _cpu.Step();

        Assert.Equal(0x20, _cpu.A);
        Assert.False(_cpu.FlagCY);
    }

    [Fact]
    public void SUB_SetsBorrowOnUnderflow()
    {
        Setup();
        _cpu.A = 0x10;
        _cpu.B = 0x20;
        _memory.Write(0, 0x90); // SUB B

        _cpu.Step();

        Assert.Equal(0xF0, _cpu.A);
        Assert.True(_cpu.FlagCY);
    }

    [Fact]
    public void INR_IncrementsRegister()
    {
        Setup();
        _cpu.B = 0x10;
        _memory.Write(0, 0x04); // INR B

        _cpu.Step();

        Assert.Equal(0x11, _cpu.B);
    }

    [Fact]
    public void INR_SetsZeroFlag()
    {
        Setup();
        _cpu.B = 0xFF;
        _memory.Write(0, 0x04); // INR B

        _cpu.Step();

        Assert.Equal(0x00, _cpu.B);
        Assert.True(_cpu.FlagZ);
    }

    [Fact]
    public void DCR_DecrementsRegister()
    {
        Setup();
        _cpu.B = 0x10;
        _memory.Write(0, 0x05); // DCR B

        _cpu.Step();

        Assert.Equal(0x0F, _cpu.B);
    }

    [Fact]
    public void JMP_SetsPCToAddress()
    {
        Setup();
        _memory.Write(0, 0xC3); // JMP
        _memory.Write(1, 0x00);
        _memory.Write(2, 0x10);

        _cpu.Step();

        Assert.Equal(0x1000, _cpu.PC);
    }

    [Fact]
    public void JNZ_JumpsWhenNotZero()
    {
        Setup();
        _cpu.FlagZ = false;
        _memory.Write(0, 0xC2); // JNZ
        _memory.Write(1, 0x00);
        _memory.Write(2, 0x10);

        _cpu.Step();

        Assert.Equal(0x1000, _cpu.PC);
    }

    [Fact]
    public void JNZ_DoesNotJumpWhenZero()
    {
        Setup();
        _cpu.FlagZ = true;
        _memory.Write(0, 0xC2); // JNZ
        _memory.Write(1, 0x00);
        _memory.Write(2, 0x10);

        _cpu.Step();

        Assert.Equal(3, _cpu.PC);
    }

    [Fact]
    public void CALL_PushesPCAndJumps()
    {
        Setup();
        _cpu.SP = 0xFFFF;
        _memory.Write(0, 0xCD); // CALL
        _memory.Write(1, 0x00);
        _memory.Write(2, 0x10);

        _cpu.Step();

        Assert.Equal(0x1000, _cpu.PC);
        Assert.Equal(0xFFFD, _cpu.SP);
        Assert.Equal(0x03, _memory.Read(0xFFFD)); // Return address low
        Assert.Equal(0x00, _memory.Read(0xFFFE)); // Return address high
    }

    [Fact]
    public void RET_PopsPCFromStack()
    {
        Setup();
        _cpu.SP = 0xFFFD;
        _memory.Write(0xFFFD, 0x34);
        _memory.Write(0xFFFE, 0x12);
        _memory.Write(0, 0xC9); // RET

        _cpu.Step();

        Assert.Equal(0x1234, _cpu.PC);
        Assert.Equal(0xFFFF, _cpu.SP);
    }

    [Fact]
    public void PUSH_BC_PushesBCToStack()
    {
        Setup();
        _cpu.SP = 0xFFFF;
        _cpu.BC = 0x1234;
        _memory.Write(0, 0xC5); // PUSH B

        _cpu.Step();

        Assert.Equal(0xFFFD, _cpu.SP);
        Assert.Equal(0x34, _memory.Read(0xFFFD));
        Assert.Equal(0x12, _memory.Read(0xFFFE));
    }

    [Fact]
    public void POP_BC_PopsBCFromStack()
    {
        Setup();
        _cpu.SP = 0xFFFD;
        _memory.Write(0xFFFD, 0x34);
        _memory.Write(0xFFFE, 0x12);
        _memory.Write(0, 0xC1); // POP B

        _cpu.Step();

        Assert.Equal(0x1234, _cpu.BC);
        Assert.Equal(0xFFFF, _cpu.SP);
    }

    [Fact]
    public void ANA_ANDsWithAccumulator()
    {
        Setup();
        _cpu.A = 0xFF;
        _cpu.B = 0x0F;
        _memory.Write(0, 0xA0); // ANA B

        _cpu.Step();

        Assert.Equal(0x0F, _cpu.A);
        Assert.False(_cpu.FlagCY);
    }

    [Fact]
    public void ORA_ORsWithAccumulator()
    {
        Setup();
        _cpu.A = 0xF0;
        _cpu.B = 0x0F;
        _memory.Write(0, 0xB0); // ORA B

        _cpu.Step();

        Assert.Equal(0xFF, _cpu.A);
        Assert.False(_cpu.FlagCY);
    }

    [Fact]
    public void XRA_XORsWithAccumulator()
    {
        Setup();
        _cpu.A = 0xFF;
        _cpu.B = 0x0F;
        _memory.Write(0, 0xA8); // XRA B

        _cpu.Step();

        Assert.Equal(0xF0, _cpu.A);
    }

    [Fact]
    public void XRA_A_ClearsAccumulator()
    {
        Setup();
        _cpu.A = 0xFF;
        _memory.Write(0, 0xAF); // XRA A

        _cpu.Step();

        Assert.Equal(0x00, _cpu.A);
        Assert.True(_cpu.FlagZ);
        Assert.True(_cpu.FlagP); // Zero has even parity
    }

    [Fact]
    public void CMP_SetsZeroWhenEqual()
    {
        Setup();
        _cpu.A = 0x42;
        _cpu.B = 0x42;
        _memory.Write(0, 0xB8); // CMP B

        _cpu.Step();

        Assert.Equal(0x42, _cpu.A); // A unchanged
        Assert.True(_cpu.FlagZ);
        Assert.False(_cpu.FlagCY);
    }

    [Fact]
    public void CMP_SetsBorrowWhenLess()
    {
        Setup();
        _cpu.A = 0x10;
        _cpu.B = 0x20;
        _memory.Write(0, 0xB8); // CMP B

        _cpu.Step();

        Assert.Equal(0x10, _cpu.A);
        Assert.True(_cpu.FlagCY);
    }

    [Fact]
    public void RLC_RotatesLeft()
    {
        Setup();
        _cpu.A = 0x80;
        _memory.Write(0, 0x07); // RLC

        _cpu.Step();

        Assert.Equal(0x01, _cpu.A);
        Assert.True(_cpu.FlagCY);
    }

    [Fact]
    public void RRC_RotatesRight()
    {
        Setup();
        _cpu.A = 0x01;
        _memory.Write(0, 0x0F); // RRC

        _cpu.Step();

        Assert.Equal(0x80, _cpu.A);
        Assert.True(_cpu.FlagCY);
    }

    [Fact]
    public void DAD_B_AddsToHL()
    {
        Setup();
        _cpu.HL = 0x1000;
        _cpu.BC = 0x0234;
        _memory.Write(0, 0x09); // DAD B

        _cpu.Step();

        Assert.Equal(0x1234, _cpu.HL);
        Assert.False(_cpu.FlagCY);
    }

    [Fact]
    public void DAD_SetsCarryOnOverflow()
    {
        Setup();
        _cpu.HL = 0xFFFF;
        _cpu.BC = 0x0001;
        _memory.Write(0, 0x09); // DAD B

        _cpu.Step();

        Assert.Equal(0x0000, _cpu.HL);
        Assert.True(_cpu.FlagCY);
    }

    [Fact]
    public void INX_B_IncrementsPair()
    {
        Setup();
        _cpu.BC = 0x00FF;
        _memory.Write(0, 0x03); // INX B

        _cpu.Step();

        Assert.Equal(0x0100, _cpu.BC);
    }

    [Fact]
    public void DCX_B_DecrementsPair()
    {
        Setup();
        _cpu.BC = 0x0100;
        _memory.Write(0, 0x0B); // DCX B

        _cpu.Step();

        Assert.Equal(0x00FF, _cpu.BC);
    }

    [Fact]
    public void STA_StoresAccumulator()
    {
        Setup();
        _cpu.A = 0x42;
        _memory.Write(0, 0x32); // STA
        _memory.Write(1, 0x00);
        _memory.Write(2, 0x10);

        _cpu.Step();

        Assert.Equal(0x42, _memory.Read(0x1000));
    }

    [Fact]
    public void LDA_LoadsAccumulator()
    {
        Setup();
        _memory.Write(0x1000, 0x42);
        _memory.Write(0, 0x3A); // LDA
        _memory.Write(1, 0x00);
        _memory.Write(2, 0x10);

        _cpu.Step();

        Assert.Equal(0x42, _cpu.A);
    }

    [Fact]
    public void SHLD_StoresHL()
    {
        Setup();
        _cpu.HL = 0x1234;
        _memory.Write(0, 0x22); // SHLD
        _memory.Write(1, 0x00);
        _memory.Write(2, 0x10);

        _cpu.Step();

        Assert.Equal(0x34, _memory.Read(0x1000));
        Assert.Equal(0x12, _memory.Read(0x1001));
    }

    [Fact]
    public void LHLD_LoadsHL()
    {
        Setup();
        _memory.Write(0x1000, 0x34);
        _memory.Write(0x1001, 0x12);
        _memory.Write(0, 0x2A); // LHLD
        _memory.Write(1, 0x00);
        _memory.Write(2, 0x10);

        _cpu.Step();

        Assert.Equal(0x1234, _cpu.HL);
    }

    [Fact]
    public void HLT_SetsHaltedFlag()
    {
        Setup();
        _memory.Write(0, 0x76); // HLT

        _cpu.Step();

        Assert.True(_cpu.Halted);
    }

    [Fact]
    public void EI_EnablesInterrupts()
    {
        Setup();
        _memory.Write(0, 0xFB); // EI

        _cpu.Step();

        Assert.True(_cpu.InterruptsEnabled);
    }

    [Fact]
    public void DI_DisablesInterrupts()
    {
        Setup();
        _cpu.InterruptsEnabled = true;
        _memory.Write(0, 0xF3); // DI

        _cpu.Step();

        Assert.False(_cpu.InterruptsEnabled);
    }

    [Fact]
    public void RST_0_CallsVector0()
    {
        Setup();
        _cpu.SP = 0xFFFF;
        _memory.Write(0x0100, 0xC7); // RST 0
        _cpu.PC = 0x0100;

        _cpu.Step();

        Assert.Equal(0x0000, _cpu.PC);
        Assert.Equal(0x01, _memory.Read(0xFFFD));
        Assert.Equal(0x01, _memory.Read(0xFFFE));
    }

    [Fact]
    public void Interrupt_WhenEnabled_CallsVector()
    {
        Setup();
        _cpu.InterruptsEnabled = true;
        _cpu.SP = 0xFFFF;
        _cpu.PC = 0x0100;

        _cpu.Interrupt(7); // RST 7 = 0x38

        Assert.Equal(0x0038, _cpu.PC);
        Assert.False(_cpu.InterruptsEnabled);
    }

    [Fact]
    public void Interrupt_WhenDisabled_IsIgnored()
    {
        Setup();
        _cpu.InterruptsEnabled = false;
        _cpu.PC = 0x0100;

        _cpu.Interrupt(7);

        Assert.Equal(0x0100, _cpu.PC);
    }
}
