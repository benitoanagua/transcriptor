using System.Net.Http;
using System.Security.Cryptography;

namespace Transcriptor.Setup;

internal static class Program
{
    private const string ModelUri = "https://huggingface.co/ggerganov/whisper.cpp/resolve/90a64d80ea254cf67575b41a5971f972c79f7b45/ggml-small.bin";
    private const string ModelSha256 = "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b";

    private const string HelpText = """
        Transcriptor.Setup

        Uso:
          transcriptor-setup "ruta\a\whisper-cli.exe"
              Descarga el modelo small a %USERPROFILE%\Transcriptor\models y configura
              WHISPER_BIN / WHISPER_MODEL para el usuario actual.

        El ejecutable whisper-cli debe obtenerse de una distribución oficial de whisper.cpp.
        """;

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && (args[0] is "-h" or "--help")))
        {
            Console.WriteLine(HelpText);
            return 0;
        }

        if (args.Length != 1 || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine("Se esperaba una única ruta a whisper-cli.exe.");
            Console.WriteLine(HelpText);
            return 2;
        }

        var whisperPath = Path.GetFullPath(args[0]);
        if (!File.Exists(whisperPath))
        {
            Console.Error.WriteLine($"No existe whisper-cli: {whisperPath}");
            return 1;
        }

        try
        {
            return await SetupInstalledAsync(whisperPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or HttpRequestException
                                              or UnauthorizedAccessException or TaskCanceledException)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
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
