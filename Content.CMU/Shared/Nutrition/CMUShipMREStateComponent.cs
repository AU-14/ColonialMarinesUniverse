using Content.Shared.CMU14.Kitchen;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Nutrition;

/// <summary>
/// Replicated per-map facts the ship MRE gate decides from. Lives on the map entity, which every client sees, so
/// neither side has to query world entities that PVS may not have sent the other.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUShipMRESystem), typeof(CMUFoodlineSystem))]
public sealed partial class CMUShipMREStateComponent : Component
{
    /// <summary>A hijacked dropship has crashed: the mess rules no longer apply for the rest of the round.</summary>
    [DataField, AutoNetworkedField]
    public bool HijackLockout;

    /// <summary>Whether the map has any (non-terminating) kitchen shutters at all.</summary>
    [DataField, AutoNetworkedField]
    public bool HasFoodline;

    /// <summary>Whether any kitchen shutter on the map is open. Only meaningful while <see cref="HasFoodline"/> holds.</summary>
    [DataField, AutoNetworkedField]
    public bool FoodlineOpen;
}
