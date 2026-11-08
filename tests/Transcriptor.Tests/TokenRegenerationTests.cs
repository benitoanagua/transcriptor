using System.Diagnostics;
using System.IO;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// RUI-01: <see cref="Tokens.axaml"/> se regenera desde el paquete oficial
/// <c>@adobe/spectrum-tokens</c> fijado (SHA-512 verificado) y debe coincidir
/// exactamente con el archivo versionado. No se permiten ediciones manuales.
/// </summary>
public sealed class TokenRegenerationTests
{
    [Fact]
    public void TokensAxamlCoincideConLaRegeneracionDelPaqueteFijado()
    {
        var solutionDir = GetSolutionDirectory();
        var generator = Path.Combine(solutionDir, "tools", "generate-spectrum-tokens.py");
        var packageDir = Path.Combine(solutionDir, "tools", "spectrum-tokens-package", "package");
        var expected = Path.Combine(solutionDir, "ui", "Theming", "Tokens.axaml");

        Assert.True(File.Exists(generator), "No existe el generador de tokens.");
        Assert.True(Directory.Exists(packageDir), "No existe el paquete de tokens fijado.");
        Assert.True(File.Exists(expected), "No existe Tokens.axaml versionado.");

        var output = Path.Combine(Path.GetTempPath(), $"Tokens-{Guid.NewGuid():N}.axaml");
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{generator}\" --package-dir \"{packageDir}\" --output \"{output}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = solutionDir,
            };
            using var process = Process.Start(start)!;
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0,
                $"El generador de tokens fall\u00f3: {stderr}");
            Assert.True(File.Exists(output), "El generador no produjo el archivo de salida.");

            var regenerated = File.ReadAllText(output);
            var versioned = File.ReadAllText(expected);
            Assert.True(versioned == regenerated,
                "Tokens.axaml no coincide con la regeneraci\u00f3n del paquete fijado. " +
                "Ejecute 'tools/generate-spectrum-tokens.py' y confirme el resultado.");
        }
        finally
        {
            if (File.Exists(output))
                File.Delete(output);
        }
    }

    private static string GetSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Transcriptor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
