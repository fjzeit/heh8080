namespace Heh8080.Core;

/// <summary>
/// Common interface for CPU implementations (8080, Z80).
/// </summary>
public interface ICpu
{
    /// <summary>Program counter.</summary>
    ushort PC { get; set; }

    /// <summary>Stack pointer.</summary>
    ushort SP { get; set; }

    /// <summary>True if the CPU is halted.</summary>
    bool Halted { get; }

    /// <summary>True if interrupts are enabled.</summary>
    bool InterruptsEnabled { get; }

    /// <summary>
    /// Execute one instruction.
    /// </summary>
    /// <returns>Number of cycles consumed.</returns>
    int Step();

    /// <summary>
    /// Reset CPU to initial state.
    /// </summary>
    void Reset();

    /// <summary>
    /// Trigger an interrupt.
    /// </summary>
    /// <param name="vector">Interrupt vector (0-7 for RST).</param>
    void Interrupt(byte vector);

    /// <summary>
    /// Get CPU state for trace logging.
    /// </summary>
    /// <returns>Tuple of (A, B, C, D, E, H, L, SP, PC, Flags).</returns>
    (byte A, byte B, byte C, byte D, byte E, byte H, byte L, ushort SP, ushort PC, byte Flags) GetTraceState();
}
