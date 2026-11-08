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
        var expected = Path.Combine(solutionDir, "ui", "Theming", "Icons.axaml");

        Assert.True(File.Exists(expected), "No existe Icons.axaml versionado.");

        // Las pruebas no usan la red: si falta la caché, fallan con un mensaje
        // accionable en lugar de saltarse o descargar.
        foreach (var pin in new[]
        {
            DesignData.Pins.SpectrumCssWorkflowIcons,
            DesignData.Pins.SpectrumCssUiIcons,
        })
        {
            var packageDir = Path.Combine(
                DesignData.DesignDataCache.CacheRoot,
                pin.CacheDirectoryName,
                "package");
            if (!Directory.Exists(packageDir))
            {
                Assert.Fail(
                    "Falta la caché de paquetes de diseño fijados. " +
                    "Ejecute `dotnet run --project tools/Transcriptor.Tools -- fetch` para descargar y verificar " +
                    $"el paquete {pin.Name}@{pin.Version}.");
            }
        }

        var regenerated = DesignData.DesignDataApi.GenerateIcons();
        var versioned = File.ReadAllText(expected);
        Assert.True(versioned == regenerated,
            "Icons.axaml no coincide con la regeneración de los paquetes fijados. " +
            "Ejecute 'dotnet run --project tools/Transcriptor.Tools -- icons' y confirme el resultado.");
    }

    private static string GetSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Transcriptor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
