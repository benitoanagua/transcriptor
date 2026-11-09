namespace Transcriptor.DesignData;

/// <summary>
/// Genera el contenido de `ui/Theming/Icons.axaml` a partir de los paquetes
/// oficiales `@fluentui/svg-icons@1.1.345` (iconos de la interfaz, SVG relleno
/// de 20 px, MIT) y `@spectrum-css/ui-icons@1.1.2` (solo los iconos internos de
/// componentes: checkmark y chevron del selector/casilla).
/// </summary>
public static class IconGenerator
{
    private const string FluentName = "@fluentui/svg-icons";
    private const string FluentVersion = "1.1.345";
    private const string UiIconsName = "@spectrum-css/ui-icons";
    private const string UiIconsVersion = "1.1.2";

    // Whitelist: paquete -> (miembro SVG dentro del paquete extraído, clave XAML, token de tamaño).
    // El token de tamaño se conserva por paridad con el generador original, aunque
    // la salida XAML solo emite la geometría. Todos los miembros de Fluent son SVG
    // de rejilla 20 px con variante rellena (_filled), lo que da trazos gruesos y
    // legibles sobre fondo oscuro. Los nombres de archivo se verificaron contra el
    // paquete descargado antes de fijar la versión.
    private static readonly (string Package, string Member, string XamlKey, string SizeToken)[] Whitelist =
    [
        // Transporte
        (FluentName, "package/icons/play_20_filled.svg", "icon-Play", "workflow-icon-size-100"),
        (FluentName, "package/icons/pause_20_filled.svg", "icon-Pause", "workflow-icon-size-100"),
        // Audio
        (FluentName, "package/icons/mic_20_filled.svg", "icon-Microphone", "workflow-icon-size-100"),
        (FluentName, "package/icons/speaker_1_20_filled.svg", "icon-VolumeTwo", "workflow-icon-size-100"),
        // Cromo de ventana
        (FluentName, "package/icons/dismiss_20_filled.svg", "icon-Close", "workflow-icon-size-100"),
        (FluentName, "package/icons/arrow_minimize_20_filled.svg", "icon-Minimize", "workflow-icon-size-100"),
        // Acciones
        (FluentName, "package/icons/delete_20_filled.svg", "icon-Delete", "workflow-icon-size-100"),
        (FluentName, "package/icons/copy_20_filled.svg", "icon-Copy", "workflow-icon-size-100"),
        (FluentName, "package/icons/send_20_filled.svg", "icon-Export", "workflow-icon-size-100"),
        (FluentName, "package/icons/arrow_clockwise_20_filled.svg", "icon-Refresh", "workflow-icon-size-100"),
        // Navegación / archivo
        (FluentName, "package/icons/folder_open_20_filled.svg", "icon-FolderOpen", "workflow-icon-size-100"),
        (FluentName, "package/icons/list_20_filled.svg", "icon-ViewList", "workflow-icon-size-100"),
        (FluentName, "package/icons/settings_20_filled.svg", "icon-Settings", "workflow-icon-size-100"),
        // Seguridad
        (FluentName, "package/icons/lock_closed_20_filled.svg", "icon-Lock", "workflow-icon-size-100"),
        (FluentName, "package/icons/lock_open_20_filled.svg", "icon-LockOpen", "workflow-icon-size-100"),
        // Biblioteca
        (FluentName, "package/icons/filter_20_filled.svg", "icon-Filter", "workflow-icon-size-75"),
        (FluentName, "package/icons/arrow_sort_down_20_filled.svg", "icon-Sort", "workflow-icon-size-75"),
        // Transcripción
        (FluentName, "package/icons/document_20_filled.svg", "icon-Transcript", "workflow-icon-size-100"),
        (FluentName, "package/icons/document_20_filled.svg", "icon-FileText", "workflow-icon-size-75"),
        // Chevrones (reemplazan Unicode ↑ ↓)
        (FluentName, "package/icons/chevron_up_20_filled.svg", "icon-ChevronUp", "workflow-icon-size-75"),
        (FluentName, "package/icons/chevron_down_20_filled.svg", "icon-ChevronDown", "workflow-icon-size-75"),
        // Iconos de alerta
        (FluentName, "package/icons/info_20_filled.svg", "icon-InfoCircle", "workflow-icon-size-100"),
        (FluentName, "package/icons/checkmark_circle_20_filled.svg", "icon-CheckmarkCircle", "workflow-icon-size-100"),
        (FluentName, "package/icons/warning_20_filled.svg", "icon-AlertTriangle", "workflow-icon-size-100"),
        // Cromo de ventana (sonido y búsqueda)
        (FluentName, "package/icons/mic_20_filled.svg", "icon-AudioWave", "workflow-icon-size-200"),
        (FluentName, "package/icons/search_20_filled.svg", "icon-Search", "workflow-icon-size-75"),
        // Iconos de componente (se dejan de Spectrum ui-icons por ser internos de componentes)
        (UiIconsName, "package/dist/combined/Checkmark100.svg", "icon-Checkmark", "workflow-icon-size-100"),
        (UiIconsName, "package/dist/combined/Chevron100.svg", "icon-Chevron", "workflow-icon-size-100"),
    ];

    public static string Generate(string fluentPackageDir, string uiIconsPackageDir)
    {
        var icons = new Dictionary<string, (string Geometry, string SizeToken)>(StringComparer.Ordinal);

        foreach (var (package, member, xamlKey, sizeToken) in Whitelist)
        {
            var packageDir = package == FluentName ? fluentPackageDir : uiIconsPackageDir;
            var memberRelative = member.StartsWith("package/", StringComparison.Ordinal)
                ? member["package/".Length..]
                : member;
            var svgPath = Path.GetFullPath(Path.Combine(packageDir, memberRelative));
            var svgXml = File.ReadAllText(svgPath);
            var geometry = SvgPathData(svgXml);
            icons[xamlKey] = (geometry, sizeToken);
        }

        var lines = new List<string>
        {
            "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\"",
            "                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">",
            $"  <!-- Generado desde {FluentName}@{FluentVersion} y {UiIconsName}@{UiIconsVersion}; no editar manualmente. -->",
            "",
        };

        foreach (var key in icons.Keys.OrderBy(static k => k, StringComparer.Ordinal))
        {
            var (geometry, _) = icons[key];
            lines.Add($"  <StreamGeometry x:Key=\"{key}\">{geometry}</StreamGeometry>");
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
        var rootTag = LocalName(root);
        if (rootTag != "svg")
        {
            throw new DesignDataException($"La raíz del SVG es <{rootTag}>, se esperaba <svg>");
        }

        var fillRule = root.Attribute(XName.Get("fill-rule"))?.Value;

        var pathParts = new List<string>();
        foreach (var elem in root.Descendants())
        {
            var tag = LocalName(elem);
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

        var pathD = string.Join(" ", pathParts).Trim();

        // El valor por defecto de SVG es nonzero; Avalonia StreamGeometry asume evenodd.
        // Si el SVG no declara fill-rule, se antepone F1 para preservar el renderizado.
        //
        // Avalonia 12.1.0's StreamGeometry parser expects a coordinate after F1/F0,
        // not an uppercase path command. The first command is lowercased (relative from
        // 0,0, which equals the absolute command at the start position) so the geometry
        // parses correctly on Avalonia while preserving the intended fill rule.
        var prefix = fillRule == "evenodd" ? "F0 " : "F1 ";
        var adjusted = pathD;
        if (adjusted.Length > 0 && char.IsUpper(adjusted[0]))
            adjusted = char.ToLowerInvariant(adjusted[0]) + adjusted[1..];
        return prefix + adjusted;
    }

    private static string LocalName(XElement element)
    {
        var name = element.Name.LocalName;
        return name;
    }
}
