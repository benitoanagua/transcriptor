using System.IO;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// RUI-01: <see cref="Tokens.axaml"/> se regenera desde el paquete oficial
/// <c>@adobe/spectrum-tokens@15.6.0</c> (SHA-512 verificado) y debe coincidir
/// exactamente con el archivo versionado. No se permiten ediciones manuales.
/// </summary>
public sealed class TokenRegenerationTests
{
    [Fact]
    public void TokensAxamlCoincideConLaRegeneracionDelPaqueteFijado()
    {
        var solutionDir = GetSolutionDirectory();
        var packageDir = Path.Combine(
            DesignData.DesignDataCache.CacheRoot,
            DesignData.Pins.SpectrumTokens.CacheDirectoryName,
            "package");
        var expected = Path.Combine(solutionDir, "ui", "Theming", "Tokens.axaml");

        Assert.True(File.Exists(expected), "No existe Tokens.axaml versionado.");

        // Las pruebas no usan la red: si falta la caché, fallan con un mensaje
        // accionable en lugar de saltarse o descargar.
        if (!Directory.Exists(packageDir))
        {
            Assert.Fail(
                "Falta la caché de paquetes de diseño fijados. " +
                "Ejecute `dotnet run --project tools/Transcriptor.Tools -- fetch` para descargar y verificar " +
                $"el paquete {DesignData.Pins.SpectrumTokens.Name}@{DesignData.Pins.SpectrumTokens.Version}.");
        }

        var regenerated = DesignData.DesignDataApi.GenerateTokens();
        var versioned = File.ReadAllText(expected);
        Assert.True(versioned == regenerated,
            "Tokens.axaml no coincide con la regeneración del paquete fijado. " +
            "Ejecute 'dotnet run --project tools/Transcriptor.Tools -- tokens' y confirme el resultado.");
    }

    private static string GetSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Transcriptor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
