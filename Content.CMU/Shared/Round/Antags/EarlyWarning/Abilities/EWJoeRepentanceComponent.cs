using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Repentance (phase E4, balance A-8): the Joe remembers whoever last hurt it and is shown a tracker
/// arrow toward them; the Sense action tells direction and distance. Granted by the level table at
/// level 2; its state lives here so the Joe's own component keeps its directed slots free.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeRepentanceComponent : Component
{
    [DataField]
    public EntityUid? LastAttacker;

    /// <summary>When the memory of the attacker fades.</summary>
    [DataField]
    public TimeSpan Until;

    [DataField]
    public TimeSpan NextUpdate;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "CMUEWJoeRepentance";

    [DataField]
    public bool AlertShown;
}
