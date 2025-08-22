using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives.Kill;

[RegisterComponent, NetworkedComponent]

public sealed partial class KillObjectiveComponent : Component
{


    [DataField("mobtokill", required: false)]
    public string MobToKill { get; private set; } = string.Empty;

    [DataField("humanonly", required: false)]
    public bool HumanOnly { get; private set; } = false;

    [DataField("amounttospawn", required:  false)]
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

    [DataField("factiontokill", required: false)]
    public string FactionToKill { get; private set; } = string.Empty;

    [DataField("amounttokill", required: false)]
    public int AmountToKill { get; private set; } = 1;

    [DataField("respawnOnRepeat", required: false)]
    public bool RespawnOnRepeat { get; private set; } = false;

    public bool MobsSpawned = false;


    public Dictionary<string, int> AmountKilledPerFaction { get; set; } = new();

    public bool IsFactionMatch(string faction)
    {
        return FactionToKill.ToLowerInvariant() == faction.ToLowerInvariant();
    }
}
