using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.GearFlavor;

/// <summary>
///     Text its owner wrote on this armor or helmet, shown when someone examines the person wearing it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUGearFlavorTextComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Text = string.Empty;
}

/// <summary>
///     On a GOVFOR/OPFOR character: the armor and helmet text from their profile, written onto that gear
///     when they draw it from their vendor.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGearFlavorOwnerComponent : Component
{
    [DataField]
    public string Armor = string.Empty;

    [DataField]
    public string Helmet = string.Empty;
}
