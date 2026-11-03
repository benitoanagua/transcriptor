using System.Diagnostics;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class WhisperProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_DrainsBothStreamsAndBoundsCapturedOutput()
    {
        var runner = new WhisperProcessRunner(TimeSpan.FromSeconds(15), maximumCapturedCharacters: 128);
        var startInfo = PowerShell(
            "$i=0; while($i -lt 12000){ [Console]::Out.WriteLine('stdout-payload'); [Console]::Error.WriteLine('stderr-payload'); $i++ }");

        var result = await runner.RunAsync(startInfo);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(128, result.StandardOutput.Length);
        Assert.Equal(128, result.StandardError.Length);
    }

    [Fact]
    public async Task RunAsync_ReturnsNonZeroExitCodeAndCapturedError()
    {
        var runner = new WhisperProcessRunner(TimeSpan.FromSeconds(10));
        var startInfo = PowerShell("[Console]::Error.WriteLine('synthetic failure'); exit 17");

        var result = await runner.RunAsync(startInfo);

        Assert.Equal(17, result.ExitCode);
        Assert.Contains("synthetic failure", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_KillsProcessTreeWhenTimeoutExpires()
    {
        var runner = new WhisperProcessRunner(TimeSpan.FromMilliseconds(300));
        var startInfo = PowerShell("Start-Sleep -Seconds 30");

        await Assert.ThrowsAsync<TimeoutException>(() => runner.RunAsync(startInfo));
    }

    [Fact]
    public async Task RunAsync_KillsDescendantProcessesWhenTimeoutExpires()
    {
        var childPidPath = Path.Combine(Path.GetTempPath(), $"transcriptor-child-{Guid.NewGuid():N}.txt");
        var command = $"$child = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-Command','Start-Sleep -Seconds 30') -PassThru; Set-Content -Path '{childPidPath}' -Value $child.Id; Start-Sleep -Seconds 30";
        var runner = new WhisperProcessRunner(TimeSpan.FromSeconds(3));

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => runner.RunAsync(PowerShell(command)));
            Assert.True(File.Exists(childPidPath));

            var childPid = int.Parse(File.ReadAllText(childPidPath));
            try
            {
                using var child = Process.GetProcessById(childPid);
                Assert.True(child.WaitForExit(5000), "El proceso hijo sobrevivió a la cancelación del árbol.");
            }
            catch (ArgumentException)
            {
                // El proceso hijo ya terminó y dejó de estar registrado en Windows.
            }
        }
        finally
        {
            if (File.Exists(childPidPath)) File.Delete(childPidPath);
        }
    }

    [Fact]
    public async Task RunAsync_KillsProcessTreeWhenCallerCancels()
    {
        var runner = new WhisperProcessRunner(TimeSpan.FromSeconds(30));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var startInfo = PowerShell("Start-Sleep -Seconds 30");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(startInfo, cancellation.Token));
    }

    private static ProcessStartInfo PowerShell(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);
        return startInfo;
    }
}
