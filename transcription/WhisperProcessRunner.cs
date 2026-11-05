using System.Diagnostics;
using System.Text;

namespace Transcriptor.Transcription;

/// <summary>Result of one Whisper.cpp invocation, with bounded captured output.</summary>
public sealed record WhisperProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Port for running Whisper.cpp locally. The default implementation uses the platform
/// process runner; test and development heads inject fakes through
/// <see cref="TranscriptionEngine"/> constructors.
/// </summary>
public interface IWhisperProcessRunner
{
    Task<WhisperProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Executes Whisper while continuously draining both redirected streams.
/// Captured output is bounded so a noisy or hostile child cannot exhaust memory.
/// </summary>
internal sealed class WhisperProcessRunner(TimeSpan? timeout = null, int maximumCapturedCharacters = 64 * 1024)
    : IWhisperProcessRunner
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(30);

    public async Task<WhisperProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (_timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCapturedCharacters);
        if (!startInfo.RedirectStandardOutput || !startInfo.RedirectStandardError)
            throw new ArgumentException("Whisper must run with stdout and stderr redirected.", nameof(startInfo));

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("No se pudo iniciar whisper-cli.");

        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);

        var stdoutTask = ReadBoundedAsync(process.StandardOutput, maximumCapturedCharacters);
        var stderrTask = ReadBoundedAsync(process.StandardError, maximumCapturedCharacters);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            throw new TimeoutException($"whisper-cli superó el tiempo límite de {_timeout}.");
        }

        var standardOutput = await stdoutTask.ConfigureAwait(false);
        var standardError = await stderrTask.ConfigureAwait(false);
        return new WhisperProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maximumCharacters)
    {
        var captured = new StringBuilder(Math.Min(maximumCharacters, 4096));
        var buffer = new char[4096];
        var remaining = maximumCharacters;

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
            if (read == 0)
                break;

            var toCapture = Math.Min(read, remaining);
            if (toCapture > 0)
            {
                captured.Append(buffer, 0, toCapture);
                remaining -= toCapture;
            }
            // Continue draining after the capture limit to avoid pipe deadlocks.
        }

        return captured.ToString();
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
