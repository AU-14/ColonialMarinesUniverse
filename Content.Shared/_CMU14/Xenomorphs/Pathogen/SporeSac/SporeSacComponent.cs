using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.SporeSac;

/// <summary>
/// Lets the Popper place a Spore Sac structure at a target tile.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUXenoSporeSacSystem))]
public sealed partial class CMUXenoSporeSacComponent : Component
{
    /// <summary>Entity prototype to spawn as the sac.</summary>
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId SacPrototype = "CMUPathogenSporeSac";

    /// <summary>How many sacs this xeno can have placed at once.</summary>
    [DataField, AutoNetworkedField]
    public int MaxSacs = 3;

    /// <summary>Plasma cost per placement.</summary>
    [DataField, AutoNetworkedField]
    public float PlasmaCost = 200f;

    /// <summary>Max range from the xeno to the target tile.</summary>
    [DataField, AutoNetworkedField]
    public float Range = 5f;

    [DataField, AutoNetworkedField]
    public TimeSpan PlaceDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>Track placed sacs so we can enforce MaxSacs.</summary>
    [ViewVariables]
    public List<EntityUid> PlacedSacs = new();
}