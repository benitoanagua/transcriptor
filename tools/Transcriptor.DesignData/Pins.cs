namespace Transcriptor.DesignData;

/// <summary>
/// Pin de un paquete de datos de diseño (Spectrum 2) fijado por versión y SHA-512.
/// Los hashes se obtuvieron del registro de npm (campo dist.integrity de cada paquete)
/// y se verifican en cada descarga para que el tooling sea reproducible y sin sorpresas.
/// </summary>
public sealed record SpectrumPin(string Name, string Version, string Url, string Sha512Base64)
{
    public string CacheDirectoryName => $"{Name}@{Version}";
}

public static class Pins
{
    /// <summary>Tokens de diseño de Spectrum 2 (colores, espaciados, radios, tipografía…).</summary>
    public static readonly SpectrumPin SpectrumTokens = new(
        "@adobe/spectrum-tokens",
        "15.6.0",
        "https://registry.npmjs.org/@adobe/spectrum-tokens/-/spectrum-tokens-15.6.0.tgz",
        "eMciiuA/2yt0dKLko2LI1QLktb9RohlckXR4brCTIxX/l5T2jfxP5DjuFEAqJUTxRnEVVrbkMtVWnyZdl7J5Ow==");

    /// <summary>Iconos de flujo de trabajo de Spectrum 2 (SVG de 20 px).</summary>
    public static readonly SpectrumPin SpectrumCssWorkflowIcons = new(
        "@adobe/spectrum-css-workflow-icons",
        "5.0.0",
        "https://registry.npmjs.org/@adobe/spectrum-css-workflow-icons/-/spectrum-css-workflow-icons-5.0.0.tgz",
        "C6N97hnwG/ZqAPsMsrxakU8F/PH4w5EUdadQermIbmCHll1ZJYdG8BL1UGu5jMTunsZkZrDgLIf1XnAnxJQhgA==");

    /// <summary>Iconos de componente de Spectrum (checkbox, chevron, search…).</summary>
    public static readonly SpectrumPin SpectrumCssUiIcons = new(
        "@spectrum-css/ui-icons",
        "1.1.2",
        "https://registry.npmjs.org/@spectrum-css/ui-icons/-/ui-icons-1.1.2.tgz",
        "rD0EOdabyUJ6pjfvh9eiGlvhk2huhaah+AG8Utzqh/+YgMCn+NQEUz2zxRo9Jva8GXxORLGLDtISafHguOtiOA==");

    public static IEnumerable<SpectrumPin> All =>
        [SpectrumTokens, SpectrumCssWorkflowIcons, SpectrumCssUiIcons];
}
