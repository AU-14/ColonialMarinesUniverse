namespace Content.Shared.CMU14.Threats;

/// <summary>
/// Marks a structure a threat builds or grows: xeno resin walls, membranes, doors, eggs and hive structures,
/// pathogen structures and mycelium, biomorph flesh nests and walls. Tagged on the parent prototypes so
/// every child inherits it. The Early Warning Joe's "Purge the Nest" directive counts these, weighted:
/// cores are worth more, and spreading growth (kudzu) is worth nothing, like weeds, which are not tagged.
/// </summary>
[RegisterComponent]
public sealed partial class CMUThreatStructureComponent : Component
{
    /// <summary>How much destroying this structure is worth. Zero means it never counts.</summary>
    [DataField]
    public int Weight = 1;
}
