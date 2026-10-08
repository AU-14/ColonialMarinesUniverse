using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Expeditions;

/// <summary>Runtime expedition metadata. The generator owns its lifecycle; the future mission console consumes it.</summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class CMUExpeditionMapComponent : Component
{
    public CMUExpeditionPlan Plan = default!;
    public EntProtoId<CMUExpeditionProfileComponent> Profile;
    public ProtoId<CMUExpeditionScenarioPrototype>? Scenario;
    public EntityUid? RecoveryTarget;
    public readonly List<EntityUid> Evidence = new();
    public bool Ready;
    public EntityUid? LandingBeacon;
    public bool FiresStarted;
    public bool GuardsSpawned;
    public int NextSquad = 1;
    public bool AutoOpen;
    public EntityUid? ZNetwork;
    public readonly List<EntityUid> UpperMaps = new();
}

[ByRefEvent]
public readonly record struct CMUExpeditionReadyEvent;
