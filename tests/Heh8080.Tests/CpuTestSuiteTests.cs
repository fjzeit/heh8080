using System.Text;
using Heh8080.Core;
using Xunit.Abstractions;

namespace Heh8080.Tests;

/// <summary>
/// Integration tests using standard 8080 CPU test suites.
/// Tests pass trivially if test files are not available - check output for skip messages.
/// </summary>
public class CpuTestSuiteTests
{
    private readonly ITestOutputHelper _output;

    public CpuTestSuiteTests(ITestOutputHelper output)
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
    public void TST8080_PassesAllTests()
    {
        var path = GetTestFilePath("TST8080.COM");
        if (path == null)
        {
            _output.WriteLine("SKIPPED: TST8080.COM not found - set HEH8080_CPU_TESTS env var");
            return;
        }

        _output.WriteLine($"Running {path}");
        var output = new StringBuilder();
        var harness = new CpmTestHarness(c => output.Append(c));
        harness.LoadCom(path);

        var completed = harness.Run(1_000_000);

        _output.WriteLine(output.ToString());
        Assert.True(completed, $"Test did not complete. Output:\n{output}");
        Assert.Contains("CPU IS OPERATIONAL", output.ToString());
    }

    [Fact]
    public void Prelim8080_PassesAllTests()
    {
        var path = GetTestFilePath("8080PRE.COM");
        if (path == null)
        {
            _output.WriteLine("SKIPPED: 8080PRE.COM not found - set HEH8080_CPU_TESTS env var");
            return;
        }

        _output.WriteLine($"Running {path}");
        var output = new StringBuilder();
        var harness = new CpmTestHarness(c => output.Append(c));
        harness.LoadCom(path);

        var completed = harness.Run(10_000_000);

        _output.WriteLine(output.ToString());
        Assert.True(completed, $"Test did not complete. Output:\n{output}");
        Assert.DoesNotContain("ERROR", output.ToString().ToUpperInvariant());
    }

    [Fact]
    public void CPUTEST_PassesAllTests()
    {
        var path = GetTestFilePath("CPUTEST.COM");
        if (path == null)
        {
            _output.WriteLine("SKIPPED: CPUTEST.COM not found - set HEH8080_CPU_TESTS env var");
            return;
        }

        _output.WriteLine($"Running {path}");
        var output = new StringBuilder();
        var harness = new CpmTestHarness(c => output.Append(c));
        harness.LoadCom(path);

        var completed = harness.Run(100_000_000);

        _output.WriteLine(output.ToString());
        Assert.True(completed, $"Test did not complete. Output:\n{output}");
        Assert.DoesNotContain("ERROR", output.ToString().ToUpperInvariant());
    }

    [Fact(Skip = "8080EXM takes hours to complete - run manually")]
    public void Exerciser8080_PassesAllTests()
    {
        var path = GetTestFilePath("8080EXM.COM");
        if (path == null)
        {
            _output.WriteLine("SKIPPED: 8080EXM.COM not found - set HEH8080_CPU_TESTS env var");
            return;
        }

        _output.WriteLine($"Running {path}");
        var output = new StringBuilder();
        var harness = new CpmTestHarness(c => output.Append(c));
        harness.LoadCom(path);

        // This test takes hours at full speed
        var completed = harness.Run(long.MaxValue);

        _output.WriteLine(output.ToString());
        Assert.True(completed, $"Test did not complete. Output:\n{output}");
        Assert.DoesNotContain("ERROR", output.ToString().ToUpperInvariant());
    }
}
