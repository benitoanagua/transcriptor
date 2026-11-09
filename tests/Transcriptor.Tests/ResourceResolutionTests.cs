using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// D1: ninguna vista puede referenciar un recurso estático o dinámico que no
/// esté definido como <c>x:Key</c> en algún diccionario del proyecto.
/// Recorre todos los *.axaml y falla ante claves no resueltas, para que un
/// cambio en Icons.axaml o Tokens.axaml no deje PathIcon vacíos en silencio.
/// </summary>
public sealed class ResourceResolutionTests
{
    private static readonly string SolutionDir = GetSolutionDirectory();

    // Claves que provee el propio framework de Avalonia/XAML y no se declaran
    // en nuestros diccionarios.
    private static readonly HashSet<string> FrameworkKeys = new(StringComparer.Ordinal)
    {
        "x:Null", "x:True", "x:False",
    };

    [Fact]
    public void EveryStaticAndDynamicResourceIsDefined()
    {
        var definedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dictionary in DictionaryFiles())
        {
            var document = XDocument.Load(dictionary);
            foreach (var element in document.Root!.Elements())
            {
                var key = element.Attribute("{http://schemas.microsoft.com/winfx/2006/xaml}Key")?.Value;
                if (!string.IsNullOrEmpty(key)) definedKeys.Add(key.Trim());
            }
        }

        var violations = new List<string>();
        var usage = new Regex(@"\{(?:StaticResource|DynamicResource)\s+([^}]+)\}",
            RegexOptions.Compiled);

        foreach (var xaml in XamlFiles())
        {
            var relative = Path.GetRelativePath(SolutionDir, xaml);
            var source = File.ReadAllText(xaml);
            foreach (Match match in usage.Matches(source))
            {
                var key = match.Groups[1].Value.Trim();
                // Ignorar usos con conversores u otros argumentos después de la clave.
                var plainKey = key.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? key;
                if (FrameworkKeys.Contains(plainKey) || definedKeys.Contains(plainKey)) continue;
                violations.Add($"{relative}: recurso no definido '{plainKey}' ({match.Value}).");
            }
        }

        if (violations.Count > 0)
        {
            Assert.Fail("Recursos estáticos/dinámicos sin definición:\n" + string.Join("\n", violations));
        }
    }

    /// <summary>
    /// D2: los tokens viven en <c>SpectrumTheme.axaml</c> (fusionado una sola
    /// vez en <c>Application.Resources</c> de cada cabeza de escritorio), de
    /// modo que cualquier ventana o diálogo creado fuera del árbol de
    /// <c>MainWindow</c> resuelve los recursos sin depender de
    /// <c>MainWindow.Resources</c>.
    ///
    /// Esta prueba es estática (sin <see cref="HeadlessUnitTestSession"/>)
    /// para evitar la contaminación del singleton <see cref="Application"/>
    /// entre pruebas en el host headless de Linux (mismo motivo por el que
    /// <see cref="StudioGeometryTests"/> queda excluida en este entorno).
    /// </summary>
    [Fact]
    public void SpectrumThemeEsElUnicoPuntoDeRecursos()
    {
        var solutionDir = GetSolutionDirectory();
        var spectrumTheme = Path.Combine(solutionDir, "ui", "Theming", "SpectrumTheme.axaml");
        var mainWindow = Path.Combine(solutionDir, "ui", "MainWindow.axaml");
        var gallery = Path.Combine(solutionDir, "desktop-dev", "SpectrumGalleryWindow.axaml");

        Assert.True(File.Exists(spectrumTheme), "Falta SpectrumTheme.axaml.");
        Assert.True(File.Exists(mainWindow), "Falta MainWindow.axaml.");

        var themeDoc = XDocument.Load(spectrumTheme);
        var includes = themeDoc.Root!.DescendantsAndSelf()
            .Where(e => e.Name.LocalName == "ResourceInclude")
            .Select(e => e.Attribute("Source")?.Value)
            .Where(v => v is not null)
            .ToList()!;

        // SpectrumTheme debe fusionar exactamente Tokens, Icons y ControlThemes.
        Assert.True(includes.Contains("avares://Transcriptor.UI/Theming/Tokens.axaml"), "SpectrumTheme no fusiona Tokens.axaml.");
        Assert.True(includes.Contains("avares://Transcriptor.UI/Theming/Icons.axaml"), "SpectrumTheme no fusiona Icons.axaml.");
        Assert.True(includes.Contains("avares://Transcriptor.UI/Theming/ControlThemes.axaml"), "SpectrumTheme no fusiona ControlThemes.axaml.");
        Assert.True(includes.Count == 3, "SpectrumTheme no debe fusionar más diccionarios de los tres esperados.");

        // MainWindow y SpectrumGalleryWindow ya no deben incluir Tokens/Icons/ControlThemes por separado.
        foreach (var xaml in new[] { mainWindow, gallery })
        {
            if (!File.Exists(xaml)) continue;
            var doc = XDocument.Load(xaml);
            var xamlIncludes = (doc.Root?.DescendantsAndSelf()
                .Where(e => e.Name.LocalName == "ResourceInclude")
                ?? Enumerable.Empty<XElement>())
                .Select(e => e.Attribute("Source")?.Value ?? string.Empty)
                .ToList();
            Assert.False(xamlIncludes.Contains("avares://Transcriptor.UI/Theming/Tokens.axaml"), $"{xaml} ya no debe incluir Tokens directamente.");
            Assert.False(xamlIncludes.Contains("avares://Transcriptor.UI/Theming/Icons.axaml"), $"{xaml} ya no debe incluir Icons directamente.");
            Assert.False(xamlIncludes.Contains("avares://Transcriptor.UI/Theming/ControlThemes.axaml"), $"{xaml} ya no debe incluir ControlThemes directamente.");
            Assert.False(xamlIncludes.Any(i => i.Contains("avares://Transcriptor.UI.Spectrum/")), $"{xaml} ya no debe referenciar el ensamblado ui-spectrum eliminado.");
        }
    }

    private static IEnumerable<string> DictionaryFiles()
    {
        foreach (var root in new[] { Path.Combine("ui", "Theming") })
        {
            var dir = Path.Combine(SolutionDir, root);
            if (Directory.Exists(dir))
                foreach (var file in Directory.GetFiles(dir, "*.axaml", SearchOption.AllDirectories))
                    yield return file;
        }
    }

    private static IEnumerable<string> XamlFiles()
    {
        foreach (var root in new[] { "ui", "desktop-dev", "desktop-avalonia" })
        {
            var dir = Path.Combine(SolutionDir, root);
            if (Directory.Exists(dir))
                foreach (var file in Directory.GetFiles(dir, "*.axaml", SearchOption.AllDirectories))
                    yield return file;
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
