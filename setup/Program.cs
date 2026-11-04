using System.Net.Http;
using System.Security.Cryptography;

namespace Transcriptor.Setup;

internal static class Program
{
    private const string ModelUri = "https://huggingface.co/ggerganov/whisper.cpp/resolve/90a64d80ea254cf67575b41a5971f972c79f7b45/ggml-small.bin";
    private const string ModelSha256 = "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b";
    private const string DesktopExecutable = "Transcriptor.Desktop.exe";
    private const string PortableMarker = "portable.flag";

    private const string HelpText = """
        Transcriptor.Setup

        Uso:
          transcriptor-setup "ruta\a\whisper-cli.exe"
              Instalación normal: descarga el modelo small a %USERPROFILE%\Transcriptor\models y
              configura WHISPER_BIN / WHISPER_MODEL para el usuario actual.

          transcriptor-setup --portable "ruta\a\whisper-cli.exe" [--target "carpeta"]
              Genera una versión portable: copia whisper-cli.exe y sus DLL a <carpeta>\whisper,
              descarga el modelo a <carpeta>\models y crea portable.flag. No modifica variables
              de entorno ni %LOCALAPPDATA%; configuración y grabaciones quedan en <carpeta>\data.
              Si se omite --target se usa la carpeta que contiene Transcriptor.Desktop.exe.

        El ejecutable whisper-cli debe obtenerse de una distribución oficial de whisper.cpp.
        """;

    private sealed record Options(bool Portable, string? WhisperPath, string? Target, bool Help);

    private static async Task<int> Main(string[] args)
    {
        if (!TryParse(args, out var options, out var parseError))
        {
            Console.Error.WriteLine(parseError);
            Console.Error.WriteLine();
            Console.WriteLine(HelpText);
            return 2;
        }

        if (options.Help || options.WhisperPath is null)
        {
            Console.WriteLine(HelpText);
            return options.Help || args.Length == 0 ? 0 : 2;
        }

        var whisperPath = Path.GetFullPath(options.WhisperPath);
        if (!File.Exists(whisperPath))
        {
            Console.Error.WriteLine($"No existe whisper-cli: {whisperPath}");
            return 1;
        }

        try
        {
            return options.Portable
                ? await SetupPortableAsync(whisperPath, options.Target)
                : await SetupInstalledAsync(whisperPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or HttpRequestException
                                              or UnauthorizedAccessException or TaskCanceledException)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
    }

    private static bool TryParse(string[] args, out Options options, out string? error)
    {
        bool portable = false, help = false;
        string? whisper = null, target = null;
        options = new Options(false, null, null, false);
        error = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    help = true;
                    break;
                case "--portable":
                    portable = true;
                    break;
                case "--target":
                    if (++i >= args.Length) { error = "Falta el valor de --target."; return false; }
                    target = args[i];
                    break;
                default:
                    if (args[i].StartsWith('-')) { error = $"Argumento desconocido: {args[i]}"; return false; }
                    if (whisper is not null) { error = "Solo se admite una ruta de whisper-cli."; return false; }
                    whisper = args[i];
                    break;
            }
        }

        if (target is not null && !portable) { error = "--target solo se puede usar con --portable."; return false; }
        options = new Options(portable, whisper, target, help);
        return true;
    }

    private static async Task<int> SetupInstalledAsync(string whisperPath)
    {
        var modelPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Transcriptor", "models", "ggml-small.bin");
        await EnsureModelAsync(modelPath);

        Environment.SetEnvironmentVariable("WHISPER_BIN", whisperPath, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("WHISPER_MODEL", modelPath, EnvironmentVariableTarget.User);

        Console.WriteLine($"Whisper: {whisperPath}");
        Console.WriteLine($"Modelo:  {modelPath}");
        Console.WriteLine("Configuración guardada para el usuario actual. Abre una nueva terminal para usarla.");
        return 0;
    }

    private static async Task<int> SetupPortableAsync(string whisperPath, string? targetOption)
    {
        var target = ResolvePortableTarget(targetOption);
        if (target is null)
        {
            Console.Error.WriteLine($"No se encontró {DesktopExecutable}. Ejecuta el configurador desde la carpeta de la aplicación o indica --target.");
            return 1;
        }

        Console.WriteLine($"Carpeta portable: {target}");
        WarnIfNotNtfs(target);

        var whisperTarget = Path.Combine(target, "whisper");
        CopyWhisper(whisperPath, whisperTarget);
        var modelPath = Path.Combine(target, "models", "ggml-small.bin");
        await EnsureModelAsync(modelPath);

        Directory.CreateDirectory(Path.Combine(target, "data"));
        // El marcador se escribe al final: si algo falla antes, la aplicación sigue en modo normal.
        File.WriteAllText(Path.Combine(target, PortableMarker),
            "Transcriptor en modo portable: configuración, Whisper, modelo y grabaciones viven en esta carpeta." + Environment.NewLine);

        Console.WriteLine($"Whisper: {Path.Combine(whisperTarget, "whisper-cli.exe")}");
        Console.WriteLine($"Modelo:  {modelPath}");
        Console.WriteLine("Versión portable lista. No se modificaron variables de entorno ni %LOCALAPPDATA%.");
        Console.WriteLine("Aviso: las pistas cifradas usan DPAPI del usuario actual; no se podrán abrir desde otro usuario o equipo.");
        return 0;
    }

    private static string? ResolvePortableTarget(string? targetOption)
    {
        if (targetOption is not null)
        {
            var explicitTarget = Path.GetFullPath(targetOption);
            return File.Exists(Path.Combine(explicitTarget, DesktopExecutable)) ? explicitTarget : null;
        }

        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..")), // <app>\tools\setup\
            Path.GetFullPath(baseDirectory)
        };
        return candidates.FirstOrDefault(candidate => File.Exists(Path.Combine(candidate, DesktopExecutable)));
    }

    private static void WarnIfNotNtfs(string target)
    {
        try
        {
            var format = new DriveInfo(Path.GetPathRoot(target)!).DriveFormat;
            if (!string.Equals(format, "NTFS", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"Aviso: la unidad es {format}, no NTFS. Transcriptor aplica ACL NTFS a cada sesión y no podrá grabar aquí; elige otra carpeta de grabaciones en Ajustes.");
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.WriteLine("Aviso: no se pudo comprobar el sistema de archivos de la unidad de destino.");
        }
    }

    private static void CopyWhisper(string whisperPath, string destination)
    {
        var sourceDirectory = Path.GetDirectoryName(whisperPath)!;
        Directory.CreateDirectory(destination);
        if (string.Equals(Path.GetFullPath(sourceDirectory).TrimEnd('\\'), Path.GetFullPath(destination).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase))
            return;

        var files = Directory.EnumerateFiles(sourceDirectory, "*.dll").Append(whisperPath);
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            // Se normaliza el nombre del ejecutable para que la ruta por defecto sea siempre la misma.
            if (string.Equals(file, whisperPath, StringComparison.OrdinalIgnoreCase)) name = "whisper-cli.exe";
            File.Copy(file, Path.Combine(destination, name), overwrite: true);
        }
        Console.WriteLine($"Whisper copiado a {destination}");
    }

    private static async Task EnsureModelAsync(string modelPath)
    {
        var modelDirectory = Path.GetDirectoryName(modelPath)!;
        var temporaryPath = modelPath + ".download";
        Directory.CreateDirectory(modelDirectory);

        if (File.Exists(modelPath) && !await VerifyModelAsync(modelPath))
        {
            Console.WriteLine("El modelo existente no coincide con la huella SHA-256 oficial; se descargará de nuevo.");
            File.Delete(modelPath);
        }

        if (File.Exists(modelPath)) return;

        Console.WriteLine("Descargando el modelo Whisper small...");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromHours(2) };
            using var response = await client.GetAsync(ModelUri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var expectedLength = response.Content.Headers.ContentLength;

            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
            {
                await input.CopyToAsync(output);
                await output.FlushAsync();
                output.Flush(flushToDisk: true);
            }

            var actualLength = new FileInfo(temporaryPath).Length;
            if (actualLength == 0 || (expectedLength.HasValue && actualLength != expectedLength.Value))
                throw new InvalidDataException("La descarga del modelo está incompleta; se conservará la configuración anterior.");
            if (!await VerifyModelAsync(temporaryPath))
                throw new InvalidDataException("La descarga del modelo no coincide con la huella SHA-256 oficial; no se instalará.");

            File.Move(temporaryPath, modelPath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static async Task<bool> VerifyModelAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = await SHA256.HashDataAsync(stream);
        var expectedHash = Convert.FromHexString(ModelSha256);
        try { return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash); }
        finally
        {
            CryptographicOperations.ZeroMemory(actualHash);
            CryptographicOperations.ZeroMemory(expectedHash);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* El borrado normal del temporal es best effort. */ }
    }
}
