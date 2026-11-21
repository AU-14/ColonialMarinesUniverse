using Content.Shared.FixedPoint;

namespace Content.Shared.CMU14.Specimens;

/// <summary>
///     A tranquiliser shell, or a handful of them, that holds a dose of chemicals for each shell. The
///     handful's solution is shared out with the shells when it's split, joined when handfuls merge, and
///     moved into the slug when a shell is fired.
/// </summary>
[RegisterComponent]
public sealed partial class CMUTranqShellComponent : Component
{
    [DataField]
    public string Solution = "tranq";

    /// <summary>
    ///     The most each shell in the handful can hold.
    /// </summary>
    [DataField]
    public FixedPoint2 PerShell = FixedPoint2.New(5);

    /// <summary>
    ///     The share of the solution that left with shells taken off the handful, handed to the new
    ///     handful when the split finishes.
    /// </summary>
    [ViewVariables]
    public Content.Shared.Chemistry.Components.Solution? Leaving;

    /// <summary>
    ///     This handful's shells are going into another handful, whose merge already took their share.
    /// </summary>
    [ViewVariables]
    public bool MergingAway;
}

/// <summary>
///     A tranquiliser slug in flight. On a hit it injects what it carries. Creatures without a
///     bloodstream that the capture kit works on (xenonids, wendigos, biomorphs) are sedated by the
///     Somnazine in it instead.
/// </summary>
[RegisterComponent]
public sealed partial class CMUTranqProjectileComponent : Component
{
    [DataField]
    public string Solution = "ammo";

    /// <summary>
    ///     Sedation one unit of Somnazine adds to a specimen. A full 5u shell is one capture hit.
    /// </summary>
    [DataField]
    public float SedationPerUnit = 0.2f;

    [DataField]
    public TimeSpan SedationTime = TimeSpan.FromSeconds(30);
}
