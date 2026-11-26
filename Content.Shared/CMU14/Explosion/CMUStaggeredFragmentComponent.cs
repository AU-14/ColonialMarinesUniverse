using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Explosion;

/// <summary>
/// Component for grenades that should fragment in a staggered pattern over time
/// (e.g., bangalore torpedoes firing in a triangle pattern with timing separation)
/// </summary>
[RegisterComponent]
public sealed partial class CMUStaggeredFragmentComponent : Component
{
    /// <summary>
    /// Time delay in seconds between each projectile being spawned.
    /// </summary>
    [DataField("delayBetweenFragments")]
    public float DelayBetweenFragments = 0.1f;

    /// <summary>
    /// Whether the fragments should fire in a triangle pattern.
    /// For 3 fragments: left, center, right.
    /// </summary>
    [DataField("useTrianglePattern")]
    public bool UseTrianglePattern = true;

    /// <summary>
    /// Horizontal spread angle in degrees for triangle pattern.
    /// Total spread between left and right shots.
    /// </summary>
    [DataField("triangleSpreadAngle")]
    public float TriangleSpreadAngle = 20f;

    /// <summary>
    /// Time until next fragment spawns.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float NextFragmentTime = 0f;

    /// <summary>
    /// Total fragments to spawn.
    /// </summary>
    [ViewVariables]
    public int TotalFragments = 0;

    /// <summary>
    /// Fragments spawned so far.
    /// </summary>
    [ViewVariables]
    public int FragmentsSpawned = 0;
}
