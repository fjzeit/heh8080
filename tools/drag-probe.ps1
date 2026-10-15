#requires -Version 7
<#
.SYNOPSIS
Measures how smoothly the borderless desktop window follows a real mouse drag.

.DESCRIPTION
Launches Heh8080.Desktop, clicks the requested scale button, then drives a left-button drag on the
bezel with SendInput while a separate thread samples GetWindowRect every millisecond.
Reports how often the window actually moved and how far it trailed the cursor.
Moves the real mouse cursor, so leave the machine alone while it runs.
#>
param(
    [ValidateSet(1.0, 0.8, 0.6, 0.4)]
    [double]$Scale = 1.0,
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\Heh8080.Desktop\bin\Debug\net10.0\Heh8080.Desktop.exe'),
    [int]$StepsPerLeg = 60,
    [int]$StepIntervalMs = 8,
    [int]$LegPixels = 400,
    [int]$Legs = 4,
    [string]$TracePath,
    [switch]$Calibrate,
    [int]$CalibrateWidth = 1200,
    [int]$CalibrateHeight = 900
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

public static class DragProbe
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public MOUSEINPUT mi; }

    const uint MOUSEEVENTF_MOVE = 0x0001, MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004,
        MOUSEEVENTF_ABSOLUTE = 0x8000, MOUSEEVENTF_VIRTUALDESK = 0x4000;

    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);

    public static void MakeDpiAware() => SetThreadDpiAwarenessContext(PerMonitorAwareV2);

    public static RECT Rect(IntPtr hwnd) { MakeDpiAware(); GetWindowRect(hwnd, out var r); return r; }

    public static double DpiScale(IntPtr hwnd) => GetDpiForWindow(hwnd) / 96.0;

    public static void Focus(IntPtr hwnd) => SetForegroundWindow(hwnd);

    static void Mouse(uint flags, int x = 0, int y = 0)
    {
        int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77), vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
        var input = new INPUT { type = 0, mi = new MOUSEINPUT
        {
            dx = (int)Math.Round((x - vx) * 65535.0 / (vw - 1)),
            dy = (int)Math.Round((y - vy) * 65535.0 / (vh - 1)),
            dwFlags = flags | MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK
        }};
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public static void Click(int x, int y)
    {
        MakeDpiAware();
        Mouse(0, x, y); Thread.Sleep(50);
        Mouse(MOUSEEVENTF_LEFTDOWN, x, y); Thread.Sleep(50);
        Mouse(MOUSEEVENTF_LEFTUP, x, y);
    }

    public static string Drag(IntPtr hwnd, int grabX, int grabY, int legs, int stepsPerLeg, int stepMs, int legPixels, string tracePath)
    {
        MakeDpiAware();
        var start = Rect(hwnd);
        var samples = new List<(double t, int left)>();
        var commands = new List<(double t, int offset)>();
        var clock = Stopwatch.StartNew();
        bool sampling = true;

        var sampler = new Thread(() =>
        {
            MakeDpiAware();
            int last = int.MinValue;
            while (Volatile.Read(ref sampling))
            {
                GetWindowRect(hwnd, out var r);
                if (r.Left != last) { lock (samples) samples.Add((clock.Elapsed.TotalMilliseconds, r.Left)); last = r.Left; }
                Thread.SpinWait(200);
            }
        }) { IsBackground = true, Priority = ThreadPriority.Highest };
        sampler.Start();

        Mouse(0, grabX, grabY); Thread.Sleep(100);
        Mouse(MOUSEEVENTF_LEFTDOWN, grabX, grabY); Thread.Sleep(100);

        double dragStart = clock.Elapsed.TotalMilliseconds;
        int offset = 0;
        for (int leg = 0; leg < legs; leg++)
        {
            int dir = leg % 2 == 0 ? 1 : -1;
            for (int i = 1; i <= stepsPerLeg; i++)
            {
                double due = dragStart + (leg * stepsPerLeg + i) * stepMs;
                while (clock.Elapsed.TotalMilliseconds < due) Thread.SpinWait(50);
                offset = (dir > 0 ? 0 : legPixels) + dir * (int)Math.Round(legPixels * (double)i / stepsPerLeg);
                Mouse(0, grabX + offset, grabY);
                commands.Add((clock.Elapsed.TotalMilliseconds, offset));
            }
        }
        double lastCommand = clock.Elapsed.TotalMilliseconds;

        int target = start.Left + offset;
        double settleDeadline = lastCommand + 5000;
        while (Rect(hwnd).Left != target && clock.Elapsed.TotalMilliseconds < settleDeadline) Thread.Sleep(1);
        double settled = clock.Elapsed.TotalMilliseconds;

        Mouse(MOUSEEVENTF_LEFTUP, grabX + offset, grabY);
        Thread.Sleep(100);
        Volatile.Write(ref sampling, false);
        sampler.Join();

        var moves = samples.Where(s => s.t >= dragStart && s.t <= settled).ToList();
        var gaps = moves.Zip(moves.Skip(1), (a, b) => b.t - a.t).DefaultIfEmpty(0).ToList();
        var lags = commands.Select(c =>
        {
            var hit = moves.FirstOrDefault(m => m.t >= c.t && m.left - start.Left == c.offset);
            return hit == default ? double.NaN : hit.t - c.t;
        }).Where(l => !double.IsNaN(l)).OrderBy(l => l).ToList();

        double driveMs = lastCommand - dragStart;
        if (!string.IsNullOrEmpty(tracePath))
            System.IO.File.WriteAllLines(tracePath,
                new[] { "kind,t_ms,offset" }
                    .Concat(commands.Select(c => $"input,{c.t - dragStart:F1},{c.offset}"))
                    .Concat(moves.Select(m => $"window,{m.t - dragStart:F1},{m.left - start.Left}")));
        return string.Join(Environment.NewLine, new[]
        {
            $"window moves      : {moves.Count} in {driveMs:F0} ms of input ({moves.Count * 1000.0 / Math.Max(1, settled - dragStart):F1} moves/s, input was {commands.Count * 1000.0 / driveMs:F1} events/s)",
            $"move gap ms       : median {Percentile(gaps.OrderBy(g => g).ToList(), 0.5):F1}, p95 {Percentile(gaps.OrderBy(g => g).ToList(), 0.95):F1}, max {gaps.Max():F1}",
            $"cursor->window ms : matched {lags.Count}/{commands.Count}, median {Percentile(lags, 0.5):F1}, p95 {Percentile(lags, 0.95):F1}",
            $"settle after input: {settled - lastCommand:F0} ms (reached target: {Rect(hwnd).Left == target || moves.LastOrDefault().left == target})"
        });
    }

    static double Percentile(List<double> sorted, double p) =>
        sorted.Count == 0 ? double.NaN : sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(p * sorted.Count))];
}
'@

$scales = 1.0, 0.8, 0.6, 0.4
$startupScale = 0.6

if ($Calibrate) {
    $formScript = "Add-Type -AssemblyName System.Windows.Forms; `$f = [Windows.Forms.Form]::new(); `$f.Text = 'drag-probe calibration'; `$f.Width = $CalibrateWidth; `$f.Height = $CalibrateHeight; [Windows.Forms.Application]::Run(`$f)"
    $proc = Start-Process pwsh -ArgumentList '-NoProfile', '-Command', $formScript -PassThru
    try {
        $deadline = (Get-Date).AddSeconds(20)
        while ($proc.MainWindowHandle -eq 0) {
            if ((Get-Date) -gt $deadline) { throw 'Timed out waiting for the calibration window' }
            Start-Sleep -Milliseconds 100
            $proc.Refresh()
        }
        $hwnd = $proc.MainWindowHandle
        Start-Sleep -Seconds 1
        [DragProbe]::Focus($hwnd)
        $r = [DragProbe]::Rect($hwnd)
        "calibration form | window $($r.Right - $r.Left)x$($r.Bottom - $r.Top) px"
        [DragProbe]::Drag($hwnd, $r.Left + 300, $r.Top + 20, $Legs, $StepsPerLeg, $StepIntervalMs, $LegPixels, $TracePath)
    }
    finally { if (-not $proc.HasExited) { $proc.Kill() } }
    return
}

$proc = Start-Process -FilePath (Resolve-Path $Exe) -PassThru
try {
    $deadline = (Get-Date).AddSeconds(20)
    while ($proc.MainWindowHandle -eq 0) {
        if ((Get-Date) -gt $deadline) { throw 'Timed out waiting for the main window' }
        Start-Sleep -Milliseconds 100
        $proc.Refresh()
    }
    $hwnd = $proc.MainWindowHandle
    Start-Sleep -Seconds 3
    [DragProbe]::Focus($hwnd)

    $dpi = [DragProbe]::DpiScale($hwnd)
    $r = [DragProbe]::Rect($hwnd)
    $widthDip = ($r.Right - $r.Left) / $dpi
    $buttonIndex = [array]::IndexOf($scales, $Scale)
    $s = $startupScale
    $buttonX = $r.Left + [int](($widthDip - 164 * $s + $buttonIndex * 40 * $s + 18 * $s) * $dpi)
    $buttonY = $r.Top + [int](18 * $s * $dpi)

    if ($Scale -ne $startupScale) {
        [DragProbe]::Click($buttonX, $buttonY)
        $deadline = (Get-Date).AddSeconds(5)
        while (([DragProbe]::Rect($hwnd)).Right - ([DragProbe]::Rect($hwnd)).Left -eq $r.Right - $r.Left) {
            if ((Get-Date) -gt $deadline) { throw "Scale button click at ($buttonX,$buttonY) did not resize the window" }
            Start-Sleep -Milliseconds 50
        }
        Start-Sleep -Seconds 2
    }

    $r = [DragProbe]::Rect($hwnd)
    $grabX = $r.Left + [int](24 * $Scale * $dpi)
    $grabY = $r.Top + [int](($r.Bottom - $r.Top) / 2)
    "scale $($Scale * 100)% | window $($r.Right - $r.Left)x$($r.Bottom - $r.Top) px at ($($r.Left),$($r.Top)) | dpi x$dpi"

    $cpuBefore = $proc.TotalProcessorTime
    $wall = [Diagnostics.Stopwatch]::StartNew()
    [DragProbe]::Drag($hwnd, $grabX, $grabY, $Legs, $StepsPerLeg, $StepIntervalMs, $LegPixels, $TracePath)
    $proc.Refresh()
    "app cpu during drag: $([int](($proc.TotalProcessorTime - $cpuBefore).TotalMilliseconds / $wall.Elapsed.TotalMilliseconds * 100))% of one core"
}
finally {
    if (-not $proc.HasExited) { $proc.Kill() }
}
