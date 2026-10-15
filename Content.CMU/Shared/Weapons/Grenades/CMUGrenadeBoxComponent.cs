using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Weapons.Grenades;

/// <summary>
/// Deployable grenade box, ported from cmss13-devs/cmss13-pve#854.
/// Only holds one kind of grenade, and cooks off if it sits in a fire with enough grenades inside.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUGrenadeBoxComponent : Component
{
    /// <summary>The only grenade prototype this box accepts. Null accepts anything the slot whitelist allows.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? Grenade;

    /// <summary>Fewer grenades than this and a fire won't set the box off.</summary>
    [DataField, AutoNetworkedField]
    public int CookOffMinimum = 17;

    /// <summary>How long the box burns before it goes up.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CookOffDelay = TimeSpan.FromSeconds(8);

    /// <summary>Explosion intensity added per grenade inside. Zero disables cooking off.</summary>
    [DataField, AutoNetworkedField]
    public float CookOffIntensityPerGrenade = 12;

    [DataField, AutoNetworkedField]
    public float CookOffSlope = 8;

    [DataField, AutoNetworkedField]
    public float CookOffMaxIntensity = 30;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? CookOffAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextFireCheck;

    [DataField]
    public TimeSpan FireCheckInterval = TimeSpan.FromSeconds(1);
}

[Serializable, NetSerializable]
public enum CMUGrenadeBoxVisuals
{
    CookingOff,
}
