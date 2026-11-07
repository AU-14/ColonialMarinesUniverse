using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Vehicle.VBIED;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Vehicle.VBIED;

/// <summary>
/// A vehicle with an IED rigged inside it. Server only, so a client can't tell a rigged vehicle from a
/// clean one without inspecting it.
/// </summary>
[RegisterComponent]
public sealed partial class CMUVBIEDRiggedComponent : Component
{
    [DataField]
    public EntityUid IED;

    [DataField]
    public CMUVBIEDTrigger Trigger;

    [DataField]
    public EntityUid? Planter;

    /// <summary>
    /// The IFF factions whoever rigged it had at the time; anyone with none of them is hostile.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId<IFFFactionComponent>> Factions = new();

    /// <summary>
    /// The integrity the vehicle has lost since it was rigged.
    /// </summary>
    [DataField]
    public float DamageTaken;

    [DataField]
    public TimeSpan NextProximityCheck;

    /// <summary>
    /// Why the bomb is going off, once something has set it off. It goes off on the next update rather
    /// than in the middle of the crash or blast that set it off.
    /// </summary>
    [DataField]
    public string? Detonation;
}
