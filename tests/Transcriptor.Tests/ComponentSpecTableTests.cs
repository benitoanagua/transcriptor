using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// Paso 3: cada <c>Spectrum.*</c> de <c>ControlThemes.axaml</c> tiene sección en
/// <c>docs/especificacion/spectrum2-componentes.md</c>, y cada token de diseño
/// citado (entre comillas invertidas en la tabla) existe en <c>Tokens.axaml</c>.
/// Las palabras entre comillas invertidas que no son tokens de diseño
/// (nombres de clase CSS como `.spectrum-Button`, XAML como `Spectrum.Button`,
/// rutas, etc.) se excluyen por no coincidir con el patrón de token.
/// </summary>
public sealed class ComponentSpecTableTests
{
    private static readonly string SolutionDir = GetSolutionDirectory();

    // Palabras kebab-case entre comillas invertidas de la tabla que NO son tokens de diseño
    // de @adobe/spectrum-tokens (son clases CSS, estados, rutas o valores de CSS).
    private static readonly HashSet<string> NonDesignTokens = new(StringComparer.Ordinal)
    {
        "spectrum-two",
        "spectrum-css",
        "component-pill-edge-to-text-100",
        "outline-offset",
        "animation-duration-100",
        "border-width",
        "font-weight",
        "component-edge-to-text-100",
        "ui-spectrum",
        "is-checked",
        "is-selected",
    };

    [Fact]
    public void CadaTemaTieneSeccionYCadaTokenExiste()
    {
        var themesPath = Path.Combine(SolutionDir, "ui", "Theming", "ControlThemes.axaml");
        var tablePath = Path.Combine(SolutionDir, "docs", "especificacion", "spectrum2-componentes.md");
        var tokensPath = Path.Combine(SolutionDir, "ui", "Theming", "Tokens.axaml");

        Assert.True(File.Exists(themesPath), "Falta ControlThemes.axaml.");
        Assert.True(File.Exists(tablePath), "Falta spectrum2-componentes.md.");
        Assert.True(File.Exists(tokensPath), "Falta Tokens.axaml.");

        var table = File.ReadAllText(tablePath);

        // Cada tema Spectrum.* debe tener una sección de encabezado en la tabla.
        var themesDoc = XDocument.Load(themesPath);
        var themeKeys = themesDoc.Root!
            .Descendants()
            .Where(e => e.Name.LocalName == "ControlTheme")
            .Select(e => e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value)
            .Where(k => k is not null && k.StartsWith("Spectrum.", StringComparison.Ordinal))
            .Select(k => k![9..]) // quita el prefijo "Spectrum."
            .Distinct()
            .ToList();

        Assert.NotEmpty(themeKeys);

        var tableLines = table.Split('\n');
        foreach (var key in themeKeys)
        {
            var keyNorm = key.Replace(" ", "").ToLowerInvariant();
            Assert.True(tableLines.Any(l =>
            {
                var trimmed = l.TrimStart();
                return trimmed.StartsWith("## ", StringComparison.Ordinal)
                    && trimmed.Substring(3).Replace(" ", "").ToLowerInvariant().Contains(keyNorm, StringComparison.Ordinal);
            }),
                $"El tema Spectrum.{key} no tiene sección en la tabla de especificación.");
        }

        // Tokens de diseño citados entre comillas invertidas: patrón kebab-case en minúsculas.
        var citedTokens = Regex.Matches(table, @"`([a-z][a-z0-9]*(?:-[a-z0-9]+)+)`")
            .Select(m => m.Groups[1].Value)
            .Where(t => !NonDesignTokens.Contains(t))
            .Distinct();

        var tokensDoc = XDocument.Load(tokensPath);
        var definedTokens = tokensDoc.Root!
            .Descendants()
            .Select(e => e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value)
            .Where(k => k is not null)
            .ToHashSet(StringComparer.Ordinal);

        var missing = citedTokens.Where(t => !definedTokens.Contains(t)).ToList();
        Assert.True(missing.Count == 0,
            $"Tokens citados en la tabla pero no definidos en Tokens.axaml: {string.Join(", ", missing)}.");
    }

    private static string GetSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Transcriptor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
