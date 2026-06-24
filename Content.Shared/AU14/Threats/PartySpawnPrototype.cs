using System.Runtime.Serialization;
using Content.Shared.AU14.util;
using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.Threats;


[Prototype]
public sealed partial class PartySpawnPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("gruntsToSpawn", required: false)]
    public Dictionary<string, int> GruntsToSpawn { get; private set; } = new Dictionary<string, int>();

    [DataField("leadersToSpawn", required: true)]
    public Dictionary<string, int> LeadersToSpawn { get; private set; } = new Dictionary<string, int>();

    [DataField("entsToSpawn", required: false)]
    public Dictionary<string, int> EntitiesToSpawn { get; private set; } = new Dictionary<string, int>();

    [DataField("spawnTogether", required: false),]
    public bool SpawnTogether { get; private set; } = true;

    /// <summary>
    /// Deprecated — use <see cref="Scaling"/> instead.
    /// Kept for backwards compatibility with existing YAML.
    /// TODO:
    /// </summary>
    [DataField("scalewithpop", required: false)]
    public bool ScaleWithPop { get; private set; } = false;

    /// <summary>
    /// Per-entity population scaling. Key is the entity prototype ID (must match a key in
    /// <see cref="LeadersToSpawn"/> or <see cref="GruntsToSpawn"/>).
    /// When present the scaled count replaces the static count for that entity.
    /// Formula: extra = floor(playerCount * Scale)
    /// Final: slots = min(Maximum ?? int.MaxValue, (Benchmark ?? staticCount) + extra)
    /// </summary>
    [DataField("scaling", required: false)]
    public Dictionary<string, JobScaleEntry> Scaling { get; private set; } = new();

    [DataField("Markers", required: false)]
    public Dictionary<ThreatMarkerType, string> Markers { get; private set; } = new Dictionary<ThreatMarkerType, string>();
    // threatmarkertype, custommarkerid. if blank use generic
}
