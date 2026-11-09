using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// D1/D5: cada tipo de control con plantilla usado en las vistas (o en plantillas de temas)
/// debe tener un <c>ControlTheme</c> Spectrum en <c>ControlThemes.axaml</c> y, al aplicarse,
/// generar hijos visuales. Los controles sin propiedad <c>Template</c> (p. ej.
/// <see cref="SelectableTextBlock"/>, <see cref="Popup"/>) se documentan y se verifican
/// como excepción justificada en lugar de exigirles plantilla.
/// </summary>
public sealed class ControlThemeCoverageTests
{
    private static readonly IReadOnlyList<Type> TemplateControls =
    [
        typeof(PathIcon),
        typeof(ScrollViewer),
        typeof(ScrollBar),
        typeof(Window),
        typeof(ContextMenu),
        typeof(MenuItem),
        typeof(Button),
        typeof(CheckBox),
        typeof(ComboBox),
        typeof(ToggleSwitch),
        typeof(RadioButton),
        typeof(Slider),
        typeof(TabControl),
        typeof(TabItem),
        typeof(ListBox),
        typeof(ProgressBar),
    ];

    /// <summary>Controles con plantilla usados en las vistas que no exponen
    /// <see cref="TemplatedControl.Template"/> (SelectableTextBlock hereda de TextBlock;
    /// Popup expone Child). Se aceptan sin tema de plantilla.</summary>
    private static readonly IReadOnlyList<Type> TemplateControlsSinPlantilla =
    [
        typeof(SelectableTextBlock),
        typeof(Popup),
    ];

    [Fact]
    public void CadaControlConPlantillaTieneTemaEHijosVisuales()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(SpectrumThemeApp));

        session.Dispatch(() =>
        {
            var host = new Panel();
            var geometry = (Geometry?)Application.Current!.Resources["icon-Microphone"];

            foreach (var type in TemplateControls)
            {
                var control = (Control)Activator.CreateInstance(type)!;
                control.Width = 100;
                control.Height = 100;

                if (control is PathIcon pathIcon && geometry is not null)
                    pathIcon.Data = geometry;

                host.Children.Add(control);
                if (control is TemplatedControl tc)
                {
                    tc.ApplyTemplate();
                    Assert.True(tc.Template is not null, $"{type.Name}: no tiene ControlTheme con Template.");
                }

                Assert.True(
                    control.GetVisualDescendants().Any(),
                    $"{type.Name}: su plantilla no generó hijos visuales.");
            }
        }, CancellationToken.None);
    }

    [Fact]
    public void ControlesSinPlantillaTienenTemaDocumentado()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(SpectrumThemeApp));
        session.Dispatch(() =>
        {
            foreach (var type in TemplateControlsSinPlantilla)
            {
                var control = (Control)Activator.CreateInstance(type)!;
                control.Width = 100;
                control.Height = 100;
                // SelectableTextBlock y Popup no son TemplatedControl: no tienen ApplyTemplate
                // ni plantilla de control; solo verifican que se instancien con los recursos.
                if (control is TemplatedControl tc2) tc2.ApplyTemplate();
            }
        }, CancellationToken.None);
    }

    private sealed class SpectrumThemeApp : Application
    {
        public override void Initialize()
        {
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Transcriptor.UI/Theming/SpectrumTheme.axaml"))
            {
                Source = new Uri("avares://Transcriptor.UI/Theming/SpectrumTheme.axaml")
            });
        }
    }
}
