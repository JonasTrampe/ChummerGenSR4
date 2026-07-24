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
        _intConnection = ParseInt(contact.Connection);
        _intLoyalty = ParseInt(contact.Loyalty);
        _strNotes = contact.Notes;
        _blnFree = contact.Free;
        GroupName = contact.GroupName;
        Membership = contact.Membership;
        AreaOfInfluence = contact.AreaOfInfluence;
        MagicalResources = contact.MagicalResources;
        MatrixResources = contact.MatrixResources;
        GroupRating = contact.GroupRating;
    }

    public string GroupName { get; private set; }
    public int Membership { get; private set; }
    public int AreaOfInfluence { get; private set; }
    public int MagicalResources { get; private set; }
    public int MatrixResources { get; private set; }

    private int _intGroupRating;
    /// <summary>Sum of the four Group modifiers - adds to Connection+Loyalty in the Karma/BP
    /// cost formula.</summary>
    public int GroupRating
    {
        get => _intGroupRating;
        private set => SetField(ref _intGroupRating, value);
    }

    public void UpdateGroup(string strGroupName, int intMembership, int intAreaOfInfluence,
        int intMagicalResources, int intMatrixResources)
    {
        GroupName = strGroupName;
        Membership = intMembership;
        AreaOfInfluence = intAreaOfInfluence;
        MagicalResources = intMagicalResources;
        MatrixResources = intMatrixResources;
        GroupRating = intMembership + intAreaOfInfluence + intMagicalResources + intMatrixResources;
        _character.UpdateContactGroup(ContactId, strGroupName, intMembership, intAreaOfInfluence,
            intMagicalResources, intMatrixResources);
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
            _character.UpdateContact(ContactId, value, Connection.ToString(), Loyalty.ToString());
        }
    }

    private int _intConnection;
    public int Connection
    {
        get => _intConnection;
        set
        {
            if (!SetField(ref _intConnection, value))
                return;
            _character.UpdateContact(ContactId, Name, value.ToString(), Loyalty.ToString());
        }
    }

    private int _intLoyalty;
    public int Loyalty
    {
        get => _intLoyalty;
        set
        {
            if (!SetField(ref _intLoyalty, value))
                return;
            _character.UpdateContact(ContactId, Name, Connection.ToString(), value.ToString());
        }
    }

    private static int ParseInt(string strValue) => int.TryParse(strValue, out var intValue) ? intValue : 0;

    private string _strNotes = string.Empty;
    public string Notes
    {
        get => _strNotes;
        set
        {
            if (!SetField(ref _strNotes, value))
                return;
            _character.UpdateContactNotes(ContactId, value);
        }
    }

    private bool _blnFree;
    /// <summary>Doesn't cost Karma/BP - matches the legacy "Free" checkbox.</summary>
    public bool Free
    {
        get => _blnFree;
        set
        {
            if (!SetField(ref _blnFree, value))
                return;
            _character.SetContactFree(ContactId, value);
        }
    }
}
