using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Transcriptor.Dev;

public partial class SpectrumGalleryWindow : Window
{
    public SpectrumGalleryWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
