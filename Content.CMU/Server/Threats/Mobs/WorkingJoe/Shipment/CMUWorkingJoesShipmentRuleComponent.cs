using Content.Shared.CMU14.Threats;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Threats.Mobs.WorkingJoe.Shipment;

/// <summary>
/// The Working Joes shipment's deploy rule, started from the threat's <c>addgamerules</c>. When its threat deploys
/// it promotes the leader (and enough members) to Early Warning Joes and gives every Early Warning Joe the
/// conversion Offer. Tuning lives here so <see cref="ThreatPrototype"/> stays generic.
/// </summary>
[RegisterComponent, Access(typeof(WorkingJoesShipmentRuleSystem))]
public sealed partial class CMUWorkingJoesShipmentRuleComponent : Component
{
    /// <summary>
    /// How many Early Warning Joes the round should have once the shipment lands. Rolled Early Warning Joes
    /// count toward it; the shipment always brings at least one, its leader.
    /// </summary>
    // explicit tags: the default camel-casing would read eWJoeTarget
    [DataField("ewJoeTarget")]
    public int EWJoeTarget = 2;

    /// <summary>The level a promoted Early Warning Joe starts at. Designed for at most 2.</summary>
    [DataField("ewJoeStartingLevel")]
    public int EWJoeStartingLevel = 2;

    /// <summary>The threat this rule reacts to; other threats' deployments are ignored.</summary>
    [DataField]
    public ProtoId<ThreatPrototype> ThreatId = "CMUThreatWorkingJoesShipment";
}
