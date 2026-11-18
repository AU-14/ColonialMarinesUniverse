// CMU14
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// ARCH-11: a structure the Early Warning Joe Offered to a Meld directive (a camera, a telecomms server). It is
/// melded into the river rather than consumed: it stays, lit orange and switched off. The marker is also what
/// keeps it from being offered, and counted, a second time. Added by <c>EWJoeMeldedStructureSystem.Meld</c>.
/// RB-30: a lit welder burns the meld back out (a 10 s do-after): the marker goes, the light it had before
/// returns, a camera watches again and a server is a server again. A repaired structure can be melded anew.
/// Networked for presence only, like <see cref="EWJoeAssimilatedConsoleComponent"/>: <see cref="MeldedBy"/>
/// and the restore data stay server-side because <see cref="EWJoeComponent"/> is SendOnlyToOwner, and the
/// antag's identity must not leak through the structures it touched.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EWJoeMeldedStructureComponent : Component
{
    /// <summary>The Joe that melded this structure, if known. Server only (see the class note).</summary>
    [DataField]
    public EntityUid? MeldedBy;

    /// <summary>RB-30: the structure was a telecomms server, so a repair makes it one again. Server only.</summary>
    [DataField]
    public bool WasTelecomServer;

    /// <summary>RB-30: the structure carried a light before the meld; without one, a repair removes the river's. Server only.</summary>
    [DataField]
    public bool HadLight;

    /// <summary>RB-30: the light as it was before the meld, restored by a repair. Server only.</summary>
    [DataField]
    public Color LightColor = Color.White;

    [DataField]
    public float LightRadius;

    [DataField]
    public float LightEnergy;

    [DataField]
    public bool LightEnabled;
}

/// <summary>RB-30: a welder burning the river's meld out of a structure.</summary>
[Serializable, NetSerializable]
public sealed partial class EWJoeMeldRepairDoAfterEvent : SimpleDoAfterEvent;
