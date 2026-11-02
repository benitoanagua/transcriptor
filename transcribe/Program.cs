namespace Transcriptor.Transcription;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var root = Environment.GetEnvironmentVariable("TRANSCRIPTOR_DIR")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Transcriptor", "Grabaciones");
            var model = Environment.GetEnvironmentVariable("WHISPER_MODEL")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Transcriptor", "models", "ggml-small.bin");
            var executable = Environment.GetEnvironmentVariable("WHISPER_BIN")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Transcriptor", "whisper", "whisper-cli.exe");
            var language = Environment.GetEnvironmentVariable("WHISPER_LANG") ?? "es";

            if (args.Length > 0)
                root = Path.GetFullPath(args[0]);

            var engine = new TranscriptionEngine(executable, model, language);
            await engine.ProcessPendingCallsAsync(root);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Transcriptor.Transcription

            Uso:
              dotnet run --project transcribe/Transcriptor.Transcription.csproj [carpeta-grabaciones]

            Configuración por variables de entorno:
              TRANSCRIPTOR_DIR  Carpeta raíz de las grabaciones
              WHISPER_BIN       Ruta completa a whisper-cli.exe
              WHISPER_MODEL     Ruta al modelo ggml
              WHISPER_LANG      Idioma (por defecto: es)

            Solo procesa llamadas con yo.wav y cliente.wav que aún no tengan transcripcion.txt.
            """);
    }
}
