using System.Diagnostics;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Tests;

public sealed class WhisperProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_DrainsBothStreamsAndBoundsCapturedOutput()
    {
        var runner = new WhisperProcessRunner(TimeSpan.FromSeconds(15), maximumCapturedCharacters: 128);
        var startInfo = Shell(
            windowsCommand: "$i=0; while($i -lt 12000){ [Console]::Out.WriteLine('stdout-payload'); [Console]::Error.WriteLine('stderr-payload'); $i++ }",
            unixCommand: "i=0; while [ $i -lt 12000 ]; do echo stdout-payload; echo stderr-payload >&2; i=$((i+1)); done");

        var result = await runner.RunAsync(startInfo);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(128, result.StandardOutput.Length);
        Assert.Equal(128, result.StandardError.Length);
    }

    [Fact]
    public async Task RunAsync_ReturnsNonZeroExitCodeAndCapturedError()
    {
        var runner = new WhisperProcessRunner(TimeSpan.FromSeconds(10));
        var startInfo = Shell(
            windowsCommand: "[Console]::Error.WriteLine('synthetic failure'); exit 17",
            unixCommand: "echo 'synthetic failure' >&2; exit 17");

        var result = await runner.RunAsync(startInfo);

        Assert.Equal(17, result.ExitCode);
        Assert.Contains("synthetic failure", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_KillsProcessTreeWhenTimeoutExpires()
    {
        var runner = new WhisperProcessRunner(TimeSpan.FromMilliseconds(300));
        var startInfo = Shell(
            windowsCommand: "Start-Sleep -Seconds 30",
            unixCommand: "sleep 30");

        await Assert.ThrowsAsync<TimeoutException>(() => runner.RunAsync(startInfo));
    }

    [Fact]
    public async Task RunAsync_KillsProcessTreeWhenCallerCancels()
    {
        var runner = new WhisperProcessRunner(TimeSpan.FromSeconds(30));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var startInfo = Shell(
            windowsCommand: "Start-Sleep -Seconds 30",
            unixCommand: "sleep 30");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(startInfo, cancellation.Token));
    }

    private static ProcessStartInfo Shell(string windowsCommand, string unixCommand)
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
            : new ProcessStartInfo
            {
                FileName = "/bin/sh",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(windowsCommand);
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(unixCommand);
        }
        return startInfo;
    }
}
