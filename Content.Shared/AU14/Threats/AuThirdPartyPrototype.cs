using Content.Shared.AU14;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.AU14.util;
using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Shared.Utility;

namespace Content.Shared.AU14.Threats;
[Prototype]
public sealed partial class AuThirdPartyPrototype : IPrototype
{

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("blacklistedThreats")]
    public List<string> BlacklistedThreats { get; private set; } = new();

    [DataField("whitelistedThreats")]
    public List<string> WhitelistedThreats { get; private set; } = new();

    [DataField("enterbyshuttle")]
    public bool Enterbyshuttle { get; private set; } =  false;

    [DataField("dropshippath", required: false)]
    public ResPath dropshippath { get; private set; } = new ResPath( "/Maps/_RMC14/alamo.yml");
    // used if enterbyshuttle is true

    [DataField("blacklistedgamemodes")]
    public List<string> BlacklistedGamemodes { get; private set; } = new();

    [DataField("whitelistedgamemodes")]
    public List<string> whitelistedgamemodes { get; private set; } = new();
    [DataField("weight", required: false)]
    public int weight { get; private set; } = 1;

    [DataField("maxplayers")]
    public int MaxPlayers { get; private set; } = 100;

    [DataField("minplayers")]
    public int MinPlayers { get; private set; } = 0;
// for rolling

    [DataField("GhostsNeeded")]
    public int GhostsNeeded { get; private set; } = 10;
    // used if this isn't a roundstart spawn


    [DataField("blacklistedPlatoons", required: false)]
    public List<string> BlacklistedPlatoons { get; private set; } = new();



    [DataField("WhitelistedPlatoons", required: false)]
    public List<string> WhitelistedPlatoons { get; private set; } = new();

    [DataField("roundstart", required: false)]
    public bool RoundStart { get; private set; } = false;

    [DataField("partyspawn", required: true)]
    public ProtoId<PartySpawnPrototype> PartySpawn { get; private set; } = new();


    [DataField("announcearrival", required: false)]
    public string? AnnounceArrival { get; private set; } = "A new force has entered the battlefield.";

}
