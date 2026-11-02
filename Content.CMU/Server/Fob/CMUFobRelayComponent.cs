namespace Content.Server.CMU14.Fob;

/// <summary>
/// A relay endpoint offered by the map-specific selector. Completion requires its current job generation.
/// Physical repair interactions must validate the operator and finish before redeeming this endpoint.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(CMUFobReserveSystem))]
public sealed partial class CMUFobRelayComponent : Component
{
    [ViewVariables] public EntityUid? Network;
    [ViewVariables] public int JobGeneration;
    [ViewVariables, AutoPausedField] public TimeSpan ReusableAt;
}

/// <summary>The network identity prevents a delayed repair from redeeming another faction's or round's job.</summary>
public readonly record struct CMUFobRelayJob(EntityUid Network, int Generation);

/// <summary>Invalidates physical work when an endpoint is offered or released.</summary>
[ByRefEvent]
public readonly record struct CMUFobRelayChangedEvent;
