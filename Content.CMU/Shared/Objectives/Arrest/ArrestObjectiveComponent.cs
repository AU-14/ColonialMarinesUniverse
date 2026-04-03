using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives.Arrest;

[RegisterComponent, NetworkedComponent]
public sealed partial class ArrestObjectiveComponent : Component
{
    [DataField("mobtoarrest", required: false)]
    public string MobToArrest { get; private set; } = string.Empty;

    [DataField("humanonly", required: false)]
    public bool HumanOnly { get; private set; } = false;

    [DataField("amounttospawn", required: false)]
    public int AmountToSpawn { get; private set; } = 1;

    [DataField("spawn")]
    public bool Spawn { get; private set; } = false;

    [DataField("synthonly", required: false)]
    public bool SynthOnly { get; private set; } = false;

    [DataField("specificjob", required: false)]
    public string? SpecificJob { get; private set; } = null;

    [DataField("spawnmob", required: false)]
    public bool SpawnMob { get; private set; } = false;

    [DataField("spawnmarker", required: false)]
    public string SpawnMarker { get; private set; } = string.Empty;

    [DataField("factiontoarrest", required: false)]
    public string FactionToArrest { get; private set; } = string.Empty;

    [DataField("amounttoarrest", required: false)]
    public int AmountToArrest { get; private set; } = 1;

    [DataField("respawnOnRepeat", required: false)]
    public bool RespawnOnRepeat { get; private set; } = false;

    [DataField("removekillmark", required: false)]
    public bool RemoveKillMark { get; private set; } = true;

    public bool MobsSpawned = false;

    public Dictionary<string, int> AmountArrestedPerFaction { get; set; } = new();

    public bool IsFactionMatch(string faction)
    {
        return FactionToArrest.ToLowerInvariant() == faction.ToLowerInvariant();
    }
}


