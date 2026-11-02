using NAudio.CoreAudioApi;

namespace Transcriptor.Rec;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var o = Options.Parse(args);
            if (o.Help) { Options.PrintHelp(); return 0; }

            using var enumerator = new MMDeviceEnumerator();
            if (o.ListDevices) { PrintDevices(enumerator); return 0; }

            var mic = Pick(enumerator, DataFlow.Capture, o.Mic);
            var sys = Pick(enumerator, DataFlow.Render, o.Sys);
            var dir = OutputDir(o);

            Console.WriteLine($"Micrófono (Yo):              {mic.FriendlyName}");
            Console.WriteLine($"Audio del sistema (Cliente): {sys.FriendlyName}");
            Console.WriteLine($"Carpeta:                     {dir}");
            Console.WriteLine("Antes de continuar, confirma que todas las personas involucradas han sido informadas y que tienes autorización para grabar.");
            Console.Write("¿Confirmas? (S/N): ");
            if (!string.Equals(Console.ReadLine()?.Trim(), "S", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Grabación cancelada.");
                return 2;
            }

            Directory.CreateDirectory(dir);
            RecordingAccessControl.RestrictSessionDirectory(dir);
            Console.WriteLine("Permisos NTFS aplicados. Grabando pistas cifradas; pulsa Enter para detener.\n");

            using var rec = new DualRecorder(mic, sys, dir);
            using var stop = new ManualResetEventSlim();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
            _ = Task.Run(() => { Console.ReadLine(); stop.Set(); });

            rec.Start();
            while (!stop.Wait(1000))
                Console.Write($"\r{rec.Elapsed:hh\\:mm\\:ss}  MIC {Bar(rec.MicLevel)}  SIS {Bar(rec.SysLevel)}   ");
            rec.Stop();

            Console.WriteLine($"\n\nGuardado en {dir}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static MMDevice Pick(MMDeviceEnumerator e, DataFlow flow, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return e.GetDefaultAudioEndpoint(flow, Role.Communications);

        var match = e.EnumerateAudioEndPoints(flow, DeviceState.Active)
            .FirstOrDefault(d => d.FriendlyName.Contains(name, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new InvalidOperationException($"No hay un dispositivo que contenga '{name}'. Usa --list.");
    }

    private static void PrintDevices(MMDeviceEnumerator e)
    {
        Console.WriteLine("Micrófonos:");
        foreach (var d in e.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)) Console.WriteLine($"  {d.FriendlyName}");
        Console.WriteLine("Salidas de audio (para capturar al cliente):");
        foreach (var d in e.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) Console.WriteLine($"  {d.FriendlyName}");
    }

    private static string OutputDir(Options o)
    {
        var root = o.Out
            ?? Environment.GetEnvironmentVariable("TRANSCRIPTOR_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Transcriptor", "Grabaciones");
        var now = DateTime.Now;
        var name = now.ToString("HHmmss") + (string.IsNullOrWhiteSpace(o.Ref) ? "" : "_" + Sanitize(o.Ref));
        return Path.Combine(root, now.ToString("yyyy-MM-dd"), name);
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
          --out   carpeta raíz (por defecto: %TRANSCRIPTOR_DIR% o %USERPROFILE%\Transcriptor\Grabaciones)
          --list  muestra los dispositivos disponibles
        """);
}
