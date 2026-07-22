using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>One editable row of the Allgemein tab's Connections/Feinde lists.</summary>
public sealed class ContactRowViewModel : ViewModelBase
{
    private readonly CharacterDocument _character;

    public ContactRowViewModel(CharacterDocument character, CharacterContactData contact)
    {
        _character = character;
        ContactId = contact.ContactId;
        IsEnemy = contact.IsEnemy;
        _strName = contact.Name;
        _strConnection = contact.Connection;
        _strLoyalty = contact.Loyalty;
    }

    public int ContactId { get; }
    public bool IsEnemy { get; }

    private string _strName = string.Empty;
    public string Name
    {
        get => _strName;
        set
        {
            if (!SetField(ref _strName, value))
                return;
            _character.UpdateContact(ContactId, value, Connection, Loyalty);
        }
    }

    private string _strConnection = "0";
    public string Connection
    {
        get => _strConnection;
        set
        {
            if (!SetField(ref _strConnection, value))
                return;
            _character.UpdateContact(ContactId, Name, value, Loyalty);
        }
    }

    private string _strLoyalty = "0";
    public string Loyalty
    {
        get => _strLoyalty;
        set
        {
            if (!SetField(ref _strLoyalty, value))
                return;
            _character.UpdateContact(ContactId, Name, Connection, value);
        }
    }
}
