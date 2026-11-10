using System.Text.RegularExpressions;

namespace Transcriptor.DesignData;

/// <summary>
/// Genera el contenido de `ui/Theming/Icons.axaml` a partir del paquete oficial
/// `@material-symbols/svg-400@0.48.3` (Material Symbols, peso 400, estilo
/// <c>outlined</c>, Apache-2.0), que es el set que usa el diseño de Google Stitch
/// ("Spectrum Audio Core").
/// </summary>
/// <remarks>
/// Todos los SVG del paquete usan <c>viewBox="0 -960 960 960"</c>. Para poder pintarlos
/// sobre un lienzo de 960 × 960 (el tema de <c>PathIcon</c> usa un <c>Canvas</c> del
/// tamaño <c>icon-viewbox-size</c>, de modo que todos los iconos conservan su relleno
/// óptico original y ninguno se estira hasta los bordes), las coordenadas Y absolutas se
/// desplazan +960. El resultado es una geometría en el rango 0–960 en ambos ejes.
/// </remarks>
public static class IconGenerator
{
    private const string PackageName = "@material-symbols/svg-400";
    private const string PackageVersion = "0.48.3";
    private const string ExpectedViewBox = "0 -960 960 960";
    private const int CanvasSize = 960;

    // Lista blanca: clave XAML -> SVG dentro del paquete extraído. Las claves se conservan
    // respecto al set anterior para no tocar las vistas. Los iconos de transporte y de
    // estado usan la variante rellena (`-fill`), como el diseño de Stitch (FILL 1); el
    // resto es de contorno. Los nombres de archivo se verificaron contra el paquete fijado.
    private static readonly (string XamlKey, string Member)[] Whitelist =
    [
        // Transporte (rellenos)
        ("icon-Play", "outlined/play_arrow-fill.svg"),
        ("icon-Pause", "outlined/pause-fill.svg"),
        ("icon-Stop", "outlined/stop-fill.svg"),
        // Audio
        ("icon-Microphone", "outlined/mic.svg"),
        ("icon-AudioWave", "outlined/graphic_eq.svg"),
        ("icon-VolumeTwo", "outlined/volume_up.svg"),
        // Cromo de ventana
        ("icon-Close", "outlined/close.svg"),
        ("icon-Minimize", "outlined/minimize.svg"),
        // Acciones
        ("icon-Delete", "outlined/delete.svg"),
        ("icon-Copy", "outlined/content_copy.svg"),
        ("icon-Export", "outlined/download.svg"),
        ("icon-Refresh", "outlined/refresh.svg"),
        // Navegación / archivo
        ("icon-FolderOpen", "outlined/folder_open.svg"),
        ("icon-ViewList", "outlined/view_list.svg"),
        ("icon-Settings", "outlined/settings.svg"),
        ("icon-Terminal", "outlined/terminal.svg"),
        // Seguridad
        ("icon-Lock", "outlined/lock.svg"),
        ("icon-LockOpen", "outlined/lock_open.svg"),
        // Biblioteca
        ("icon-Filter", "outlined/filter_alt.svg"),
        ("icon-Sort", "outlined/sort.svg"),
        ("icon-Search", "outlined/search.svg"),
        // Transcripción
        ("icon-Transcript", "outlined/subtitles.svg"),
        ("icon-FileText", "outlined/description.svg"),
        // Chevrones y marca de verificación
        ("icon-Chevron", "outlined/chevron_right.svg"),
        ("icon-ChevronUp", "outlined/keyboard_arrow_up.svg"),
        ("icon-ChevronDown", "outlined/keyboard_arrow_down.svg"),
        ("icon-Checkmark", "outlined/check.svg"),
        // Estado (rellenos)
        ("icon-InfoCircle", "outlined/info-fill.svg"),
        ("icon-CheckmarkCircle", "outlined/check_circle-fill.svg"),
        ("icon-AlertTriangle", "outlined/warning-fill.svg"),
    ];

    private static readonly Regex CommandGroup = new(
        @"([MmLlHhVvCcSsQqTtAaZz])([^MmLlHhVvCcSsQqTtAaZz]*)",
        RegexOptions.Compiled);

    private static readonly Regex NumberToken = new(
        @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?",
        RegexOptions.Compiled);

    public static string Generate(string materialPackageDir)
    {
        var icons = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (xamlKey, member) in Whitelist)
        {
            var svgPath = Path.GetFullPath(Path.Combine(materialPackageDir, member));
            if (!File.Exists(svgPath))
            {
                throw new DesignDataException(
                    $"No existe el icono {member} en {PackageName}@{PackageVersion}.");
            }

            icons[xamlKey] = SvgPathData(File.ReadAllText(svgPath));
        }

        var lines = new List<string>
        {
            "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\"",
            "                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">",
            $"  <!-- Generado desde {PackageName}@{PackageVersion} (Material Symbols, outlined, peso 400); no editar manualmente. -->",
            $"  <!-- Todas las geometrías viven en un lienzo de {CanvasSize} x {CanvasSize}. -->",
            $"  <x:Double x:Key=\"icon-viewbox-size\">{CanvasSize}</x:Double>",
            "",
        };

        foreach (var key in icons.Keys.OrderBy(static k => k, StringComparer.Ordinal))
        {
            lines.Add($"  <StreamGeometry x:Key=\"{key}\">{icons[key]}</StreamGeometry>");
        }

        lines.Add("");
        lines.Add("</ResourceDictionary>");
        lines.Add("");

        return string.Join("\n", lines);
    }

    private static string SvgPathData(string svgXml)
    {
        var document = XDocument.Parse(svgXml);
        var root = document.Root ?? throw new DesignDataException("El SVG no tiene elemento raíz.");
        var rootTag = root.Name.LocalName;
        if (rootTag != "svg")
        {
            throw new DesignDataException($"La raíz del SVG es <{rootTag}>, se esperaba <svg>");
        }

        var viewBox = root.Attribute(XName.Get("viewBox"))?.Value;
        if (viewBox != ExpectedViewBox)
        {
            throw new DesignDataException(
                $"El viewBox del SVG es '{viewBox}', se esperaba '{ExpectedViewBox}'.");
        }

        var fillRule = root.Attribute(XName.Get("fill-rule"))?.Value;

        var pathParts = new List<string>();
        foreach (var elem in root.Descendants())
        {
            var tag = elem.Name.LocalName;
            if (tag == "path")
            {
                if (elem.Attribute(XName.Get("transform")) is not null)
                {
                    throw new DesignDataException("El <path> del SVG lleva un atributo transform.");
                }
                var d = elem.Attribute(XName.Get("d"))?.Value
                    ?? throw new DesignDataException("El <path> del SVG no tiene atributo d.");
                pathParts.Add(d.Trim());
            }
            else if (tag is not ("defs" or "title" or "desc" or "metadata"))
            {
                throw new DesignDataException($"El SVG contiene un elemento no soportado <{tag}>.");
            }
        }

        if (pathParts.Count == 0)
        {
            throw new DesignDataException("El SVG no contiene un elemento <path d>.");
        }

        // El valor por defecto de SVG es nonzero; Avalonia StreamGeometry asume evenodd.
        // Si el SVG no declara fill-rule, se antepone F1 para preservar el renderizado.
        var prefix = fillRule == "evenodd" ? "F0 " : "F1 ";

        // Cada <path> se desplaza por separado: el primer comando de cada uno es un `M`
        // que se emite en minúscula (relativo desde 0,0, igual al absoluto inicial). El
        // analizador de StreamGeometry de Avalonia 12.1 espera una coordenada, no un
        // comando en mayúscula, justo después de F0/F1.
        var shifted = pathParts.Select(ShiftIntoCanvas);
        return prefix + string.Join(" ", shifted);
    }

    /// <summary>
    /// Desplaza +960 las coordenadas Y absolutas de un path con viewBox "0 -960 960 960",
    /// dejando intactos los comandos relativos y el resto del texto (separadores incluidos).
    /// </summary>
    private static string ShiftIntoCanvas(string pathD)
    {
        var builder = new StringBuilder(pathD.Length + 16);
        var consumed = 0;
        var isFirstGroup = true;

        foreach (Match group in CommandGroup.Matches(pathD))
        {
            if (group.Index != consumed)
            {
                throw new DesignDataException("El path del SVG contiene texto fuera de un comando.");
            }
            consumed = group.Index + group.Length;

            var command = group.Groups[1].Value[0];
            var args = group.Groups[2].Value;
            var numbers = NumberToken.Matches(args);

            if (command is 'A' or 'a')
            {
                throw new DesignDataException(
                    "El path del SVG usa arcos (A/a), no soportados por el desplazamiento de coordenadas.");
            }

            var arity = Arity(command);
            if (arity > 0 && numbers.Count % arity != 0)
            {
                throw new DesignDataException(
                    $"El comando '{command}' tiene {numbers.Count} números, no múltiplo de {arity}.");
            }

            var firstIsAbsoluteMove = isFirstGroup && command == 'M';
            if (isFirstGroup)
            {
                if (command is not ('M' or 'm') || numbers.Count < 2)
                {
                    throw new DesignDataException(
                        "El path del SVG debe empezar con un movimiento M/m x y.");
                }
                builder.Append('m');
            }
            else
            {
                builder.Append(command);
            }

            int[] yPositions = char.IsUpper(command) ? AbsoluteYPositions(command) : [];

            var cursor = 0;
            for (var i = 0; i < numbers.Count; i++)
            {
                var match = numbers[i];

                // Un `M x y x2 y2 …` absoluto equivale a `M x y L x2 y2 …`. Al emitir el
                // primer movimiento en minúscula (relativo desde 0,0), las parejas
                // implícitas pasarían a ser relativas, así que se declara `L` explícito.
                if (firstIsAbsoluteMove && i == 2)
                {
                    builder.Append(args, cursor, match.Index - cursor);
                    builder.Append('L');
                    cursor = match.Index;
                }

                builder.Append(args, cursor, match.Index - cursor);

                // En el primer movimiento relativo (`m`) solo la primera pareja es absoluta
                // (parte de 0,0); las siguientes son líneas relativas y no se tocan.
                var position = arity == 0 ? 0 : i % arity;
                var isShifted = isFirstGroup && !firstIsAbsoluteMove
                    ? i == 1
                    : Array.IndexOf(yPositions, position) >= 0;
                var text = isShifted ? Shift(match.Value) : match.Value;

                // Un número negativo original hacía de separador ("408-453.92"); tras
                // desplazarlo es positivo y hay que separarlo explícitamente del anterior.
                if (text.Length > 0 && text[0] is not ('-' or '+')
                    && builder.Length > 0 && (char.IsDigit(builder[builder.Length - 1]) || builder[builder.Length - 1] == '.'))
                {
                    builder.Append(' ');
                }

                builder.Append(text);
                cursor = match.Index + match.Length;
            }
            builder.Append(args, cursor, args.Length - cursor);

            isFirstGroup = false;
        }

        if (consumed != pathD.Length || builder.Length == 0)
        {
            throw new DesignDataException("El path del SVG no se pudo analizar por completo.");
        }

        return builder.ToString();
    }

    private static int Arity(char command) => char.ToUpperInvariant(command) switch
    {
        'M' or 'L' or 'T' => 2,
        'H' or 'V' => 1,
        'C' => 6,
        'S' or 'Q' => 4,
        'Z' => 0,
        _ => throw new DesignDataException($"Comando de path no soportado: {command}."),
    };

    private static int[] AbsoluteYPositions(char command) => command switch
    {
        'M' or 'L' or 'T' => [1],
        'V' => [0],
        'C' => [1, 3, 5],
        'S' or 'Q' => [1, 3],
        _ => [],
    };

    private static string Shift(string value)
    {
        var number = decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return (number + CanvasSize).ToString("0.##########", CultureInfo.InvariantCulture);
    }
}
