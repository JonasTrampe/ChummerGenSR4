using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;

namespace Chummer.NewUI.Controls;

/// <summary>
///     One Aktionsfertigkeiten/Wissensfertigkeiten row. A skill can be in exactly one of six
///     visual states, driven by the flags below rather than separate control types, since the
///     column layout is identical across all of them - only color/interactivity differs:
///     <list type="bullet">
///         <item>Normal, rating 0, not defaultable (IsUnavailable) - fully dimmed, no pool shown.</item>
///         <item>Normal, rating 0, defaultable (neither flag set, Rating=="0") - dimmed text but
///             still shows the attribute-only default pool.</item>
///         <item>Normal, rating &gt; 0, no specialization - full color, pool shown.</item>
///         <item>Normal, rating &gt; 0, with specialization (Specialization set) - full color;
///             the specialization combo shows the chosen value as its content.</item>
///         <item>In a skill group (IsGroupLocked) - rating follows the group, so the row's own
///             rating/pool controls are locked instead of hidden.</item>
///         <item>Metaskill subskill with no specialization column at all (IsMetaskill) - e.g. the
///             individual Wahrnehmung senses.</item>
///     </list>
///     The specialization combo itself is always present (never swapped for plain text) and
///     always disabled - changing a specialization is a special-occasion action handled
///     elsewhere (via karma expenditure), not something this row edits directly.
///     CanEdit*/HasPool are plain computed getters, not bindable AvaloniaProperties, but they
///     still need INotifyPropertyChanged: XAML sets Pool, IsGroupLocked etc. on the SkillRow
///     instance *after* the constructor (and its InitializeComponent call) has already built the
///     child bindings, so without an explicit notification here the computed values would be
///     stuck evaluating against their not-yet-set defaults forever - the static handlers below
///     re-raise the derived property whenever something it depends on changes.
/// </summary>
public partial class SkillRow : UserControl, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    static SkillRow()
    {
        PoolProperty.Changed.AddClassHandler<SkillRow>((r, _) => r.RaisePropertyChanged(nameof(HasPool)));
        IsUnavailableProperty.Changed.AddClassHandler<SkillRow>((r, _) => r.RaisePropertyChanged(nameof(CanEditRating)));
        IsGroupLockedProperty.Changed.AddClassHandler<SkillRow>((r, _) => r.RaisePropertyChanged(nameof(CanEditRating)));
    }

    private void RaisePropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    public static readonly StyledProperty<string?> SkillNameProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(SkillName));

    public static readonly StyledProperty<string?> AttributeProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(Attribute));

    public static readonly StyledProperty<string?> RatingProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(Rating));

    public static readonly StyledProperty<string?> PoolProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(Pool));

    public static readonly StyledProperty<string?> PoolTooltipProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(PoolTooltip));

    public static readonly StyledProperty<string?> SpecializationProperty =
        AvaloniaProperty.Register<SkillRow, string?>(nameof(Specialization));

    /// <summary>Not available to this character at all (e.g. a Magic skill on a non-Awakened
    /// character) - fully dimmed, no pool, no interactive controls.</summary>
    public static readonly StyledProperty<bool> IsUnavailableProperty =
        AvaloniaProperty.Register<SkillRow, bool>(nameof(IsUnavailable));

    /// <summary>Rating is 0 and the skill cannot be defaulted off its attribute either - distinct
    /// from IsUnavailable in that it's still a real, ownable skill, just currently useless.</summary>
    public static readonly StyledProperty<bool> IsNotDefaultableProperty =
        AvaloniaProperty.Register<SkillRow, bool>(nameof(IsNotDefaultable));

    /// <summary>Rating is locked because it belongs to an active skill group - the row still shows
    /// a rating/pool, but the +/specialization controls are locked out.</summary>
    public static readonly StyledProperty<bool> IsGroupLockedProperty =
        AvaloniaProperty.Register<SkillRow, bool>(nameof(IsGroupLocked));

    /// <summary>Metaskill subskill (e.g. individual Wahrnehmung senses) that never takes a
    /// specialization - hides that column entirely rather than leaving it empty.</summary>
    public static readonly StyledProperty<bool> IsMetaskillProperty =
        AvaloniaProperty.Register<SkillRow, bool>(nameof(IsMetaskill));

    public SkillRow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public string? SkillName
    {
        get => GetValue(SkillNameProperty);
        set => SetValue(SkillNameProperty, value);
    }

    public string? Attribute
    {
        get => GetValue(AttributeProperty);
        set => SetValue(AttributeProperty, value);
    }

    public string? Rating
    {
        get => GetValue(RatingProperty);
        set => SetValue(RatingProperty, value);
    }

    public string? Pool
    {
        get => GetValue(PoolProperty);
        set => SetValue(PoolProperty, value);
    }

    public string? PoolTooltip
    {
        get => GetValue(PoolTooltipProperty);
        set => SetValue(PoolTooltipProperty, value);
    }

    public string? Specialization
    {
        get => GetValue(SpecializationProperty);
        set => SetValue(SpecializationProperty, value);
    }

    public bool IsUnavailable
    {
        get => GetValue(IsUnavailableProperty);
        set => SetValue(IsUnavailableProperty, value);
    }

    public bool IsNotDefaultable
    {
        get => GetValue(IsNotDefaultableProperty);
        set => SetValue(IsNotDefaultableProperty, value);
    }

    public bool IsGroupLocked
    {
        get => GetValue(IsGroupLockedProperty);
        set => SetValue(IsGroupLockedProperty, value);
    }

    public bool IsMetaskill
    {
        get => GetValue(IsMetaskillProperty);
        set => SetValue(IsMetaskillProperty, value);
    }

    public bool HasPool => !string.IsNullOrWhiteSpace(Pool) && Pool.Trim() != "0";
    public bool CanEditRating => !IsUnavailable && !IsGroupLocked;
}
