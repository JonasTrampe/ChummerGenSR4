using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CommlinkItemViewModel
{
    internal CommlinkItemViewModel(CharacterCommlinkData objData)
    {
        Guid = objData.Guid;
        Name = objData.Name;
        Response = objData.Response;
        Equipped = objData.Equipped;
        Active = objData.Active;
    }

    public string Guid { get; }
    public string Name { get; }
    public int Response { get; }
    public bool Equipped { get; }
    public bool Active { get; }
    public string DisplayName => Equipped ? Name + " (R " + Response + ")" : Name + " (nicht ausgerüstet)";
}
