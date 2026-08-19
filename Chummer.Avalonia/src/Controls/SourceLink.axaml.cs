using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;

namespace Chummer.NewUI.Controls;

public partial class SourceLink : UserControl
{
    public static readonly StyledProperty<string?> SourcePageProperty =
        AvaloniaProperty.Register<SourceLink, string?>(nameof(SourcePage));

    public static readonly DirectProperty<SourceLink, bool> CanOpenProperty =
        AvaloniaProperty.RegisterDirect<SourceLink, bool>(nameof(CanOpen), o => o.CanOpen);

    private bool _blnCanOpen;

    /// <summary>"&lt;book code&gt; &lt;page&gt;" (e.g. "SR4 118") - the same shape every
    /// Source/Page pair in this port is already displayed as.</summary>
    public string? SourcePage
    {
        get => GetValue(SourcePageProperty);
        set => SetValue(SourcePageProperty, value);
    }

    /// <summary>Recomputed whenever SourcePage changes: whether a PDF reader and this book's path
    /// are both configured (Optionen), so the view can show a clickable link vs. plain text.</summary>
    public bool CanOpen
    {
        get => _blnCanOpen;
        private set => SetAndRaise(CanOpenProperty, ref _blnCanOpen, value);
    }

    public SourceLink()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == SourcePageProperty)
                RefreshCanOpen();
        };
        RefreshCanOpen();
    }

    private void RefreshCanOpen() => CanOpen = PdfLinkService.CanOpen && !string.IsNullOrWhiteSpace(SourcePage);

    private void OnClick(object? sender, RoutedEventArgs e) => PdfLinkService.OpenPdf(SourcePage ?? string.Empty);
}
