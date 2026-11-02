using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja;

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

    /// <summary>
    /// Set once the first player takes the body, so a re-entering ghost doesn't get a new name or briefing.
    /// </summary>
    [DataField]
    public bool SetupComplete;
}
