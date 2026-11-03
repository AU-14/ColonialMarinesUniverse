using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     Optional target finder for an A.T.G.M. The holder can click an entity to lock the next missiles onto it;
///     the gunner still guides each missile with their cursor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CMUATGMSystem))]
public sealed partial class CMUATGMFinderComponent : Component
{
    /// <summary>
    ///     The launcher this finder is wired to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedLauncher;

    /// <summary>
    /// <summary>
    ///     The entity the finder user has lased. Missiles fired while this is set home on it. Server-authoritative;
    ///     validated on use because the entity may since have been deleted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LockedTarget;

    /// <summary>
    ///     The finder user needs an unoccluded view of the target within this many tiles to lock it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LockRange = 30f;

    /// <summary>
    ///     Minimum time between two successful locks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LockCooldown = TimeSpan.FromSeconds(1.5);

    /// <summary>
    ///     How long the laser and lock last, in seconds, before the finder user has to lase again.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LockDuration = 30f;

    /// <summary>
    ///     A new lock cannot be taken before this time.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextLock;
}
