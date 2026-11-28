namespace Content.Shared.CMU14.Threats.Rules;

/// <summary>
/// The colony's win condition against a Working Joes shipment: the round ends once <see cref="Percent"/> of the
/// threat units are dead, deleted or off the planet. A cured unit leaves the count.
/// </summary>
[RegisterComponent]
public sealed partial class CMUKillAllHijackedJoesRuleComponent : Component
{
    /// <summary>Percentage of threat units that must be eliminated (1-100).</summary>
    [DataField]
    public int Percent = 100;

    /// <summary>Every threat unit seen while the rule ran; deleted ones stay in it as eliminated.</summary>
    [ViewVariables]
    public HashSet<EntityUid> Units = new();

    /// <summary>Whether any unit was ever counted, so a shipment cured to the last Joe still ends the round.</summary>
    [ViewVariables]
    public bool HadUnits;
}
