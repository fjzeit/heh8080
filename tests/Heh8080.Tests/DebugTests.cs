using Heh8080.Core;

namespace Heh8080.Tests;

public class DebugTests
{
    #region GetTraceState Tests

    [Fact]
    public void GetTraceState_ReturnsCorrectValues_8080()
    {
        var memory = new Memory();
        var ioBus = new IoBus();
        var cpu = new Cpu8080(memory, ioBus);

        cpu.A = 0x12;
        cpu.B = 0x34;
        cpu.C = 0x56;
        cpu.D = 0x78;
        cpu.E = 0x9A;
        cpu.H = 0xBC;
        cpu.L = 0xDE;
        cpu.SP = 0xF000;
        cpu.PC = 0x1234;
        cpu.FlagZ = true;
        cpu.FlagCY = true;

        var state = cpu.GetTraceState();

        Assert.Equal(0x12, state.A);
        Assert.Equal(0x34, state.B);
        Assert.Equal(0x56, state.C);
        Assert.Equal(0x78, state.D);
        Assert.Equal(0x9A, state.E);
        Assert.Equal(0xBC, state.H);
        Assert.Equal(0xDE, state.L);
        Assert.Equal(0xF000, state.SP);
        Assert.Equal(0x1234, state.PC);
        // Flags: Z=0x40, CY=0x01, bit1=0x02 = 0x43
        Assert.Equal(0x43, state.Flags);
    }

    [Fact]
    public void GetTraceState_ReturnsCorrectValues_Z80()
    {
        var memory = new Memory();
        var ioBus = new IoBus();
        var cpu = new CpuZ80(memory, ioBus);

        cpu.A = 0xAB;
        cpu.B = 0xCD;
        cpu.PC = 0x5678;
        cpu.SP = 0xEF00;

        var state = cpu.GetTraceState();

        Assert.Equal(0xAB, state.A);
        Assert.Equal(0xCD, state.B);
        Assert.Equal(0x5678, state.PC);
        Assert.Equal(0xEF00, state.SP);
    }

    #endregion

    #region TraceBuffer Tests

    [Fact]
    public void TraceBuffer_Add_StoresEntry()
    {
        var buffer = new TraceBuffer(4);
        var entry = new TraceEntry(0x100, 0x3E, 0x42, 0x00, 0, 0, 0, 0, 0, 0, 0, 0xFFFF, 0x02);

        buffer.Add(entry);

        Assert.Equal(1, buffer.Count);
        var entries = buffer.GetEntries();
        Assert.Single(entries);
        Assert.Equal(0x100, entries[0].PC);
        Assert.Equal(0x3E, entries[0].Opcode);
    }

    [Fact]
    public void TraceBuffer_WhenFull_OverwritesOldest()
    {
        var buffer = new TraceBuffer(3);
        buffer.Add(new TraceEntry(0x100, 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        buffer.Add(new TraceEntry(0x101, 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        buffer.Add(new TraceEntry(0x102, 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        // Buffer full, this should overwrite first
        buffer.Add(new TraceEntry(0x103, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        Assert.Equal(3, buffer.Count);
        var entries = buffer.GetEntries();
        Assert.Equal(3, entries.Length);
        // Should have 0x101, 0x102, 0x103 (oldest 0x100 overwritten)
        Assert.Equal(0x101, entries[0].PC);
        Assert.Equal(0x102, entries[1].PC);
        Assert.Equal(0x103, entries[2].PC);
    }

    [Fact]
    public void TraceBuffer_GetEntries_ReturnsChronologicalOrder()
    {
        var buffer = new TraceBuffer(4);
        buffer.Add(new TraceEntry(0x100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        buffer.Add(new TraceEntry(0x101, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        buffer.Add(new TraceEntry(0x102, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var entries = buffer.GetEntries();

        Assert.Equal(0x100, entries[0].PC);
        Assert.Equal(0x101, entries[1].PC);
        Assert.Equal(0x102, entries[2].PC);
    }

    [Fact]
    public void TraceBuffer_Clear_ResetsBuffer()
    {
        var buffer = new TraceBuffer(4);
        buffer.Add(new TraceEntry(0x100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        buffer.Add(new TraceEntry(0x101, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.GetEntries());
    }

    #endregion

    #region Emulator Breakpoint Tests

    [Fact]
    public void Emulator_SetBreakpoint_AddsToCollection()
    {
        var emulator = new Emulator(CpuType.Intel8080);

        emulator.SetBreakpoint(0x100);
        emulator.SetBreakpoint(0x200);

        Assert.Contains((ushort)0x100, emulator.Breakpoints);
        Assert.Contains((ushort)0x200, emulator.Breakpoints);
    }

    [Fact]
    public void Emulator_ClearBreakpoint_RemovesFromCollection()
    {
        var emulator = new Emulator(CpuType.Intel8080);
        emulator.SetBreakpoint(0x100);
        emulator.SetBreakpoint(0x200);

        emulator.ClearBreakpoint(0x100);

        Assert.DoesNotContain((ushort)0x100, emulator.Breakpoints);
        Assert.Contains((ushort)0x200, emulator.Breakpoints);
    }

    [Fact]
    public async Task Emulator_Breakpoint_StopsAtAddress()
    {
        var emulator = new Emulator(CpuType.Intel8080);
        // Load: NOP NOP NOP HLT at 0x0000
        emulator.Load(0, new byte[] { 0x00, 0x00, 0x00, 0x76 });
        emulator.SetBreakpoint(0x0002);

        emulator.Start();
        await Task.Delay(100); // Give it time to hit breakpoint

        Assert.True(emulator.BreakpointHit);
        Assert.Equal(0x0002, emulator.HitAddress);
        Assert.Equal(0x0002, emulator.Cpu.PC);
        Assert.False(emulator.IsRunning);

        emulator.Dispose();
    }

    [Fact]
    public async Task Emulator_ClearHit_AllowsResume()
    {
        var emulator = new Emulator(CpuType.Intel8080);
        // NOP NOP NOP HLT
        emulator.Load(0, new byte[] { 0x00, 0x00, 0x00, 0x76 });
        emulator.SetBreakpoint(0x0001);

        emulator.Start();
        await Task.Delay(100);

        Assert.True(emulator.BreakpointHit);
        Assert.Equal(0x0001, emulator.Cpu.PC);

        // Clear breakpoint and resume
        emulator.ClearBreakpoint(0x0001);
        emulator.ClearHit();
        emulator.Start();
        await Task.Delay(100);

        // Should have run to HLT
        Assert.True(emulator.Cpu.Halted);
        Assert.Equal(0x0004, emulator.Cpu.PC);

        emulator.Dispose();
    }

    #endregion

    #region Emulator Trace Tests

    [Fact]
    public void Emulator_TraceDisabledByDefault()
    {
        var emulator = new Emulator(CpuType.Intel8080);
        Assert.False(emulator.TraceEnabled);
    }

    [Fact]
    public async Task Emulator_TraceEnabled_CapturesInstructions()
    {
        var emulator = new Emulator(CpuType.Intel8080);
        // MVI A, 42h ; NOP ; HLT
        emulator.Load(0, new byte[] { 0x3E, 0x42, 0x00, 0x76 });
        emulator.TraceEnabled = true;

        emulator.Start();
        await Task.Delay(100);

        var entries = emulator.TraceBuffer.GetEntries();
        Assert.True(entries.Length >= 3); // At least 3 instructions

        // First instruction: MVI A at PC=0
        Assert.Equal(0x0000, entries[0].PC);
        Assert.Equal(0x3E, entries[0].Opcode);

        // Second instruction: NOP at PC=2
        Assert.Equal(0x0002, entries[1].PC);
        Assert.Equal(0x00, entries[1].Opcode);

        // After MVI A, 42h - A should be 0x42
        Assert.Equal(0x42, entries[1].A);

        emulator.Dispose();
    }

    #endregion
}
