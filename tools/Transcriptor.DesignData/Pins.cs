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

    /// <summary>Iconos del sistema Fluent de Microsoft (SVG relleno, rejilla de 20 px, MIT).</summary>
    /// <remarks>Sustituye a @adobe/spectrum-css-workflow-icons para los iconos de la
    /// interfaz: sus trazos rellenos son legibles a 16-20 px sobre fondo oscuro,
    /// mientras que los iconos de flujo de trabajo de Spectrum 2 son de trazo fino
    /// (~1,5 px) y prácticamente ilegibles a esa escala.</remarks>
    public static readonly SpectrumPin SvgIconsFluent = new(
        "@fluentui/svg-icons",
        "1.1.345",
        "https://registry.npmjs.org/@fluentui/svg-icons/-/svg-icons-1.1.345.tgz",
        "Mz5e5BFKtvRN5Yiwrs5JqZRKgd8Rt8S3YiCAuwOWudmm7NA14Y8Ijr8QzMrE6gtNdoUP6/om7ue0lBQJLL57Mw==");

    /// <summary>Iconos de componente de Spectrum (checkbox, chevron, search…).
    /// Se conservan solo para iconos internos de componentes (checkmark, chevron).</summary>
    public static readonly SpectrumPin SpectrumCssUiIcons = new(
        "@spectrum-css/ui-icons",
        "1.1.2",
        "https://registry.npmjs.org/@spectrum-css/ui-icons/-/ui-icons-1.1.2.tgz",
        "rD0EOdabyUJ6pjfvh9eiGlvhk2huhaah+AG8Utzqh/+YgMCn+NQEUz2zxRo9Jva8GXxORLGLDtISafHguOtiOA==");

    public static IEnumerable<SpectrumPin> All =>
        [SpectrumTokens, SvgIconsFluent, SpectrumCssUiIcons];
}

/// <summary>
/// Un archivo CSS de Spectrum 2 fijado por commit y SHA-256.
/// Los hashes se calcularon tras descargar el archivo de
/// raw.githubusercontent.com/adobe/spectrum-css y se verifican en cada descarga.
/// </summary>
public sealed record SpectrumCssFile(string Component, string RelativePath, string Sha256);

public static class SpectrumCssPins
{
    /// <summary>Confirmación de adobe/spectrum-css que representa Spectrum 2.</summary>
    public const string Commit = "70d3660a992a98837d8177d8c25dc5a282e8f361";

    /// <summary>URL base para descargar los archivos CSS de componentes.</summary>
    public const string BaseUrl = "https://raw.githubusercontent.com/adobe/spectrum-css/" + Commit + "/";

    /// <summary>Directorio de caché donde se guardan los CSS descargados.</summary>
    public const string CacheDirectoryName = "spectrum-css@" + Commit;

    /// <summary>Archivos CSS de los componentes a pinchar.</summary>
    public static readonly IReadOnlyList<SpectrumCssFile> Files =
    [
        new("accordion", "components/accordion/index.css", "8f7b9e9b62abab0b245f4c6d3ea9f5ba748bbeed30ea07a026fa2036984a4e68"),
        new("accordion", "components/accordion/themes/spectrum-two.css", "3d16f94663e6fcfb6794254df0338b9fef8a879d5fb761259591bd62e3947bb0"),
        new("actionbutton", "components/actionbutton/index.css", "4642c0ab05283b625cbfc71cd501f1bb2bb8ba57081e6d9da1fae1ad13d6e626"),
        new("actionbutton", "components/actionbutton/themes/spectrum-two.css", "4ebb65b1e439f824b49f94d78bcebb51131ee6f8944df572fd3c4a4e01aa8540"),
        new("actiongroup", "components/actiongroup/index.css", "9d0cec7dc73af464e95775e38c5c0749d80119c4e4a96c2ebd143a92fda0858a"),
        new("actiongroup", "components/actiongroup/themes/spectrum-two.css", "19c72774217eaf1420020bcd7a416a9f954dc6e73b096bac2b88709c58f60ce8"),
        new("alertdialog", "components/alertdialog/index.css", "64e1c82f228a4460c08a242b4575221dac9ee6f9b48eeb04cff8702fd5e2d1c3"),
        new("badge", "components/badge/index.css", "c6b5aad16cbd0b134dd5b4856fd77e015550f63d265ed04e2e6d0895c06c6b40"),
        new("button", "components/button/index.css", "a13c684eaefcd610e1b89526fd7978879bc3f436ca9d1bd68fa3ed0aa2e91187"),
        new("button", "components/button/themes/spectrum-two.css", "3b3b6097cdec9f4382a8497aad291476d5f4e1a5ea21d686439570a9a69c94f0"),
        new("card", "components/card/index.css", "6d4ab49a0daa236fb6cb13970fcb12acedfcce9d11173f702db707bea1f37475"),
        new("card", "components/card/themes/spectrum-two.css", "8285c4a0cca8820019e66b4234177e577607b1db749a474706278a424da08b36"),
        new("checkbox", "components/checkbox/index.css", "77e6c6429047dd9c237c2d74979c25d0738ded1c30fd04689c6a1685aa7b51d5"),
        new("checkbox", "components/checkbox/themes/spectrum-two.css", "e480c3a45c322a8412d50ab761aaff81a6a78af32373a4cbd2076080c35dfc17"),
        new("divider", "components/divider/index.css", "1fee6e3bf06f8be3f33bb98ef3b89f0d98015414087332f8a9f7775749aaa4a6"),
        new("divider", "components/divider/themes/spectrum-two.css", "d45fa9945c4b4c8f222b2b2ecfe2bbbb09508e472ec2aa790ccd78ecacf36d1d"),
        new("inlinealert", "components/inlinealert/index.css", "dbe6945ce5cb1ef915d0dd044be88697a80ffe713e33181dd4b663b0b95c93c1"),
        new("menu", "components/menu/index.css", "246cc0774e2c09647bb1237990d2b69829cc44d23729a3318c0122a5e0a06115"),
        new("menu", "components/menu/themes/spectrum-two.css", "bec96186815c0e6bbe990c58023fc60d2eaf20766a57a59e50565dc9b2defdc1"),
        new("picker", "components/picker/index.css", "409417989a5820fc1642f56fa4cd7705691df8d1ddc332ef1e359212cae253ee"),
        new("picker", "components/picker/themes/spectrum-two.css", "dcba00bc87961200fe948985b5a192a61ad610b71ac609bee62c0b07688ee4a3"),
        new("popover", "components/popover/index.css", "466224632876324fca52089b2971628f9d515cfd49bcd479419297bd0302f572"),
        new("popover", "components/popover/themes/spectrum-two.css", "ea74cd5be4ec5ba492df9aa9b38716eb9e6a6e840dad620a6d1560120f7e4557"),
        new("progressbar", "components/progressbar/index.css", "4b524fd558fb5da8d0f993b27eac542339fd457100a8ccae151166f5d8cd2d3d"),
        new("progressbar", "components/progressbar/themes/spectrum-two.css", "9da12afc4215e942e994c4f86b78dfea0e3933a835d6a58990853c3d3e6e12f1"),
        new("progresscircle", "components/progresscircle/index.css", "83a77282424b8447e4057296e50c845799c4e2e9a408b173a2caf5909acd27a2"),
        new("progresscircle", "components/progresscircle/themes/spectrum-two.css", "43399fef9f9ddf78fd8ba4e78cd8310363cb62b4c2f37749efdacd30af6b4a16"),
        new("radio", "components/radio/index.css", "3fd3d6898eadbbd669a2dffec51820d6f6b39a68e85f76374d4d0d406bee6c76"),
        new("radio", "components/radio/themes/spectrum-two.css", "67db02be728d015ffcc013eeca0a43815d5c7d69d13a7d1e46ad9a4612df09a4"),
        new("search", "components/search/index.css", "abe56db234f883c119061406c012b8e6e3789a252b02f15039b91c0eaa8ecb39"),
        new("search", "components/search/themes/spectrum-two.css", "87958da41b8b00fdb08a60a4558d658ea53add0eaaaa702ec5a5c13e3859b79d"),
        new("slider", "components/slider/index.css", "eaaf30bb2371e9bd09b1d1051c7fe8b274216f8496327dc2cd4d124b0ba697d0"),
        new("slider", "components/slider/themes/spectrum-two.css", "1701960b9e8657b844cbb038730b2f7aa890b9a9fabe5633810c5f76c8f07d39"),
        new("statuslight", "components/statuslight/index.css", "fddc4177878cf7538c3585b1a30e1fbde6457a4272e6bca2130d0e78de85895d"),
        new("stepper", "components/stepper/index.css", "c3bbcf48e4888b10a8f8e00ac720dfecb9c68aa9f00535ada91d8ca675685295"),
        new("stepper", "components/stepper/themes/spectrum-two.css", "8afeb6126c45237d27c0082053d983dc40e0917b13455d09d5fe697796fc3d9e"),
        new("switch", "components/switch/index.css", "40ab00b6fe62f7919fbdd559344fb135b5baccdb93d3ae156344574dcb3a387b"),
        new("switch", "components/switch/themes/spectrum-two.css", "3e19839926e167088e7b008025947cb8b724e94e5274ad103fb8eb5251d7f5ea"),
        new("tabs", "components/tabs/index.css", "b9549677d24b0f140ed6c748612ad0d427592db541f65d494f92caa434ebc4a4"),
        new("tabs", "components/tabs/themes/spectrum-two.css", "320806db442da721f3de048e8ae103bebee1f212759ab1a3f7bf4bc7227da7b3"),
        new("textfield", "components/textfield/index.css", "f2e3e0bbed8f2b25fe68c583a221c501a0a321d1e94dfc065463a224786cd413"),
        new("textfield", "components/textfield/themes/spectrum-two.css", "4488257d66ee533df1fc905e1c2d9d0622138835c8c2fcf54ffe78bc837fca00"),
        new("toast", "components/toast/index.css", "058f8affb4edde45769428ead89c0aca97c8694d37f7eba3c8e917c7c3bffb65"),
        new("toast", "components/toast/themes/spectrum-two.css", "4e58a268b42522fbb642bdc1b80d5bdf4012288694718a37b13010de2f395ddc"),
        new("tooltip", "components/tooltip/index.css", "9ac7cc582eb773782e0cd63823bbb24b3217ded8f2dd20e0385aa105e7ffa215"),
        new("tooltip", "components/tooltip/themes/spectrum-two.css", "444d8e6827215264fe6520474696eab15d428297286609cb5857653a1f68b5a2"),
    ];
}
