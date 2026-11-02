using System.Net.Http;

namespace Transcriptor.Setup;

internal static class Program
{
    private const string ModelUri = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin";

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1 || args[0] is "-h" or "--help")
        {
            Console.WriteLine("""
                Transcriptor.Setup

                Uso:
                  dotnet run --project setup/Transcriptor.Setup.csproj -- "ruta\a\whisper-cli.exe"

                Descarga el modelo small y configura WHISPER_BIN / WHISPER_MODEL para el usuario actual.
                El ejecutable whisper-cli debe obtenerse de una distribución oficial de whisper.cpp.
                """);
            return args.Length == 0 || args[0] is "-h" or "--help" ? 0 : 2;
        }

        var whisperPath = Path.GetFullPath(args[0]);
        if (!File.Exists(whisperPath))
        {
            Console.Error.WriteLine($"No existe whisper-cli: {whisperPath}");
            return 1;
        }

        var modelDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Transcriptor", "models");
        var modelPath = Path.Combine(modelDirectory, "ggml-small.bin");
        var temporaryPath = modelPath + ".download";
        Directory.CreateDirectory(modelDirectory);

        if (!File.Exists(modelPath))
        {
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

                File.Move(temporaryPath, modelPath, overwrite: true);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        Environment.SetEnvironmentVariable("WHISPER_BIN", whisperPath, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("WHISPER_MODEL", modelPath, EnvironmentVariableTarget.User);

        Console.WriteLine($"Whisper: {whisperPath}");
        Console.WriteLine($"Modelo:  {modelPath}");
        Console.WriteLine("Configuración guardada para el usuario actual. Abre una nueva terminal para usarla.");
        return 0;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* El borrado normal del temporal es best effort. */ }
    }
}
