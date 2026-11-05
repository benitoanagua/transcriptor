using System.Diagnostics;
using Transcriptor.Transcription;
using Xunit;

namespace Transcriptor.Transcription.Tests.Windows;

// Test exclusivo de Windows: verifica el descuelgue del árbol de procesos hijo
// con powershell.exe; la suite portable solo comprueba timeout/cancelación.
public sealed class WhisperProcessTreeTests
{
    [Fact]
    public async Task RunAsync_KillsDescendantProcessesWhenTimeoutExpires()
    {
        var childPidPath = Path.Combine(Path.GetTempPath(), $"transcriptor-child-{Guid.NewGuid():N}.txt");
        var command = $"$child = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-Command','Start-Sleep -Seconds 30') -PassThru; Set-Content -Path '{childPidPath}' -Value $child.Id; Start-Sleep -Seconds 30";
        var runner = new WhisperProcessRunner(TimeSpan.FromSeconds(10));

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
