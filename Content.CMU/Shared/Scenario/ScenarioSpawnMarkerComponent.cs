using System.Collections.Generic;

namespace Content.Shared.AU14.Scenario;

[RegisterComponent]
public sealed partial class ScenarioSpawnMarkerComponent : Component
{
    [DataField(required: true)]
    public SpawnMarkerKind Kind { get; private set; }

    [DataField(required: true)]
    public List<string> Tags { get; private set; } = new();

    [DataField]
    public int Count { get; private set; } = 1;
}
