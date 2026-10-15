using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     A wire-guided missile in flight. The server steers it: with a <see cref="TargetEntity"/> it homes on that
///     entity and the guider's cursor bends the path; without one it flies to the guider's cursor
///     (<see cref="Target"/>), or straight until the guider sends one. The guider, finder, target entity and wire
///     endpoints are networked, so the guider's client knows when to stream cursor positions and every client
///     can draw the wire.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUGuidedMissileComponent : Component
{
    /// <summary>
    ///     The spotter steering this missile. Null once guidance is lost; the missile then flies straight.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Guider;

    /// <summary>
    ///     The finder the guider must keep holding.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Finder;

    /// <summary>
    ///     The A.T.G.M. tripod the missile was fired from. The tripod rather than the fixed launcher inside it,
    ///     because the tripod is anchored and visible; used as the wire's start point.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Launcher;

    /// <summary>
    ///     Where the missile was spawned. The wire's start point if <see cref="Launcher"/> is not available.
    /// </summary>
    [DataField, AutoNetworkedField]
    public MapCoordinates WireOrigin = MapCoordinates.Nullspace;

    /// <summary>
    ///     The entity the spotter locked before launch. The missile homes on it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TargetEntity;

    /// <summary>
    ///     The guider's latest cursor position. Without a lock this is the aim point; with a lock it bends the
    ///     path while fresh. Nullspace until the first accepted update.
    /// </summary>
    [ViewVariables]
    public MapCoordinates Target = MapCoordinates.Nullspace;

    /// <summary>
    ///     Whether the guider has sent at least one accepted cursor update. Until then an unlocked missile
    ///     flies straight and can only detonate by collision or timeout.
    /// </summary>
    [ViewVariables]
    public bool HasGuidance;

    /// <summary>
    ///     Last known world position of <see cref="TargetEntity"/>.
    /// </summary>
    [ViewVariables]
    public MapCoordinates TrackedPosition = MapCoordinates.Nullspace;

    /// <summary>
    ///     Estimated velocity of <see cref="TargetEntity"/>, from per-tick position differences.
    /// </summary>
    [ViewVariables]
    public Vector2 TrackedVelocity;

    /// <summary>
    ///     Once set, the missile no longer follows <see cref="TargetEntity"/> (deleted, changed map or out of the
    ///     spotter's sight) and flies to <see cref="TrackedPosition"/> instead.
    /// </summary>
    [ViewVariables]
    public bool TrackingFrozen;

    /// <summary>
    ///     Current flight heading, as a world-space vector angle (see <see cref="Vector2Helpers.ToAngle"/>).
    /// </summary>
    [DataField]
    public Angle Heading;

    /// <summary>
    ///     Heading at launch; <see cref="MaxOffBoresightDeg"/> is measured from this.
    /// </summary>
    [DataField]
    public Angle LaunchHeading;

    [DataField]
    public float Speed = 9f;

    [DataField]
    public float TurnRateDegPerSec = 90f;

    [DataField]
    public float MaxOffBoresightDeg = 120f;

    [DataField]
    public float ImpactRadius = 0.25f;

    [DataField]
    public float MaxGuidanceDistance = 30f;

    [DataField]
    public float MaxSteerOffset = 4f;

    [DataField]
    public float SteerOffsetPerDistance = 0.5f;

    [DataField]
    public float TerminalDistance = 2.5f;

    [DataField]
    public float FadeDistance = 3f;

    [DataField]
    public TimeSpan CursorFreshness = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float MaxLead = 2f;

    [DataField]
    public TimeSpan TargetSightTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Before this time the missile ignores <see cref="ImpactRadius"/>.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ArmedAt;

    /// <summary>
    ///     The missile self-detonates at this time.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan DetonateAt;

    /// <summary>
    ///     Server time of the last accepted guidance update; the cursor is stale after
    ///     <see cref="CursorFreshness"/>.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan LastGuidanceUpdate;

    /// <summary>
    ///     When the guider last had an unoccluded view of <see cref="TargetEntity"/>.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan TargetLastSeen;

    /// <summary>
    ///     Next time the guider's view of <see cref="TargetEntity"/> is checked.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextSightCheck;

    /// <summary>
    ///     Session subscribed to this missile's PVS so the spotter keeps seeing it.
    /// </summary>
    [ViewVariables]
    public ICommonSession? ViewSubscriber;
}
