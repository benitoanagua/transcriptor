using System.Globalization;
using Transcriptor.Platform.Windows;
using Transcriptor.Transcription;

namespace Transcriptor.Rec;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var o = Options.Parse(args);
            if (o.Help) { Options.PrintHelp(); return 0; }

            var catalog = new WasapiDeviceCatalog();
            if (o.ListDevices) { PrintDevices(catalog); return 0; }

            var mic = Pick(catalog, o.Mic, catalog.GetDefaultCaptureDeviceId());
            var sys = Pick(catalog, o.Sys, catalog.GetDefaultOutputDeviceId());
            var dir = OutputDir(o);

            Console.WriteLine($"Micrófono (Yo):              {mic.Name}");
            Console.WriteLine($"Audio del sistema (Cliente): {sys.Name}");
            Console.WriteLine($"Carpeta:                     {dir}");
            Console.WriteLine("Antes de continuar, confirma que todas las personas involucradas han sido informadas y que tienes autorización para grabar.");
            Console.Write("¿Confirmas? (S/N): ");
            if (!string.Equals(Console.ReadLine()?.Trim(), "S", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Grabación cancelada.");
                return 2;
            }

            Directory.CreateDirectory(dir);
            new WindowsSessionDirectorySecurity().RestrictSessionDirectory(dir);
            Console.WriteLine("Permisos NTFS aplicados. Grabando pistas cifradas; pulsa Enter para detener.\n");

            using var capture = new WasapiCaptureFactory().Create(dir, mic.Id, sys.Id);
            using var stop = new ManualResetEventSlim();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
            _ = Task.Run(() => { Console.ReadLine(); stop.Set(); });

            capture.Start();
            while (!stop.Wait(1000))
                Console.Write($"\r{capture.Elapsed:hh\\:mm\\:ss}  MIC {Bar(capture.MicrophonePeak)}  SIS {Bar(capture.SystemPeak)}   ");
            capture.Stop();

            Console.WriteLine($"\n\nGuardado en {dir}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static AudioDeviceOption Pick(IDeviceCatalog catalog, string? name, string? defaultId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            var all = catalog.GetCaptureDevices()
                .Concat(catalog.GetOutputDevices())
                .FirstOrDefault(d => d.Id == defaultId);
            return all ?? throw new InvalidOperationException("El dispositivo predeterminado no está disponible.");
        }

        var match = catalog.GetCaptureDevices()
            .Concat(catalog.GetOutputDevices())
            .FirstOrDefault(d => d.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new InvalidOperationException($"No hay un dispositivo que contenga '{name}'. Usa --list.");
    }

    private static void PrintDevices(IDeviceCatalog catalog)
    {
        Console.WriteLine("Micrófonos:");
        foreach (var d in catalog.GetCaptureDevices()) Console.WriteLine($"  {d.Name}");
        Console.WriteLine("Salidas de audio (para capturar al cliente):");
        foreach (var d in catalog.GetOutputDevices()) Console.WriteLine($"  {d.Name}");
    }

    private static string OutputDir(Options o)
    {
        var root = o.Out
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Transcriptor", "Grabaciones");
        var now = DateTime.Now;
        var name = now.ToString("HHmmss", CultureInfo.InvariantCulture) + (string.IsNullOrWhiteSpace(o.Ref) ? "" : "_" + Sanitize(o.Ref));
        return Path.Combine(root, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), name);
    }

    private static string Sanitize(string s)
    {
        var bad = Path.GetInvalidFileNameChars().Append(' ').ToArray();
        return string.Join("-", s.Trim().Split(bad, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Bar(float level)
    {
        int n = (int)Math.Round(Math.Clamp(level, 0f, 1f) * 10);
        return new string('#', n).PadRight(10, '.');
    }
}

internal sealed record Options(string? Ref, string? Out, string? Mic, string? Sys, bool ListDevices, bool Help)
{
    public static Options Parse(string[] args)
    {
        string? @ref = null, outDir = null, mic = null, sys = null;
        bool list = false, help = false;
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : throw new ArgumentException($"Falta el valor de {args[i - 1]}.");
            switch (args[i])
            {
                case "--ref": @ref = Next(); break;
                case "--out": outDir = Next(); break;
                case "--mic": mic = Next(); break;
                case "--sys": sys = Next(); break;
                case "--list": list = true; break;
                case "-h": case "--help": help = true; break;
                default: throw new ArgumentException($"Argumento desconocido: {args[i]}");
            }
        }
        return new Options(@ref, outDir, mic, sys, list, help);
    }

    public static void PrintHelp() => Console.WriteLine("""
        rec – graba micrófono (yo.trn) y audio del sistema (cliente.trn)

        Uso: rec [--ref "nombre"] [--mic texto] [--sys texto] [--out carpeta] [--list]

          --ref   referencia de la llamada (se agrega al nombre de la carpeta)
          --mic   parte del nombre del micrófono (por defecto: el de comunicaciones)
          --sys   parte del nombre de la salida de audio a capturar (por defecto: la de comunicaciones)
          --out   carpeta raíz (por defecto: %USERPROFILE%\Transcriptor\Grabaciones)
          --list  muestra los dispositivos disponibles
        """);
}
