// SPDX-License-Identifier: EUPL-1.2
using System.Diagnostics;
using Xfir.CommandLine;
using Xunit;

namespace Xfir.Tests;

public class StartupProcessTests
{
    internal static string Probe => Path.Combine(AppContext.BaseDirectory, "startup-probe", "Xfir.StartupProbe.exe");

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task InvalidOptionsExitWithoutRequestingAWindow(string[] args)
    {
        Assert.False(AppArguments.OpensWindow(args));
        var result = await Run(args);
        Assert.Equal(2, result.Code);
        Assert.Contains("Uso:", result.Error);
        Assert.DoesNotContain("GUI_REQUESTED", result.Output);
    }

    public static TheoryData<string[]> InvalidArguments => new()
    {
        new[] { "--pfd" }, new[] { "--help", "extra" }, new[] { "--pdf" }, new[] { "file.xfir", "extra.pdf" }
    };

    [Fact]
    public async Task HelpExitsWithoutOpeningAWindow()
    {
        var result = await Run(["--help"]);
        Assert.Equal(0, result.Code);
        Assert.Contains("Uso:", result.Output);
        Assert.Equal("", result.Error);
    }

    [Fact]
    public async Task NormalGuiRoutesRemainAvailable()
    {
        foreach (var args in new[] { Array.Empty<string>(), new[] { "file with spaces.xfir" } })
        {
            Assert.True(AppArguments.OpensWindow(args));
            var result = await Run(args);
            Assert.Equal(203, result.Code);
            Assert.Contains("GUI_REQUESTED", result.Output);
        }
    }

    [Fact]
    public async Task ActualStartupCodeConvertsUsingPathsWithSpaces()
    {
        var folder = Path.Combine(Path.GetTempPath(), "xfir-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var source = Path.Combine(folder, "form with spaces.xfir");
            var destination = Path.Combine(folder, "copy with spaces.pdf");
            File.WriteAllBytes(source, ReaderTests.Sample());
            var result = await Run(["--pdf", source, destination]);
            Assert.Equal(0, result.Code);
            Assert.True(File.Exists(destination));
            Assert.Contains("PDF creato:", result.Output);
            Assert.Equal("", result.Error);
        }
        finally { Directory.Delete(folder, true); }
    }

    private static async Task<(int Code, string Output, string Error)> Run(string[] args)
    {
        var start = new ProcessStartInfo(Probe)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(true); throw; }
        return (process.ExitCode, await output, await error);
    }
}
