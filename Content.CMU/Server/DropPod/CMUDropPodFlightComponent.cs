using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.DropPod;

/// <summary>
/// Runtime state of a pod between launch and landing.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUDropPodSystem))]
public sealed partial class CMUDropPodFlightComponent : Component
{
    [ViewVariables]
    public EntityUid PlanetMap;

    [ViewVariables]
    public Vector2i Target;

    [ViewVariables]
    public EntityUid? TransitMap;

    [ViewVariables]
    public int Phase = -1;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPhaseAt;

    [ViewVariables]
    public bool Falling;

    [ViewVariables]
    public bool BrokenApart;

    [ViewVariables]
    public EntityCoordinates Landing;
}

/// <summary>
/// Runtime state of a landed pod that is still holding its occupant.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUDropPodSystem))]
public sealed partial class CMUDropPodLandedComponent : Component
{
    [ViewVariables]
    public bool Injured;

    [ViewVariables]
    public int TreatmentRemaining;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextActionAt;
}

/// <summary>
/// Runtime state of a custom drop unit between launch and landing. Lives on the unit's core.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUCustomDropUnitSystem))]
public sealed partial class CMUCustomDropUnitFlightComponent : Component
{
    [ViewVariables]
    public EntityUid PlanetMap;

    [ViewVariables]
    public Vector2i Target;

    [ViewVariables]
    public int Phase = -1;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPhaseAt;

    [ViewVariables]
    public bool BrokenApart;

    /// <summary>
    /// The unit's doors, kept shut and bolted for the flight.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> Doors = new();
}
