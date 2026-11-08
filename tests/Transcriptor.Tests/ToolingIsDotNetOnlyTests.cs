using System.Text.RegularExpressions;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// Todo lo que no es código de la aplicación (generadores, pruebas, verificaciones,
/// herramientas de captura, pasos de CI) se escribe en C# sobre .NET 10. Esta prueba
/// impide que vuelvan herramientas de otro ecosistema al repositorio versionado ni
/// que las pruebas o las herramientas lancen procesos de Python, Node o shells.
/// </summary>
public sealed class ToolingIsDotNetOnlyTests
{
    private static readonly string SolutionDir = GetSolutionDirectory();

    // Extensiones y manifiesto que no pueden versionarse como parte del tooling.
    private static readonly string[] ForbiddenExtensions = [".py", ".sh", ".ps1", ".js", ".mjs", ".ts"];
    private static readonly string[] ForbiddenFiles = ["package.json"];

    // Ejecutables que ninguna prueba ni herramienta de C# puede arrancar.
    private static readonly Regex ProcessLauncher = new(
        @"\bProcessStartInfo\s*\{[^}]*?\bFileName\s*=\s*[""'](python|python3|node|npm|npx|bash|sh|pwsh)[""']",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void RepositoryDoesNotVersionNonDotNetTooling()
    {
        var violations = new List<string>();
        foreach (var file in EnumerateSolutionFiles())
        {
            var name = Path.GetFileName(file);
            var extension = Path.GetExtension(file);
            if (ForbiddenExtensions.Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase))
                || ForbiddenFiles.Any(f => f.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add(Path.GetRelativePath(SolutionDir, file));
            }
        }

        if (violations.Count > 0)
        {
            Assert.Fail(
                "El repositorio versiona archivos de tooling de otro ecosistema: " +
                string.Join(", ", violations));
        }
    }

    [Fact]
    public void TestsAndToolsDoNotLaunchNonDotNetProcesses()
    {
        var violations = new List<string>();
        foreach (var root in new[] { "tests", "tools" })
        {
            var rootDir = Path.Combine(SolutionDir, root);
            if (!Directory.Exists(rootDir)) continue;
            foreach (var cs in Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(cs);
                if (ProcessLauncher.IsMatch(source))
                {
                    violations.Add(Path.GetRelativePath(SolutionDir, cs));
                }
            }
        }

        if (violations.Count > 0)
        {
            Assert.Fail(
                "Una prueba o herramienta lanza procesos fuera de .NET: " +
                string.Join(", ", violations));
        }
    }

    private static IEnumerable<string> EnumerateSolutionFiles()
    {
        var excluded = new[] { ".git", "bin", "obj", "artifacts" };
        foreach (var file in Directory.GetFiles(SolutionDir, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(SolutionDir, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Intersect(excluded, StringComparer.OrdinalIgnoreCase).Any()) continue;
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
