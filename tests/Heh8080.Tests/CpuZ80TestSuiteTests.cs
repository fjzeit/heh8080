using System.Text;
using Heh8080.Core;
using Xunit.Abstractions;

namespace Heh8080.Tests;

/// <summary>
/// Integration tests using standard Z80 CPU test suites (ZEXDOC/ZEXALL).
/// Tests pass trivially if test files are not available - check output for skip messages.
/// </summary>
public class CpuZ80TestSuiteTests
{
    private readonly ITestOutputHelper _output;

    public CpuZ80TestSuiteTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? GetTestFilePath(string filename)
    {
        // Check environment variable first
        var envPath = Environment.GetEnvironmentVariable("HEH8080_CPU_TESTS");
        if (!string.IsNullOrEmpty(envPath))
        {
            var path = Path.Combine(envPath, filename);
            if (File.Exists(path)) return path;
        }

        // Check relative paths from test execution directory
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "cpu_tests", filename),
            Path.Combine(baseDir, "..", "..", "..", "..", "cpu_tests", filename),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "tests", "cpu_tests", filename),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "tests", "cpu_tests", filename),
        };

        foreach (var candidate in candidates)
        {
            var normalized = Path.GetFullPath(candidate);
            if (File.Exists(normalized)) return normalized;
        }

        return null;
    }

    [Fact]
    public void ZEXDOC_FirstTest_Passes()
    {
        var path = GetTestFilePath("ZEXDOC.COM");
        if (path == null)
        {
            _output.WriteLine("SKIPPED: ZEXDOC.COM not found - set HEH8080_CPU_TESTS env var");
            return;
        }

        _output.WriteLine($"Running {path} (first test only - full suite takes hours)");
        var output = new StringBuilder();
        var harness = new CpmTestHarnessZ80(c => output.Append(c));
        harness.LoadCom(path);

        // Run for limited instructions to complete first test (~3-4 min worth)
        // Each test outputs "OK" when complete
        long maxInstructions = 500_000_000; // ~30 seconds at full speed
        int okCount = 0;

        while (harness.InstructionCount < maxInstructions && !harness.HasExited)
        {
            harness.Step();

            // Check if we've seen an OK
            string current = output.ToString();
            int newOkCount = CountOccurrences(current, "OK");
            if (newOkCount > okCount)
            {
                okCount = newOkCount;
                _output.WriteLine($"Test {okCount} passed at instruction {harness.InstructionCount:N0}");
                if (okCount >= 1) break; // Stop after first OK
            }
        }

        _output.WriteLine(output.ToString());
        Assert.True(okCount >= 1, $"Expected at least one test to pass. Output:\n{output}");
        Assert.DoesNotContain("ERROR", output.ToString().ToUpperInvariant());
    }

    [Fact(Skip = "ZEXDOC takes ~4.5 hours to complete - run manually")]
    public void ZEXDOC_AllTests_Pass()
    {
        var path = GetTestFilePath("ZEXDOC.COM");
        if (path == null)
        {
            _output.WriteLine("SKIPPED: ZEXDOC.COM not found - set HEH8080_CPU_TESTS env var");
            return;
        }

        _output.WriteLine($"Running {path} (full suite - this will take hours)");
        var output = new StringBuilder();
        var harness = new CpmTestHarnessZ80(c => output.Append(c));
        harness.LoadCom(path);

        var completed = harness.Run(long.MaxValue);

        _output.WriteLine(output.ToString());
        Assert.True(completed, $"Test did not complete. Output:\n{output}");
        Assert.DoesNotContain("ERROR", output.ToString().ToUpperInvariant());
    }

    [Fact(Skip = "ZEXALL takes ~4.5 hours to complete - run manually")]
    public void ZEXALL_AllTests_Pass()
    {
        var path = GetTestFilePath("ZEXALL.COM");
        if (path == null)
        {
            _output.WriteLine("SKIPPED: ZEXALL.COM not found - set HEH8080_CPU_TESTS env var");
            return;
        }

        _output.WriteLine($"Running {path} (full suite - this will take hours)");
        var output = new StringBuilder();
        var harness = new CpmTestHarnessZ80(c => output.Append(c));
        harness.LoadCom(path);

        var completed = harness.Run(long.MaxValue);

        _output.WriteLine(output.ToString());
        Assert.True(completed, $"Test did not complete. Output:\n{output}");
        Assert.DoesNotContain("ERROR", output.ToString().ToUpperInvariant());
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int i = 0;
        while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
        {
            count++;
            i += pattern.Length;
        }
        return count;
    }
}
