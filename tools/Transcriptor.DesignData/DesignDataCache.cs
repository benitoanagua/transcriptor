namespace Transcriptor.DesignData;

/// <summary>
/// Caché local de paquetes de datos de diseño fijados por hash.
///
/// Cada paquete se descarga una vez desde el registro de npm, se verifica su
/// SHA-512 en base64 contra el pin fijado y se extrae en
/// `artifacts/design-data/<paquete>@<versión>/package/`. Una entrada verificada
/// se reutiliza sin red (idempotente).
/// </summary>
public static class DesignDataCache
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Transcriptor-DesignData-Fetcher" }
        }
    };

    private static readonly string SolutionDirectory = FindSolutionDirectory();

    public static string CacheRoot { get; } =
        Path.GetFullPath(Path.Combine(SolutionDirectory, "artifacts", "design-data"));

    /// <summary>Devuelve la carpeta extraída de un pin (la que contiene package.json),
    /// descargándola y verificándola si falta.</summary>
    public static string EnsurePackage(SpectrumPin pin)
    {
        var packageDir = Path.Combine(CacheRoot, pin.CacheDirectoryName, "package");
        var metadataFile = Path.Combine(packageDir, "package.json");

        if (Directory.Exists(packageDir) && File.Exists(metadataFile) && PackageMetadataMatches(metadataFile, pin))
        {
            return packageDir;
        }

        var archive = DownloadArchive(pin);
        VerifySha512(archive, pin);
        ExtractTarGz(archive, Path.Combine(CacheRoot, pin.CacheDirectoryName));

        if (!PackageMetadataMatches(metadataFile, pin))
        {
            throw new DesignDataException(
                $"El paquete {pin.Name}@{pin.Version} no contiene el package.json esperado tras extraerlo.");
        }

        return packageDir;
    }

    private static bool PackageMetadataMatches(string metadataFile, SpectrumPin pin)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(metadataFile));
            var root = document.RootElement;
            return root.TryGetProperty("name", out var name) && name.GetString() == pin.Name
                && root.TryGetProperty("version", out var version) && version.GetString() == pin.Version;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] DownloadArchive(SpectrumPin pin)
    {
        using var response = HttpClient.GetAsync(pin.Url).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
    }

    private static void VerifySha512(byte[] archive, SpectrumPin pin)
    {
        var hash = SHA512.HashData(archive);
        var actual = Convert.ToBase64String(hash);
        if (!string.Equals(actual, pin.Sha512Base64, StringComparison.Ordinal))
        {
            throw new DesignDataException(
                $"El SHA-512 de {pin.Name}@{pin.Version} no coincide: {actual} != {pin.Sha512Base64}.");
        }
    }

    private static void ExtractTarGz(byte[] archive, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        using var archiveStream = new MemoryStream(archive);
        using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream);
        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            if (entry.EntryType != TarEntryType.RegularFile) continue;
            if (entry.Name is "package" or "./package" or "") continue;
            var targetPath = Path.GetFullPath(Path.Combine(destinationDir, entry.Name));
            if (!targetPath.StartsWith(destinationDir, StringComparison.Ordinal))
            {
                throw new DesignDataException($"Ruta no segura dentro del paquete: {entry.Name}.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using var destination = File.Create(targetPath);
            entry.DataStream!.CopyTo(destination);
        }
    }

    /// <summary>Descarga y verifica un archivo CSS de adobe/spectrum-css.</summary>
    public static string EnsureSpectrumCssFile(SpectrumCssFile file)
    {
        var relativeDir = Path.GetDirectoryName(file.RelativePath) ?? string.Empty;
        var targetDir = Path.GetFullPath(Path.Combine(CacheRoot, SpectrumCssPins.CacheDirectoryName, relativeDir));
        var targetPath = Path.Combine(targetDir, Path.GetFileName(file.RelativePath));

        if (File.Exists(targetPath) && FileSha256Matches(targetPath, file.Sha256))
            return targetPath;

        var url = SpectrumCssPins.BaseUrl + file.RelativePath;
        using var response = HttpClient.GetAsync(url).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        var data = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        VerifySha256(data, file);
        Directory.CreateDirectory(targetDir);
        File.WriteAllBytes(targetPath, data);
        return targetPath;
    }

    private static bool FileSha256Matches(string path, string expected)
    {
        try
        {
            var sha = SHA256.HashData(File.ReadAllBytes(path));
            var actual = Convert.ToHexString(sha).ToLowerInvariant();
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void VerifySha256(byte[] data, SpectrumCssFile file)
    {
        var hash = SHA256.HashData(data);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new DesignDataException(
                $"El SHA-256 de {file.Component}/{file.RelativePath} no coincide: {actual} != {file.Sha256}.");
        }
    }

    private static string FindSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Transcriptor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}

public sealed class DesignDataException(string message) : Exception(message);
