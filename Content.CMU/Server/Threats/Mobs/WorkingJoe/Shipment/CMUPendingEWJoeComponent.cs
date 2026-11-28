namespace Content.Server.CMU14.Threats.Mobs.WorkingJoe.Shipment;

/// <summary>
/// On an unclaimed shipment leader body: promote it to an Early Warning Joe when a mind arrives.
/// </summary>
[RegisterComponent, Access(typeof(WorkingJoesShipmentRuleSystem))]
public sealed partial class CMUPendingEWJoeComponent : Component
{
    /// <summary>The level the Early Warning Joe starts at, copied from the shipment rule.</summary>
    [DataField]
    public int StartingLevel = 2;
}
