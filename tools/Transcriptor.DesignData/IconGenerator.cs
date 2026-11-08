namespace Transcriptor.DesignData;

/// <summary>
/// Genera el contenido de `ui/Theming/Icons.axaml` a partir de los paquetes
/// oficiales `@adobe/spectrum-css-workflow-icons@5.0.0` y
/// `@spectrum-css/ui-icons@1.1.2`, con paridad byte a byte con el generador
/// original de Python.
/// </summary>
public static class IconGenerator
{
    private const string WorkflowName = "@adobe/spectrum-css-workflow-icons";
    private const string WorkflowVersion = "5.0.0";
    private const string UiIconsName = "@spectrum-css/ui-icons";
    private const string UiIconsVersion = "1.1.2";

    // Whitelist: paquete -> (miembro SVG dentro del paquete extraído, clave XAML, token de tamaño).
    // El token de tamaño se conserva por paridad con el generador original, aunque
    // la salida XAML solo emite la geometría.
    private static readonly (string Package, string Member, string XamlKey, string SizeToken)[] Whitelist =
    [
        // Transporte
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Play_20_N.svg", "icon-Play", "workflow-icon-size-100"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Pause_20_N.svg", "icon-Pause", "workflow-icon-size-100"),
        // Audio
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Microphone_20_N.svg", "icon-Microphone", "workflow-icon-size-100"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_VolumeTwo_20_N.svg", "icon-VolumeTwo", "workflow-icon-size-100"),
        // Cromo de ventana
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Close_20_N.svg", "icon-Close", "workflow-icon-size-100"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Minimize_20_N.svg", "icon-Minimize", "workflow-icon-size-100"),
        // Acciones
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Delete_20_N.svg", "icon-Delete", "workflow-icon-size-75"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Copy_20_N.svg", "icon-Copy", "workflow-icon-size-75"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Export_20_N.svg", "icon-Export", "workflow-icon-size-75"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Refresh_20_N.svg", "icon-Refresh", "workflow-icon-size-75"),
        // Navegación / archivo
        (WorkflowName, "package/dist/assets/svg/S2_Icon_FolderOpen_20_N.svg", "icon-FolderOpen", "workflow-icon-size-100"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_ViewList_20_N.svg", "icon-ViewList", "workflow-icon-size-75"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Settings_20_N.svg", "icon-Settings", "workflow-icon-size-100"),
        // Seguridad
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Lock_20_N.svg", "icon-Lock", "workflow-icon-size-100"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_LockOpen_20_N.svg", "icon-LockOpen", "workflow-icon-size-100"),
        // Biblioteca
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Filter_20_N.svg", "icon-Filter", "workflow-icon-size-75"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Sort_20_N.svg", "icon-Sort", "workflow-icon-size-75"),
        // Transcripción
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Transcript_20_N.svg", "icon-Transcript", "workflow-icon-size-100"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_FileText_20_N.svg", "icon-FileText", "workflow-icon-size-75"),
        // Chevrones (reemplazan Unicode ↑ ↓)
        (WorkflowName, "package/dist/assets/svg/S2_Icon_ChevronUp_20_N.svg", "icon-ChevronUp", "workflow-icon-size-75"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_ChevronDown_20_N.svg", "icon-ChevronDown", "workflow-icon-size-75"),
        // Iconos de alerta
        (WorkflowName, "package/dist/assets/svg/S2_Icon_InfoCircle_20_N.svg", "icon-InfoCircle", "workflow-icon-size-100"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_CheckmarkCircle_20_N.svg", "icon-CheckmarkCircle", "workflow-icon-size-100"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_AlertTriangle_20_N.svg", "icon-AlertTriangle", "workflow-icon-size-100"),
        // Cromo de ventana (sonido y búsqueda)
        (WorkflowName, "package/dist/assets/svg/S2_Icon_AudioWave_20_N.svg", "icon-AudioWave", "workflow-icon-size-200"),
        (WorkflowName, "package/dist/assets/svg/S2_Icon_Search_20_N.svg", "icon-Search", "workflow-icon-size-75"),
        // Iconos de componente
        (UiIconsName, "package/dist/combined/Checkmark100.svg", "icon-Checkmark", "workflow-icon-size-100"),
        (UiIconsName, "package/dist/combined/Chevron100.svg", "icon-Chevron", "workflow-icon-size-100"),
    ];

    public static string Generate(string workflowPackageDir, string uiIconsPackageDir)
    {
        var icons = new Dictionary<string, (string Geometry, string SizeToken)>(StringComparer.Ordinal);

        foreach (var (package, member, xamlKey, sizeToken) in Whitelist)
        {
            var packageDir = package == WorkflowName ? workflowPackageDir : uiIconsPackageDir;
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
            $"  <!-- Generado desde {WorkflowName}@{WorkflowVersion} y {UiIconsName}@{UiIconsVersion}; no editar manualmente. -->",
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
        if (fillRule is null or "nonzero") return "F1 " + pathD;
        if (fillRule == "evenodd") return "F0 " + pathD;
        throw new DesignDataException($"fill-rule no soportado: {fillRule}.");
    }

    private static string LocalName(XElement element)
    {
        var name = element.Name.LocalName;
        return name;
    }
}
