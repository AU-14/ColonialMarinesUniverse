using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Scent;

/// <summary>
/// Lives on a Wendigo. After it catches a scent (Catch Scent action), it tracks <see cref="Target"/> for
/// <see cref="Duration"/>: a tracker arrow alert points at the target and the target's recent positions
/// (<see cref="Trail"/>) are drawn as a red smell trail, only for this Wendigo's own player.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUWendigoScentTrackerComponent : Component
{
    // The trail is the Wendigo's own sense; nobody else needs it.
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan TrackUntil;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(180);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField]
    public TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    /// <summary>Oldest points are dropped past this many.</summary>
    [DataField]
    public int MaxTrailPoints = 20;

    /// <summary>A sample closer than this to the newest point only refreshes that point instead of adding one.</summary>
    [DataField]
    public float MinPointSpacing = 0.5f;

    /// <summary>How long a trail point takes to fade out completely on the client.</summary>
    [DataField]
    public TimeSpan TrailFade = TimeSpan.FromSeconds(25);

    /// <summary>Recent positions of the target, oldest first.</summary>
    [ViewVariables, AutoNetworkedField]
    public List<CMUWendigoScentPoint> Trail = new();

    [DataField]
    public ProtoId<AlertPrototype> Alert = "CMUWendigoScentTracker";

    [DataField]
    public ProtoId<AlertCategoryPrototype> AlertCategory = "CMUWendigoScent";
}

/// <summary>One sampled position of a scent target, with the server time it was last confirmed there.</summary>
[Serializable, NetSerializable]
public readonly record struct CMUWendigoScentPoint(MapCoordinates Coordinates, TimeSpan Time);
