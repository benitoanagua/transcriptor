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

    private static IEnumerable<string> DictionaryFiles()
    {
        foreach (var root in new[] { Path.Combine("ui", "Theming"), Path.Combine("ui-spectrum", "Theming") })
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
