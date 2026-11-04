using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Kitchen;

/// <summary>
///     A rack of mugs (ported from CM-SS13). Take one with an empty hand; put a mug back by using it on the rack. Keeps a
///     count rather than real mugs, and shows roughly how full it is.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUMugRackComponent : Component
{
    /// <summary>What the rack hands out, and the only thing it takes back.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId Mug = "RMCCoffeeCup";

    [DataField, AutoNetworkedField]
    public int Amount = 50;

    /// <summary>A full rack; the fill levels are thirds of this.</summary>
    [DataField, AutoNetworkedField]
    public int Capacity = 50;
}

[Serializable, NetSerializable]
public enum CMUMugRackVisuals : byte
{
    Fill,
}

/// <summary>How full the rack looks: empty, then the three mug overlays from the fullest down.</summary>
[Serializable, NetSerializable]
public enum CMUMugRackFill : byte
{
    Empty,
    Low,
    Half,
    Full,
}
