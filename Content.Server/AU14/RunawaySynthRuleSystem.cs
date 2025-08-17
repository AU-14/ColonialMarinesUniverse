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
        _wantedSystem.SendFax(_entitySystemManager, _entityManager, "Colony Marshal Bureau", "AUPaperRunawaySynth", "Colony Administrator");

        // Add criminal record for runaway synth
        var station = _stationSystem.GetOwningStation(uid);
        if (station == null)
            return;

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
