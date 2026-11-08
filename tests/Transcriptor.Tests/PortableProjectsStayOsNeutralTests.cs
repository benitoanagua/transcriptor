using System.Xml.Linq;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// Las capas portables (ui, transcription y desktop-dev) no deben apuntar a
/// Windows ni referencia al adaptador de plataforma de Windows, y la cabeza de
/// producción (desktop-avalonia) no debe referenciar la cabeza de desarrollo
/// (desktop-dev). Los fakes y protecciones de desarrollo no llegan a producción.
/// </summary>
public sealed class PortableProjectsStayOsNeutralTests
{
    private static readonly string SolutionDir = GetSolutionDirectory();

    [Fact]
    public void PortableProjectsDoNotTargetWindowsNorReferencePlatformWindows()
    {
        var violations = new List<string>();

        foreach (var project in new[]
        {
            "ui/Transcriptor.UI.csproj",
            "transcription/Transcriptor.Transcription.csproj",
            "desktop-dev/Transcriptor.Dev.csproj",
        })
        {
            var document = XDocument.Load(Path.Combine(SolutionDir, project));
            var ns = document.Root!.GetDefaultNamespace();

            foreach (var tfm in document.Root.Descendants(ns + "TargetFramework"))
            {
                if (tfm.Value.Contains("-windows", StringComparison.Ordinal))
                {
                    violations.Add($"{project}: TargetFramework '{tfm.Value}' apunta a Windows.");
                }
            }

            foreach (var reference in document.Root.Descendants(ns + "ProjectReference"))
            {
                var include = reference.Attribute("Include")?.Value ?? string.Empty;
                if (include.Contains("platform-windows", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{project}: referencia al adaptador de Windows '{include}'.");
                }
            }
        }

        if (violations.Count > 0)
        {
            Assert.Fail(string.Join(Environment.NewLine, violations));
        }
    }

    [Fact]
    public void DesktopAvaloniaDoesNotReferenceDesktopDev()
    {
        var project = "desktop-avalonia/Transcriptor.Desktop.csproj";
        var document = XDocument.Load(Path.Combine(SolutionDir, project));
        var ns = document.Root!.GetDefaultNamespace();

        var violations = new List<string>();
        foreach (var reference in document.Root.Descendants(ns + "ProjectReference"))
        {
            var include = reference.Attribute("Include")?.Value ?? string.Empty;
            if (include.Contains("desktop-dev", StringComparison.OrdinalIgnoreCase)
                || include.Contains("Transcriptor.Dev", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"{project}: no debe referenciar la cabeza de desarrollo ('{include}').");
            }
        }

        Assert.Empty(violations);
    }

    private static string GetSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Transcriptor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
