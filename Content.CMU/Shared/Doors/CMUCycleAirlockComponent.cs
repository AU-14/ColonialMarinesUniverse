using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Doors;

/// <summary>
///     One door of a cycling airlock. Only one door in a cycle can be open at a time: while one is open, the rest are
///     bolted shut, and they unbolt once every door in the cycle is closed again.
/// </summary>
/// <remarks>
///     Doors on the same grid with the same <see cref="Group"/> form a cycle. A door with no group cycles with every
///     other ungrouped cycling door within <see cref="AutoLinkRange"/> tiles, so a plain two-door airlock needs no
///     setup; give doors a group when they're further apart or when separate airlocks sit close together.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUCycleAirlockComponent : Component
{
    /// <summary>The cycle this door belongs to, shared by every door in it. Null links to nearby ungrouped doors.</summary>
    [DataField, AutoNetworkedField]
    public string? Group;

    /// <summary>How far away, in tiles, an ungrouped door looks for the other doors of its cycle.</summary>
    [DataField]
    public float AutoLinkRange = 3f;

    /// <summary>Whether this door's bolts are down because another door in its cycle is open.</summary>
    [DataField, AutoNetworkedField]
    public bool BoltedByCycle;
}
