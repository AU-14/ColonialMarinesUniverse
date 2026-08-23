using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Yautja;

public enum YautjaMilitaryCaste : byte
{
    Soldier,
    Enforcer,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaMilitaryCasteComponent : Component
{
    [DataField, AutoNetworkedField]
    public YautjaMilitaryCaste Caste = YautjaMilitaryCaste.Soldier;

    [DataField, AutoNetworkedField]
    public bool WhitelistIcon;
}
