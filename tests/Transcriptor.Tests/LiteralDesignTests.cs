using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// RNUI-05: ninguna vista ni code-behind de vista puede contener valores de
/// diseño literales (colores hex, tamaños, radios, espaciados, tipografías).
/// Todo debe consumir tokens v\u00eda <see>DynamicResource</see>.
/// </summary>
public sealed class LiteralDesignTests
{
    private static readonly string[] ViewRoots =
    [
        Path.Combine("ui"),
        Path.Combine("desktop-dev"),
        Path.Combine("desktop-avalonia"),
    ];

    private static readonly Regex HexColor = new(@"#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Atributos que nunca deben llevar un n\u00famero crudo en una vista.
    private static readonly Regex DesignLiteral = new(
        @"\b(Width|Height|FontSize|Margin|Padding|CornerRadius|Spacing)\=\""([0-9]+(?:\.[0-9]+)?)\""",
        RegexOptions.Compiled);

    [Fact]
    public void NoLiteralDesignValuesInViews()
    {
        var violations = new List<string>();
        var solutionDir = GetSolutionDirectory();

        foreach (var root in ViewRoots)
        {
            var rootDir = Path.Combine(solutionDir, root);
            if (!Directory.Exists(rootDir))
                continue;
            foreach (var xaml in Directory.GetFiles(rootDir, "*.axaml", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(solutionDir, xaml);
                var document = XDocument.Load(xaml);
                Walk(document.Root!, violations, relative, isRootWindow: true);
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void NoLiteralWindowConstructionInViewCodeBehind()
    {
        var violations = new List<string>();
        var solutionDir = GetSolutionDirectory();

        foreach (var root in ViewRoots)
        {
            var rootDir = Path.Combine(solutionDir, root);
            if (!Directory.Exists(rootDir))
                continue;
            foreach (var cs in Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(solutionDir, cs);
                var source = File.ReadAllText(cs);
                if (Regex.IsMatch(source, @"\bnew\s+Button\s*\("))
                    violations.Add($"{relative}: 'new Button(...)' en code-behind de vista.");
                if (Regex.IsMatch(source, @"\bnew\s+Window\s*\("))
                    violations.Add($"{relative}: 'new Window(...)' en code-behind de vista.");
            }
        }

        Assert.Empty(violations);
    }

    private static void Walk(XElement element, List<string> violations, string relative, bool isRootWindow)
    {
        foreach (var attr in element.Attributes())
        {
            var name = attr.Name.LocalName;
            var value = attr.Value;

            // Colores hex solo permitidos en el diccionario de tokens.
            if (HexColor.IsMatch(value)
                && !relative.EndsWith("Theming/Tokens.axaml", System.StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"{relative}: color hex literal '{value}' en '{attr.Name}'.");
            }

            // N\u00fameros crudos en atributos de dise\u00f1o.
            var designMatch = DesignLiteral.Match(value);
            if (designMatch.Success)
            {
                // El ancho/alto de la ventana ra\u00edz es tama\u00f1o de ventana, no de componente.
                var isRootWindowSize = isRootWindow && name is "Width" or "Height";
                if (!isRootWindowSize)
                {
                    violations.Add(
                        $"{relative}: literal de dise\u00f1o '{name}={value}' en '{element.Name.LocalName}'.");
                }
            }
        }

        foreach (var child in element.Elements())
            Walk(child, violations, relative, isRootWindow: false);
    }

    private static string GetSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Transcriptor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
