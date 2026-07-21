using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Chummer.NewUI.ViewModels;

/// <summary>Minimal INotifyPropertyChanged base for the Avalonia views' ViewModels - hand-rolled
/// rather than pulling in a toolkit, since a handful of bindable properties per view doesn't need
/// source-generated boilerplate. Views bind to these instead of code-behind reaching into the
/// visual tree with FindControl.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
