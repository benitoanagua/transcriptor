namespace Transcriptor.DesignData;

/// <summary>
/// Genera el contenido de `ui/Theming/Tokens.axaml` a partir de las variables
/// del paquete oficial `@adobe/spectrum-tokens`, con paridad byte a byte con
/// el generador original de Python.
/// </summary>
public static class TokenGenerator
{
    private const string PackageName = "@adobe/spectrum-tokens";
    private const string Version = "15.6.0";

    // Tokens de color (resueltos sobre el conjunto "dark").
    private static readonly string[] ColorTokens =
    [
        "gray-25", "gray-50", "gray-75", "gray-100", "gray-200", "gray-300",
        "gray-400", "gray-500", "gray-600", "gray-700", "gray-800", "gray-900",
        "gray-1000", "blue-700", "blue-800", "blue-900", "blue-1000",
        "background-base-color", "background-elevated-color",
        "background-layer-1-color", "background-layer-2-color",
        "background-pasteboard-color", "card-background-well-color",
        "accent-background-color-default",
        "accent-background-color-hover", "accent-background-color-down",
        "accent-content-color-default", "accent-subtle-background-color-default",
        "neutral-background-color-default",
        "neutral-background-color-hover", "neutral-background-color-down",
        "neutral-subdued-background-color-default",
        "neutral-subdued-background-color-hover", "neutral-subdued-background-color-down",
        "neutral-subtle-background-color-default",
        "neutral-content-color-default", "neutral-subdued-content-color-default",
        "disabled-content-color", "disabled-border-color", "disabled-background-color",
        "heading-color", "title-color", "body-color", "detail-color",
        "static-white-text-color", "static-black-text-color",
        "icon-color-neutral", "icon-color-primary-default", "icon-color-informative",
        "icon-color-positive", "icon-color-notice", "icon-color-negative",
        "gray-visual-color", "neutral-visual-color",
        "popover-border-color", "action-bar-border-color",
        "focus-indicator-color", "drop-shadow-color", "drop-shadow-color-100",
        "drop-shadow-color-200", "drop-shadow-color-300", "drop-shadow-ambient-color",
        "drop-shadow-transition-color", "drop-shadow-elevated-color",
        "negative-background-color-default",
        "negative-background-color-hover", "negative-background-color-down",
        "negative-subtle-background-color-default", "negative-content-color-default",
        "negative-border-color-default",
        "positive-background-color-default", "positive-subtle-background-color-default",
        "positive-color-900", "notice-background-color-default",
        "notice-subtle-background-color-default", "notice-color-900",
        "notice-color-1000", "informative-background-color-default",
        "informative-visual-color", "informative-subtle-background-color-default",
        "transparent-white-25", "transparent-white-100",
    ];

    // Tokens numéricos (resueltos sobre "desktop", salvo corner-radius-* que usa "dark").
    private static readonly string[] DoubleTokens =
    [
        "spacing-25", "spacing-50", "spacing-75", "spacing-85", "spacing-100",
        "spacing-200", "spacing-300", "spacing-350", "spacing-400", "spacing-500",
        "spacing-600", "spacing-700", "spacing-800", "spacing-900",
        "component-height-50", "component-height-75", "component-height-100",
        "component-height-200", "component-height-300", "component-height-400",
        "font-size-25", "font-size-50", "font-size-75", "font-size-body-small", "font-size-100",
        "font-size-200", "font-size-300", "font-size-400", "font-size-500",
        "font-size-600", "font-size-700", "font-size-800", "font-size-900",
        "line-height-font-size-50", "line-height-font-size-75",
        "line-height-font-size-100", "line-height-font-size-200",
        "line-height-font-size-300", "line-height-font-size-400",
        "line-height-font-size-500", "field-width-small", "field-width-medium",
        "focus-indicator-gap", "focus-indicator-thickness",
        "status-light-dot-size-small", "status-light-dot-size-medium",
        "status-light-text-to-visual-100",
        "text-to-control-75", "text-to-control-100",
        "checkbox-control-size-small", "checkbox-control-size-medium",
        "radio-button-control-size-small", "radio-button-control-size-medium",
        "radio-button-selection-indicator",
        "switch-control-height-small", "switch-control-height-medium",
        "switch-control-width-small", "switch-control-width-medium",
        "switch-handle-size-small", "switch-handle-size-medium",
        "switch-handle-selected-size-small", "switch-handle-selected-size-medium",
        "slider-handle-size-small", "slider-handle-size-medium",
        "slider-track-thickness",
        "progress-circle-size-small", "progress-circle-size-medium",
        "progress-circle-thickness-small", "progress-circle-thickness-medium",
        "divider-thickness-small", "divider-thickness-medium",
        "toast-height", "toast-maximum-width",
        "tooltip-maximum-width",
        "alert-dialog-minimum-width", "alert-dialog-maximum-width",
        "overlay-opacity",
        "opacity-disabled", "corner-radius-0-value", "corner-radius-full-value",
        "border-width-100", "border-width-200", "border-width-400",
        "list-view-max-height", "list-view-max-height-expanded", "settings-form-max-width",
        "workflow-icon-size-50", "workflow-icon-size-75", "workflow-icon-size-100",
        "workflow-icon-size-200", "workflow-icon-size-300",
    ];

    private static readonly string[] CornerRadiusTokens =
    [
        "corner-radius-0", "corner-radius-75", "corner-radius-100",
        "corner-radius-200", "corner-radius-300", "corner-radius-400",
        "corner-radius-500", "corner-radius-600", "corner-radius-700",
        "corner-radius-800", "corner-radius-full",
    ];

    private static readonly Dictionary<string, string[]> ThicknessTokens = new(StringComparer.Ordinal)
    {
        ["spacing-0-thickness"] = ["corner-radius-0", "corner-radius-0", "corner-radius-0", "corner-radius-0"],
        ["spacing-25-thickness"] = ["spacing-25", "spacing-25", "spacing-25", "spacing-25"],
        ["spacing-50-thickness"] = ["spacing-50", "spacing-50", "spacing-50", "spacing-50"],
        ["spacing-75-thickness"] = ["spacing-75", "spacing-75", "spacing-75", "spacing-75"],
        ["spacing-85-thickness"] = ["spacing-85", "spacing-85", "spacing-85", "spacing-85"],
        ["spacing-100-thickness"] = ["spacing-100", "spacing-100", "spacing-100", "spacing-100"],
        ["spacing-200-thickness"] = ["spacing-200", "spacing-200", "spacing-200", "spacing-200"],
        ["spacing-300-thickness"] = ["spacing-300", "spacing-300", "spacing-300", "spacing-300"],
        ["spacing-400-thickness"] = ["spacing-400", "spacing-400", "spacing-400", "spacing-400"],
        ["spacing-100x75-thickness"] = ["spacing-100", "spacing-75"],
        ["spacing-200x75-thickness"] = ["spacing-200", "spacing-75"],
        ["spacing-200x85-thickness"] = ["spacing-200", "spacing-85"],
        ["spacing-300x75-thickness"] = ["spacing-300", "spacing-75"],
        ["spacing-100x50-thickness"] = ["spacing-100", "spacing-50"],
        ["spacing-200x50-thickness"] = ["spacing-200", "spacing-50"],
        ["spacing-200x100-thickness"] = ["spacing-200", "spacing-100"],
        ["spacing-85x50-thickness"] = ["spacing-85", "spacing-50"],
        ["spacing-200x0-thickness"] = ["spacing-200", "corner-radius-0"],
        ["spacing-100x0-thickness"] = ["spacing-100", "corner-radius-0"],
        ["spacing-0x100-thickness"] = ["corner-radius-0", "spacing-100"],
        ["spacing-0x200-thickness"] = ["corner-radius-0", "spacing-200"],
        ["padding-top-spacing-100-thickness"] = ["corner-radius-0", "spacing-100", "corner-radius-0", "corner-radius-0"],
        ["padding-bottom-spacing-100-thickness"] = ["corner-radius-0", "corner-radius-0", "corner-radius-0", "spacing-100"],
        ["border-bottom-1-thickness"] = ["corner-radius-0", "corner-radius-0", "corner-radius-0", "spacing-25"],
        ["border-top-1-thickness"] = ["corner-radius-0", "spacing-25", "corner-radius-0", "corner-radius-0"],
        ["border-bottom-focus-thickness"] = ["corner-radius-0", "corner-radius-0", "corner-radius-0", "focus-indicator-thickness"],
        ["border-left-1-thickness"] = ["spacing-25", "corner-radius-0", "corner-radius-0", "corner-radius-0"],
        ["radio-button-selection-indicator-thickness"] = ["radio-button-selection-indicator", "radio-button-selection-indicator", "radio-button-selection-indicator", "radio-button-selection-indicator"],
        ["progress-circle-thickness-small-thickness"] = ["progress-circle-thickness-small", "progress-circle-thickness-small", "progress-circle-thickness-small", "progress-circle-thickness-small"],
        ["progress-circle-thickness-medium-thickness"] = ["progress-circle-thickness-medium", "progress-circle-thickness-medium", "progress-circle-thickness-medium", "progress-circle-thickness-medium"],
        ["margin-bottom-spacing-100-thickness"] = ["corner-radius-0", "corner-radius-0", "corner-radius-0", "spacing-100"],
        ["border-width-100-thickness"] = ["border-width-100", "border-width-100", "border-width-100", "border-width-100"],
        ["border-width-200-thickness"] = ["border-width-200", "border-width-200", "border-width-200", "border-width-200"],
        ["border-width-400-thickness"] = ["border-width-400", "border-width-400", "border-width-400", "border-width-400"],
    };

    private static readonly Dictionary<string, string> FontWeightTokens = new(StringComparer.Ordinal)
    {
        ["regular-font-weight"] = "Regular",
        ["medium-font-weight"] = "Medium",
        ["bold-font-weight"] = "Bold",
        ["extra-bold-font-weight"] = "ExtraBold",
    };

    // Peso derivado (no existe en Spectrum 2): el diseño de Google Stitch usa 600 para
    // etiquetas y títulos (`label-*`, `headline-*`). Se emite sin validar contra el paquete.
    private const string SemiBoldWeightToken = "semi-bold-font-weight";

    public static string Generate(IDictionary<string, JsonElement> variables)
    {
        var lines = new List<string>
        {
            "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\"",
            "                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">",
            $"  <!-- Generado desde {PackageName}@{Version}; no editar manualmente. -->",
        };

        foreach (var name in ColorTokens)
        {
            var value = Color(TokenValue(variables, name, "dark"));
            lines.Add($"  <SolidColorBrush x:Key=\"{name}\">{value}</SolidColorBrush>");
        }

        foreach (var name in DoubleTokens)
        {
            string value;
            if (name == "list-view-max-height")
            {
                var total = 5 * ParseDimension(TokenValue(variables, "component-height-200", "desktop"))
                    + ParseDimension(TokenValue(variables, "spacing-350", "desktop"));
                value = FormatDouble(total);
            }
            else if (name == "list-view-max-height-expanded")
            {
                // Derivado: altura de la lista de sesiones cuando no hay ninguna seleccionada
                // (el panel de detalle se colapsa y la lista puede usar ese espacio): 10 filas
                // de component-height-200 más spacing-350, el doble de filas que el tope normal.
                var total = 10 * ParseDimension(TokenValue(variables, "component-height-200", "desktop"))
                    + ParseDimension(TokenValue(variables, "spacing-350", "desktop"));
                value = FormatDouble(total);
            }
            else if (name == "settings-form-max-width")
            {
                var total = 3 * ParseDimension(TokenValue(variables, "field-width-medium", "desktop"));
                value = FormatDouble(total);
            }
            else if (name == "font-size-body-small")
            {
                // Derivado (no existe en Spectrum 2): 13 px para texto corrido y de estado.
                // Source Sans 3 tiene x-height baja; a 12 px (font-size-75) se ve fina sobre
                // fondo oscuro. Las etiquetas en mayúsculas siguen usando font-size-75.
                value = "13";
            }
            else if (name == "corner-radius-full-value")
            {
                value = "9999";
            }
            else
            {
                var sourceName = name == "corner-radius-0-value" ? "corner-radius-0" : name;
                value = Dimension(TokenValue(variables, sourceName, "desktop"));
            }

            lines.Add($"  <x:Double x:Key=\"{name}\">{value}</x:Double>");
        }

        foreach (var name in CornerRadiusTokens)
        {
            var value = name == "corner-radius-full"
                ? "9999"
                : Dimension(TokenValue(variables, name, "dark"));
            lines.Add($"  <CornerRadius x:Key=\"{name}\">{value}</CornerRadius>");
        }

        foreach (var (name, sourceNames) in ThicknessTokens)
        {
            var values = sourceNames.Select(source => Dimension(TokenValue(variables, source, "desktop"))).ToList();
            if (values.Count == 1) values = [values[0], values[0], values[0], values[0]];
            else if (values.Count == 2) values = [values[0], values[1], values[0], values[1]];
            lines.Add($"  <Thickness x:Key=\"{name}\">{string.Join(",", values)}</Thickness>");
        }

        foreach (var (name, avaloniaWeight) in FontWeightTokens)
        {
            var sourceWeight = TokenValue(variables, name, "dark").GetString()!;
            if (!sourceWeight.Trim().ToLowerInvariant().Replace("-", "").Equals(
                    avaloniaWeight.ToLowerInvariant(), StringComparison.Ordinal))
            {
                throw new DesignDataException($"El token oficial {name} cambió: {sourceWeight}.");
            }
            lines.Add($"  <FontWeight x:Key=\"{name}\">{avaloniaWeight}</FontWeight>");
        }
        lines.Add($"  <FontWeight x:Key=\"{SemiBoldWeightToken}\">SemiBold</FontWeight>");

        // Desviación deliberada de Spectrum 2: el diseño de Google Stitch ("Spectrum Audio
        // Core") fija JetBrains Mono como fuente monoespaciada. Se sigue vigilando el token
        // oficial para detectar cambios, pero la familia emitida es la de Stitch.
        var codeFont = TokenValue(variables, "code-font-family", "dark").GetString()!;
        if (codeFont != "Source Code Pro")
        {
            throw new DesignDataException($"El token oficial code-font-family cambió: {codeFont}.");
        }

        lines.AddRange(
        [
            "  <FontFamily x:Key=\"source-sans-3-font-family\">" +
            "avares://Transcriptor.UI/Assets/Fonts#Source Sans 3, Segoe UI" +
            "</FontFamily>",
            "  <FontFamily x:Key=\"code-font-family\">" +
            "avares://Transcriptor.UI/Assets/Fonts#JetBrains Mono, Consolas" +
            "</FontFamily>",
            "</ResourceDictionary>",
            "",
        ]);

        return string.Join("\n", lines);
    }

    private static JsonElement TokenValue(IDictionary<string, JsonElement> variables, string name, string variant)
    {
        try
        {
            return ResolveValue(variables[name], variant);
        }
        catch (KeyNotFoundException)
        {
            throw new DesignDataException($"El token oficial no existe: {name}.");
        }
    }

    private static JsonElement ResolveValue(JsonElement token, string variant)
    {
        if (token.ValueKind != JsonValueKind.Object) return token;
        if (token.TryGetProperty("sets", out var sets) && sets.ValueKind == JsonValueKind.Object)
        {
            JsonElement? selected = null;
            if (sets.TryGetProperty("dark", out var dark)) selected = dark;
            else if (sets.TryGetProperty("desktop", out var desktop)) selected = desktop;
            else if (sets.TryGetProperty("default", out var default_)) selected = default_;

            if (selected is { } sel) return ResolveValue(sel, variant);
        }
        if (token.TryGetProperty("value", out var value)) return value;
        throw new DesignDataException($"No se pudo resolver el token para {variant}: {token}.");
    }

    private static string Dimension(JsonElement value)
    {
        var text = value.GetString()!.Trim();
        return text.EndsWith("px", StringComparison.Ordinal) ? text[..^2] : text;
    }

    private static double ParseDimension(JsonElement value) =>
        double.Parse(Dimension(value), CultureInfo.InvariantCulture);

    // Igual que str(float) de Python: "220.0" en lugar de "220".
    private static string FormatDouble(double value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (text.IndexOfAny(['.', 'e', 'E']) < 0) text += ".0";
        return text;
    }

    private static string Color(JsonElement value)
    {
        var text = value.GetString()!.Trim();
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*([\d.]+))?\s*\)");
        if (!match.Success)
        {
            if (text.StartsWith('#')) return text.ToUpperInvariant();
            throw new DesignDataException($"Formato de color no soportado: {text}");
        }

        var red = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var green = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var blue = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        var alpha = match.Groups[4].Success ? double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 1.0;
        if (alpha < 1)
        {
            var alphaByte = (int)Math.Round(alpha * 255);
            return $"#{alphaByte:X2}{red:X2}{green:X2}{blue:X2}";
        }
        return $"#{red:X2}{green:X2}{blue:X2}";
    }
}
