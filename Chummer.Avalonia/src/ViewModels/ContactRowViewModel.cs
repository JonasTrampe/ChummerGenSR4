using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>One row of the Allgemein tab's Connections/Feinde lists.</summary>
public sealed class ContactRowViewModel
{
    public string Name { get; }
    public string Connection { get; }
    public string Loyalty { get; }

    public ContactRowViewModel(CharacterContactData contact)
    {
        Name = contact.Name;
        Connection = contact.Connection;
        Loyalty = contact.Loyalty;
    }
}
