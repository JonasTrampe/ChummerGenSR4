using Avalonia;
using Avalonia.Controls;

namespace Chummer.NewUI.Controls;

/// <summary>
///     Common skeleton shared by every "tree + detail" tab in the app: a header row (action
///     buttons) docked above a two-column body - a tree/list on the left, freeform detail content
///     on the right, with a manually-draggable <see cref="GridSplitter" /> between them. Every one
///     of these tabs used to hand-roll the same DockPanel/Grid/GridSplitter nesting; extracting it
///     here means that structure only has to be gotten right once.
/// </summary>
public partial class TreeDetailPane : UserControl
{
    public static readonly StyledProperty<object?> HeaderContentProperty =
        AvaloniaProperty.Register<TreeDetailPane, object?>(nameof(HeaderContent));

    public static readonly StyledProperty<object?> TreeContentProperty =
        AvaloniaProperty.Register<TreeDetailPane, object?>(nameof(TreeContent));

    public static readonly StyledProperty<object?> DetailContentProperty =
        AvaloniaProperty.Register<TreeDetailPane, object?>(nameof(DetailContent));

    // GridLength, not double: ColumnDefinition.Width is a GridLength, and a {Binding} carrying a
    // double into it needs a runtime type conversion that Avalonia's binding engine doesn't apply
    // the way its XAML-literal-attribute parser does - the assignment silently no-ops, leaving
    // ColumnDefinition.Width at its CLR default of GridLength(1, Star), which splits 50/50 with
    // the equally-star-sized detail column no matter what TreeWidth is set to.
    public static readonly StyledProperty<GridLength> TreeWidthProperty =
        AvaloniaProperty.Register<TreeDetailPane, GridLength>(nameof(TreeWidth), new GridLength(260));

    public static readonly StyledProperty<Thickness> ContentMarginProperty =
        AvaloniaProperty.Register<TreeDetailPane, Thickness>(nameof(ContentMargin), new Thickness(4));

    public TreeDetailPane()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public object? TreeContent
    {
        get => GetValue(TreeContentProperty);
        set => SetValue(TreeContentProperty, value);
    }

    public object? DetailContent
    {
        get => GetValue(DetailContentProperty);
        set => SetValue(DetailContentProperty, value);
    }

    public GridLength TreeWidth
    {
        get => GetValue(TreeWidthProperty);
        set => SetValue(TreeWidthProperty, value);
    }

    public Thickness ContentMargin
    {
        get => GetValue(ContentMarginProperty);
        set => SetValue(ContentMarginProperty, value);
    }
}
