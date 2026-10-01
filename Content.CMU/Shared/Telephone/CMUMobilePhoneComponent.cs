using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Telephone;

/// <summary>
/// A handheld phone that takes the name of whoever first picks it up, renameable with a screwdriver.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUMobilePhoneComponent : Component
{
    [DataField]
    public string? OwnerName;

    [DataField]
    public int MaxNameLength = 32;
}
