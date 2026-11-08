using System.Diagnostics;
using System.IO;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// Los iconos de <see cref="Icons.axaml"/> se regeneran desde los paquetes oficiales
/// <c>@adobe/spectrum-css-workflow-icons@5.0.0</c> y <c>@spectrum-css/ui-icons@1.1.2</c>
/// (SHA-512 verificado) y deben coincidir exactamente con el archivo versionado.
/// No se permiten ediciones manuales de las geometrías de icono.
/// </summary>
public sealed class IconRegenerationTests
{
    [Fact]
    public void IconsAxamlCoincideConLaRegeneracionDeLosPaquetesFijados()
    {
        var solutionDir = GetSolutionDirectory();
        var generator = Path.Combine(solutionDir, "tools", "generate-spectrum-icons.py");
        var expected = Path.Combine(solutionDir, "ui", "Theming", "Icons.axaml");

        Assert.True(File.Exists(generator), "No existe el generador de iconos.");
        Assert.True(File.Exists(expected), "No existe Icons.axaml versionado.");

        var output = Path.Combine(Path.GetTempPath(), $"Icons-{Guid.NewGuid():N}.axaml");
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{generator}\" --output \"{output}\"",
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
                $"El generador de iconos fall\u00f3: {stderr}");
            Assert.True(File.Exists(output), "El generador no produjo el archivo de salida.");

            var regenerated = File.ReadAllText(output);
            var versioned = File.ReadAllText(expected);
            Assert.True(versioned == regenerated,
                "Icons.axaml no coincide con la regeneraci\u00f3n de los paquetes fijados. " +
                "Ejecute 'tools/generate-spectrum-icons.py' y confirme el resultado.");
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
