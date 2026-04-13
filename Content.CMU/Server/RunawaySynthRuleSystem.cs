using System.Threading;
using Content.Server._RMC14.Synth;
using Content.Server.AU14.ColonyEconomy;
using Content.Server.GameTicking.Rules;
using Content.Server.AU14.Round.Antags;
using Content.Server.AU14.Systems;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Roles;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.StationRecords;
using Content.Shared.CriminalRecords;
using Content.Shared.Paper;
using Robust.Shared.Random;

namespace Content.Server.AU14;

public sealed class RunawaySynthRuleSystem : GameRuleSystem<RunawaySynthRuleComponent>
{
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;

    [Dependency] private readonly WantedSystem _wantedSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly Content.Server.CriminalRecords.Systems.CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly Content.Server.CriminalRecords.Systems.CriminalRecordsConsoleSystem _criminalRecordsConsole = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public bool IsSynthAlive = true;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RunawaySynthComponent, MobStateChangedEvent>(OnSynthMobStateChanged);
        SubscribeLocalEvent<RunawaySynthComponent, ComponentStartup>(OnSynthSpawned);
    }


    private void OnSynthMobStateChanged(EntityUid uid, RunawaySynthComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead || args.NewMobState == MobState.Invalid)
        {
            _wantedSystem.SendFax(_entitySystemManager, _entityManager, "Colony Marshal Bureau", "AUPaperRunawaySynthDead", "Colony Administrator");

            _colonyBudget.AddToBudget(2500);
        }
    }
    //hardcoding for now,but prob should be a config option - eg
    private void OnSynthSpawned(EntityUid uid, RunawaySynthComponent component, ComponentStartup args)
    {
        // Build a list of 12 colonist names (including the synth) for the fax
        var synthName = _entityManager.GetComponentOrNull<MetaDataComponent>(uid)?.EntityName ?? "Unknown";

        var station = _stationSystem.GetOwningStation(uid);
        var nameList = new System.Collections.Generic.List<string> { synthName };

        if (station != null)
        {
            // Gather all colonist names from station records
            var allNames = new System.Collections.Generic.List<string>();
            foreach (var (_, record) in _stationRecords.GetRecordsOfType<GeneralStationRecord>(station.Value))
            {
                if (record.Name != synthName && !record.Name.Contains("(Unknown)") && !record.Name.Contains("Fugitive") && !record.Name.Contains("Runaway"))
                    allNames.Add(record.Name);
            }

            _random.Shuffle(allNames);
            var count = System.Math.Min(4, allNames.Count);
            for (var i = 0; i < count; i++)
                nameList.Add(allNames[i]);
        }

        // Shuffle the final list so the synth is in a random position
        _random.Shuffle(nameList);

        // Build the numbered list
        var listText = "";
        for (var i = 0; i < nameList.Count; i++)
            listText += $"  {i + 1}. {nameList[i]}\n";

        var faxContent = "[color=#383838]█[/color][color=#ffffff]░░[/color][color=#8c0000]█ [color=#383838]█▄[/color] █ [/color][head=3]Colonial Marshall Bureau[/head]\n\n" +
            "[color=#383838]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/color]\n" +
            "[color=#8c0000]▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀[/color]\n\n" +
            "[head=2][color=goldenrod]Fugitive Alert[/color][/head]\n\n" +
            "[bold]To:[/bold] [italic]CMB Office Staff[/italic]\n" +
            "[bold]From:[/bold] [bold]CMB Sectoral HQ[/bold]\n" +
            "[color=#134975]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]\n" +
            "Sheriff,\n" +
            "  A runaway Synthetic has been detected at your colony. One of the following colonists is the synth. " +
            "Liquidate it and the $2500 bounty is yours.\n\n" +
            "[bold]Suspect List:[/bold]\n" +
            listText + "\n" +
            "Signed,\n" +
            "[color=#dfc189][bolditalic]Regional HQ[/bolditalic][/color]\n" +
            "[color=#134975]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]";

        _wantedSystem.SendCustomFax(
            "Colony Marshal Bureau",
            "Fugitive Alert",
            faxContent,
            "paper_stamp-cmb",
            new System.Collections.Generic.List<StampDisplayInfo>
            {
                new() { StampedColor = Robust.Shared.Maths.Color.FromHex("#b0901b"), StampedName = "CMB" }
            },
            "Colony Administrator");

        if (station == null)
            return;

        // Add criminal record for runaway synth
        // Add a general record if not present (required for criminal record)
        var generalKey = _stationRecords.GetRecordByName(station.Value, "Runaway Synthetic");
        StationRecordKey key;
        if (generalKey is not uint id)
        {
            key = _stationRecords.AddRecordEntry(station.Value, new GeneralStationRecord
            {
                Name = "Runaway Synthetic"
            });
        }
        else
        {
            key = new StationRecordKey(id, station.Value);
        }

        // Add the criminal record with bounty 2500, all else null/default
        _stationRecords.AddRecordEntry<CriminalRecord>(key, new CriminalRecord
        {
            Bounty = 2500,
            Status = Content.Shared.Security.SecurityStatus.Wanted,
            Reason = "Defective Equipment",
            InitiatorName = "HQ",
            History = new System.Collections.Generic.List<CrimeHistory>()
        }, null);

        // Add to scanned records so it appears on the console
        _criminalRecordsConsole.AddScannedRecord(key);
    }
}
