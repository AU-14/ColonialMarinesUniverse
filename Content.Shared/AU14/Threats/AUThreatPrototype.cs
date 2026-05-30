using Content.Shared.AU14;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.AU14.util;
using Content.Shared.Roles;
using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Threats;
[Prototype]
public sealed partial class ThreatPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;


    [DataField("blacklistedPlatoons", required: false)]
    public List<string> BlacklistedPlatoons { get; private set; } = new();



    [DataField("WhitelistedPlatoons", required: false)]
    public List<string> WhitelistedPlatoons { get; private set; } = new();

    [DataField("threatweight", required: false)]
    public int ThreatWeight { get; private set; } = 1;

    /// <summary>
    /// List of game rule prototype IDs to add for this threat's win condition (e.g., "KillAllGovforRule", "ThreatSurviveRule").
    /// </summary>
    [DataField("winconditions", required: false)]
    public List<string> WinConditions { get; private set; } = new();


    [DataField("roundstartspawns")]
    public ProtoId<PartySpawnPrototype> RoundStartSpawn { get; private set; } = new();

    [DataField("possibleInserts")]
    public List<AuInsertPrototype> Inserts { get; private set; } = new();


 //   [DataField("govforratio")]
  //  public float GovForRatio { get; private set; } = 0.6f;

    [DataField("threatratio")]
    public float ThreatRatio { get; private set; } = 0.25f;


    [DataField("thirdpartyratio")]
    public float ThirdPartyRatio { get; private set; } = 0.15f;
    // for roundstart

    [DataField("blacklistedgamemodes")]
    public List<string> BlacklistedGamemodes { get; private set; } = new();

    [DataField("whitelistedgamemodes")]
    public List<string> whitelistedgamemodes { get; private set; } = new();


    [DataField("maxplayers")]
    public int MaxPlayers { get; private set; } = 100;

    [DataField("minplayers")]
    public int MinPlayers { get; private set; } = 0;

    [DataField("objectivewhitelist", required: false)]
    public List<string> ObjectiveWhitelist { get; private set; } = new();

    [DataField("addgamerules", required: false)]
    public List<string> AddGameRules { get; private set; } = new();

    [DataField("winmessage", required: false)]
    public string? WinMessage { get; private set; } = null;

    [DataField("maxthirdParties")]
    public int MaxThirdParties { get; private set; } = 7;

    [DataField("thirdpartyinterval", required: false)]

    public int ThirdPartyInterval { get; private set; } = 14000;

    [DataField("lorePrimer")]
    public ProtoId<LorePrimerPrototype>? LorePrimer { get; private set; }

    [DataField("hiveevolution")]
    public bool hiveevolution { get; private set; } = false;
    // if xeno evo should send messages

    /// <summary>
    /// Optional job scaling prototype for human job slots.
    /// Used by ColonyFall and DistressSignal modes (Insurgency/FOF use Planet instead).
    /// </summary>
    [DataField("jobScaling", required: false)]
    public ProtoId<JobScalePrototype>? JobScaling { get; private set; }

    /// <summary>
    /// Minimum seconds after round start before threat entities spawn and win conditions activate.
    /// </summary>
    [DataField("spawnDelayMin")]
    public int SpawnDelayMin { get; private set; } = 1800;

    /// <summary>
    /// Maximum seconds after round start before threat entities spawn and win conditions activate.
    /// </summary>
    [DataField("spawnDelayMax")]
    public int SpawnDelayMax { get; private set; } = 3800;

}
