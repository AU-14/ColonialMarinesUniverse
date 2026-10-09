using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Driver actions of an open vehicle: the hazard lights, the automatic turn signals, the horn and the
/// headlight switch. Added to the vehicle the first time someone takes its driver's seat.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleDriverActionsComponent : Component
{
    [DataField]
    public EntProtoId HazardsAction = "CMUActionJeepHazards";

    [DataField]
    public EntProtoId HornAction = "CMUActionJeepHorn";

    [DataField]
    public EntProtoId HeadlightsAction = "CMUActionJeepHeadlights";

    [DataField]
    public EntProtoId AutoSignalsAction = "CMUActionJeepAutoSignals";

    [DataField]
    public EntityUid? HazardsActionEntity;

    [DataField]
    public EntityUid? HornActionEntity;

    [DataField]
    public EntityUid? HeadlightsActionEntity;

    [DataField]
    public EntityUid? AutoSignalsActionEntity;

    /// <summary>
    /// Both turn signals blink together while this is on.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Hazards;

    /// <summary>
    /// The turn signals blink on their own while the driver steers.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoSignals = true;
}

public sealed partial class CMUVehicleHazardsActionEvent : InstantActionEvent;

public sealed partial class CMUVehicleHornActionEvent : InstantActionEvent;

public sealed partial class CMUVehicleAutoSignalsActionEvent : InstantActionEvent;
