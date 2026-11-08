using System.Security.Cryptography;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// Verifica que nadie sustituya las fuentes estáticas oficiales de
/// `ui/Assets/Fonts/` por otra versión sin notarlo. Los hashes SHA-256 abajo
/// corresponden a los archivos versiónados; la URL de origen de cada familia
/// queda documentada en `docs/THIRD-PARTY-NOTICES.md`.
/// </summary>
public sealed class FontIntegrityTests
{
    private static readonly string FontsDir =
        Path.Combine(GetSolutionDirectory(), "ui", "Assets", "Fonts");

    private static readonly Dictionary<string, string> ExpectedHashes = new(StringComparer.Ordinal)
    {
        ["SourceCodePro-Bold.ttf"] = "b2095e0d657e6d28dc32444a9dacabab0c9241d0bf39d96371756cc9bdbc3a5f",
        ["SourceCodePro-Medium.ttf"] = "f9dbc746f60c83f5cb1503b19f90729d8195cab6bb67f2b861d4eb257ed86536",
        ["SourceCodePro-Regular.ttf"] = "74bd80d3e42a08517cd7e1108ba3d86f2da29ac0f3065be95e0357956ab9db37",
        ["SourceSans3-Bold.ttf"] = "83a72786970a93a8c48eaead886c995676d512c0ee7164138f8ea76ea847d75c",
        ["SourceSans3-ExtraBold.ttf"] = "096020cb33598ea74c556c9446d6261c8993e5f742e9e96b7cd952457a9f1e2d",
        ["SourceSans3-Medium.ttf"] = "bec4ffe926595df756b5a1e39a9f704debfab88055d9a157f978eb500444dd28",
        ["SourceSans3-Regular.ttf"] = "20ab9752bd19a373dba627393b6c142e6cf8fc2f219119a8e0cb1442ef679ec5",
    };

    [Fact]
    public void FontFilesMatchExpectedSha256()
    {
        Assert.True(Directory.Exists(FontsDir), $"No existe la carpeta de fuentes: {FontsDir}");

        var versioned = Directory.GetFiles(FontsDir, "*.ttf");
        Assert.True(ExpectedHashes.Keys.Count == versioned.Length,
            "La carpeta de fuentes tiene más o menos archivos de los esperados.");

        foreach (var file in versioned)
        {
            var name = Path.GetFileName(file);
            Assert.True(ExpectedHashes.TryGetValue(name, out var expected),
                $"Fuente inesperada en ui/Assets/Fonts/: {name}");
            var actual = Sha256Hex(file);
            Assert.True(
                string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                $"La fuente {name} cambió su SHA-256 (posible sustitución de versión): {actual} != {expected}.");
        }
    }

    private static string Sha256Hex(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string GetSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Transcriptor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
