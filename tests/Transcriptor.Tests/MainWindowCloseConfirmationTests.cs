using System.Reflection;
using Transcriptor.UI;
using Xunit;

namespace Transcriptor.Tests;

/// <summary>
/// Verifica que <see cref="MainWindow.SkipCloseConfirmation"/> existe y es
/// de tipo <see cref="bool"/>. Usado por --capture-ui-reference en modo
/// headless para evitar que el Popup de confirmación de cierre falle al no
/// haber overlay layer. El valor por defecto de un auto-property bool en C#
/// es false, por lo que la app de producción y el modo normal de desktop-dev
/// no cambian su comportamiento.
/// </summary>
public sealed class MainWindowCloseConfirmationTests
{
    [Fact]
    public void SkipCloseConfirmationPropertyExistsAndIsBoolean()
    {
        var prop = typeof(MainWindow).GetProperty(
            "SkipCloseConfirmation",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(prop);
        Assert.True(prop.CanRead);
        Assert.True(prop.CanWrite);
        Assert.Equal(typeof(bool), prop.PropertyType);
    }
}
