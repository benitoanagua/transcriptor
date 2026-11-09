namespace Transcriptor.Tools;

internal static class Program
{
    private static readonly string SolutionDir = FindSolutionDirectory();

    private static int Main(string[] args)
    {
        if (args.Length is 0)
        {
            WriteError("Falta el subcomando. Uso: dotnet run --project tools/Transcriptor.Tools -- <fetch|tokens|icons|inventory|package|smoke> [argumentos]");
            return 1;
        }

        try
        {
            return args[0] switch
            {
                "fetch" => Fetch(),
                "tokens" => Tokens(),
                "icons" => Icons(),
                "inventory" => Inventory(),
                "package" => Package(),
                "smoke" => Smoke(args.Skip(1).ToArray()),
                _ => UnknownCommand(args[0])
            };
        }
        catch (Exception ex)
        {
            WriteError(ex.Message);
            return 1;
        }
    }

    private static int Fetch()
    {
        DesignData.DesignDataApi.EnsureCache();
        foreach (var pin in DesignData.Pins.All)
        {
            Console.WriteLine($"[fetch] {pin.Name}@{pin.Version}: caché verificada en {DesignData.DesignDataCache.CacheRoot}.");
        }
        DesignData.DesignDataApi.EnsureSpectrumCssCache();
        Console.WriteLine(
            $"[fetch] spectrum-css@{DesignData.SpectrumCssPins.Commit}: " +
            $"{DesignData.SpectrumCssPins.Files.Count} archivos CSS verificados en {DesignData.DesignDataCache.CacheRoot}.");
        return 0;
    }

    private static int Tokens()
    {
        var output = DesignData.DesignDataApi.TokensOutputPath;
        File.WriteAllText(output, DesignData.DesignDataApi.GenerateTokens());
        Console.WriteLine($"[tokens] Generado {output}.");
        return 0;
    }

    private static int Icons()
    {
        var output = DesignData.DesignDataApi.IconsOutputPath;
        File.WriteAllText(output, DesignData.DesignDataApi.GenerateIcons());
        Console.WriteLine($"[icons] Generado {output}.");
        return 0;
    }

    private static int Inventory()
    {
        var inventoryDir = Path.Combine(SolutionDir, "artifacts", "package", "dependency-inventory");
        Directory.CreateDirectory(inventoryDir);

        var projects = new[]
        {
            (ProjectPath: "desktop-avalonia/Transcriptor.Desktop.csproj", Name: "desktop"),
            (ProjectPath: "rec/Rec.csproj", Name: "recorder"),
            (ProjectPath: "transcription/Transcriptor.Transcription.csproj", Name: "transcription"),
        };

        foreach (var (projectPath, name) in projects)
        {
            var fullProjectPath = Path.GetFullPath(Path.Combine(SolutionDir, projectPath));
            var json = RunDotNet("list", fullProjectPath, "package", "--include-transitive", "--format", "json");
            ValidateJson(json, name);
            File.WriteAllText(Path.Combine(inventoryDir, $"{name}.packages.json"), json);
            Console.WriteLine($"[inventory] {name} → {Path.Combine(inventoryDir, $"{name}.packages.json")}.");
        }

        var files = Directory.GetFiles(inventoryDir, "*.packages.json");
        if (files.Length != projects.Length)
        {
            WriteError($"El inventario de dependencias está incompleto: {files.Length} de {projects.Length} archivos.");
            return 1;
        }
        return 0;
    }

    private static int Package()
    {
        var packageDir = Path.Combine(SolutionDir, "artifacts", "package");
        Directory.CreateDirectory(packageDir);

        CopyItem(Path.Combine(SolutionDir, "README.md"), Path.Combine(packageDir, "README.md"));
        CopyItem(Path.Combine(SolutionDir, "docs"), Path.Combine(packageDir, "docs"));
        CopyItem(Path.Combine(SolutionDir, "LICENSE"), Path.Combine(packageDir, "LICENSE"));

        var requiredFiles = new[]
        {
            "Transcriptor.Desktop.exe",
            "tools/setup/transcriptor-setup.exe",
            "tools/rec/rec.exe",
            "README.md",
            "docs/SECURITY.md",
            "LICENSE",
            "docs/THIRD-PARTY-NOTICES.md",
            "dependency-inventory/desktop.packages.json",
            "dependency-inventory/recorder.packages.json",
            "dependency-inventory/transcription.packages.json",
        };

        foreach (var relative in requiredFiles)
        {
            var candidate = Path.GetFullPath(Path.Combine(packageDir, relative));
            if (!File.Exists(candidate))
            {
                WriteError($"Falta un archivo obligatorio del paquete de release: {candidate}");
                return 1;
            }
        }

        var packageFiles = Directory.GetFiles(packageDir, "*", SearchOption.AllDirectories);
        if (packageFiles.Length is 0)
        {
            WriteError("El paquete de release está vacío.");
            return 1;
        }

        var artifactsDir = Path.Combine(SolutionDir, "artifacts");
        Directory.CreateDirectory(artifactsDir);
        var archive = Path.Combine(artifactsDir, "Transcriptor-win-x64.zip");
        if (File.Exists(archive)) File.Delete(archive);
        ZipFile.CreateFromDirectory(packageDir, archive);
        if (!File.Exists(archive))
        {
            WriteError("No se creó el archivo de release Transcriptor-win-x64.zip.");
            return 1;
        }

        var sha256 = ComputeSha256(archive);
        File.WriteAllText(archive + ".sha256", $"{sha256}  Transcriptor-win-x64.zip");
        ValidateReleaseArchive(archive, requiredFiles);
        ScanForbiddenBytes(archive);
        Console.WriteLine($"[package] {archive} ({packageFiles.Length} archivos, SHA-256 {sha256}).");
        return 0;
    }

    private static void ValidateReleaseArchive(string archive, string[] requiredFiles)
    {
        var sidecar = File.ReadAllText(archive + ".sha256").Trim();
        var parts = sidecar.Split("  ", StringSplitOptions.None);
        if (parts.Length != 2 || !Regex.IsMatch(parts[0], "^[0-9a-fA-F]{64}$"))
        {
            throw new DesignData.DesignDataException($"Formato de sidecar SHA-256 no válido: {archive}.sha256");
        }
        var actualHash = ComputeSha256(archive);
        if (!string.Equals(parts[0], actualHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[1], "Transcriptor-win-x64.zip", StringComparison.Ordinal))
        {
            throw new DesignData.DesignDataException($"El sidecar SHA-256 no coincide con {archive}");
        }

        using var zip = System.IO.Compression.ZipFile.OpenRead(archive);
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            entries.Add(entry.FullName.Replace('\\', '/'));
        }
        foreach (var required in requiredFiles)
        {
            if (!entries.Contains(required))
            {
                throw new DesignData.DesignDataException($"El archivo de release falta entrada requerida: {required}");
            }
        }
    }

    private static void ScanForbiddenBytes(string archive)
    {
        var forbiddenEntryPattern = new Regex(@"(^|/)Transcriptor\.Dev(\.|$)|(^|/)DevSample|(^|/)dev-secret",
            RegexOptions.Compiled);
        var forbiddenStrings = new[]
        {
            "InsecureDevSecretProtector",
            "DevSampleLibrary",
            "ReversibleTestSecretProtector",
        };
        var needles = new List<(byte[] Bytes, string Reason)>();
        foreach (var text in forbiddenStrings)
        {
            needles.Add((System.Text.Encoding.Unicode.GetBytes(text), $"protector/biblioteca de desarrollo: {text}"));
            needles.Add((System.Text.Encoding.ASCII.GetBytes(text), $"protector/biblioteca de desarrollo: {text}"));
        }

        using var zip = System.IO.Compression.ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (forbiddenEntryPattern.IsMatch(entryPath))
            {
                throw new DesignData.DesignDataException(
                    $"El paquete de release contiene un ensamblado de desarrollo: {entryPath}");
            }
            if (!entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && !entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && !entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                && !entry.Name.EndsWith(".config", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            using var stream = entry.Open();
            var buffer = new byte[entry.Length];
            var read = 0;
            while (read < buffer.Length)
            {
                var chunk = stream.Read(buffer, read, buffer.Length - read);
                if (chunk <= 0) break;
                read += chunk;
            }
            if (read is 0) continue;
            var data = buffer.AsSpan(0, read);
            foreach (var (needle, reason) in needles)
            {
                if (Contains(data, needle))
                {
                    throw new DesignData.DesignDataException(
                        $"El paquete de release contiene {reason} dentro de: {entryPath}");
                }
            }
        }
    }

    private static bool Contains(ReadOnlySpan<byte> data, ReadOnlySpan<byte> needle)
    {
        if (needle.IsEmpty || needle.Length > data.Length) return false;
        var lastStart = data.Length - needle.Length;
        for (var start = 0; start <= lastStart; start++)
        {
            var match = true;
            for (var offset = 0; offset < needle.Length; offset++)
            {
                if (data[start + offset] != needle[offset])
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
        return false;
    }

    private static int Smoke(string[] exeArgs)
    {
        if (exeArgs.Length is not 1)
        {
            WriteError("Falta la ruta del ejecutable: smoke <ruta-al-.exe>.");
            return 1;
        }

        var exePath = Path.GetFullPath(exeArgs[0], SolutionDir);
        if (!File.Exists(exePath))
        {
            WriteError($"No existe el ejecutable para el smoke test: {exePath}");
            return 1;
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            ArgumentList = { "--smoke-test" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = SolutionDir,
        })!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Console.Write(output);
        Console.Error.Write(error);
        if (process.ExitCode != 0)
        {
            WriteError($"El smoke test falló con el código de salida {process.ExitCode}.");
            return process.ExitCode;
        }
        Console.WriteLine("[smoke] El ejecutable respondió con código de salida 0.");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        WriteError($"Subcomando desconocido: {command}. Use fetch, tokens, icons, inventory, package o smoke.");
        return 1;
    }

    private static string RunDotNet(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = SolutionDir,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new DesignData.DesignDataException($"dotnet {arguments} falló con código {process.ExitCode}: {error}");
        }
        return output;
    }

    private static void ValidateJson(string json, string name)
    {
        try
        {
            JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new DesignData.DesignDataException($"El inventario de {name} no es JSON válido: {ex.Message}.");
        }
    }

    private static void CopyItem(string source, string destination)
    {
        var sourceAttrs = File.GetAttributes(source);
        if ((sourceAttrs & FileAttributes.Directory) is FileAttributes.Directory)
        {
            Directory.CreateDirectory(destination);
            foreach (var child in Directory.GetFileSystemEntries(source))
            {
                CopyItem(child, Path.Combine(destination, Path.GetFileName(child)));
            }
        }
        else
        {
            File.Copy(source, destination, overwrite: true);
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }

    private static void WriteError(string message) => Console.Error.WriteLine($"error: {message}");

    private static string FindSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Transcriptor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
