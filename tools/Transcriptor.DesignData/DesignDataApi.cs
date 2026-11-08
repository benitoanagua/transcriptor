namespace Transcriptor.DesignData;

/// <summary>
/// API de proceso para el tooling de datos de diseño: descarga verificada de
/// paquetes Spectrum 2 y generación de XAML. No lanza procesos externos ni
/// usa la red fuera de <see cref="EnsureCache" />.
/// </summary>
public static class DesignDataApi
{
    private static readonly string SolutionDir = FindSolutionDirectory();

    /// <summary>Ruta de salida versionada de Tokens.axaml.</summary>
    public static string TokensOutputPath => Path.Combine(SolutionDir, "ui", "Theming", "Tokens.axaml");

    /// <summary>Ruta de salida versionada de Icons.axaml.</summary>
    public static string IconsOutputPath => Path.Combine(SolutionDir, "ui", "Theming", "Icons.axaml");

    /// <summary>Descarga y verifica (o reutiliza) todos los paquetes pinados. Sin red si ya están en caché.</summary>
    public static void EnsureCache()
    {
        foreach (var pin in Pins.All) DesignDataCache.EnsurePackage(pin);
    }

    /// <summary>Genera el contenido de Tokens.axaml desde la caché verificada.</summary>
    /// <exception cref="DesignDataException">Si falta la caché (llamar a <see cref="EnsureCache" /> primero).</exception>
    public static string GenerateTokens()
    {
        var packageDir = DesignDataCache.EnsurePackage(Pins.SpectrumTokens);
        var variablesFile = Path.Combine(packageDir, "dist", "json", "variables.json");
        if (!File.Exists(variablesFile))
        {
            throw new DesignDataException(
                "Falta la caché de datos de diseño. Ejecute: dotnet run --project tools/Transcriptor.Tools -- fetch");
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(variablesFile));
        var variables = document.RootElement.EnumerateObject()
            .ToDictionary(prop => prop.Name, prop => prop.Value, StringComparer.Ordinal);
        return TokenGenerator.Generate(variables);
    }

    /// <summary>Genera el contenido de Icons.axaml desde la caché verificada.</summary>
    public static string GenerateIcons()
    {
        var workflowDir = DesignDataCache.EnsurePackage(Pins.SpectrumCssWorkflowIcons);
        var uiIconsDir = DesignDataCache.EnsurePackage(Pins.SpectrumCssUiIcons);
        return IconGenerator.Generate(workflowDir, uiIconsDir);
    }

    private static string FindSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Transcriptor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
