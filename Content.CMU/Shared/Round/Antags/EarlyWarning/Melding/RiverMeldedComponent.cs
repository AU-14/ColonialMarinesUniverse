using Content.Shared.Alert;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;

/// <summary>
/// A River-Melded thrall of the Early Warning Joe (phase E3). Deliberately its own marker rather than
/// the CLF subversion one (COUPLING-01): the Seegson reset key every Working Joe carries reverses a
/// subversion and must not touch this. Loyalty is role-level; the body keeps its NPC faction (H-5).
/// Server only.
/// </summary>
[RegisterComponent]
public sealed partial class RiverMeldedComponent : Component
{
    /// <summary>The Joe that made this thrall. Null once the master is gone and the thrall is being freed.</summary>
    [DataField]
    public EntityUid? Master;

    /// <summary>Playtest 4: the thrall's own reminder of what it is now, cleared on cure.</summary>
    [DataField]
    public ProtoId<AlertPrototype> Alert = "CMUEWJoeRiverMelded";

    /// <summary>The conversion added the Grand Machine marker, so the cure takes it away again.</summary>
    [DataField]
    public bool AddedTouched;

    /// <summary>
    /// Radio channel granted at conversion and revoked on cure. The cure removes only the channel;
    /// the radio components go only when nothing else uses them (headsets grant onto the same ones).
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype>? GrantedChannel;

    /// <summary>How many of the warnings the victim received before conversion; for admins (View Variables) and tests.</summary>
    [DataField]
    public int WarningsReceived;
}
