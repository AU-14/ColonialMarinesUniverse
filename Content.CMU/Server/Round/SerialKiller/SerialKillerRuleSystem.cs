using Content.Server.AU14.ColonyEconomy;
using Content.Server.GameTicking.Rules;
using Content.Server.AU14.Systems;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.StationRecords;
using Content.Shared.CriminalRecords;
using Content.Shared.Security;
using Content.Shared.Cuffs.Components;

namespace Content.Server.AU14.Round.SerialKiller;

public sealed class SerialKillerRuleSystem : GameRuleSystem<SerialKillerRuleComponent>
{
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly Content.Server.CriminalRecords.Systems.CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly Content.Server.CriminalRecords.Systems.CriminalRecordsConsoleSystem _criminalRecordsConsole = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly WantedSystem _wantedSystem = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;

    private EntityUid? _killerUid = null;
    private bool _killerCaptured = false;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SerialKillerComponent, ComponentStartup>(OnSerialKillerSpawned);
    }

    protected override void Started(EntityUid uid, SerialKillerRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        _killerCaptured = false;
        _killerUid = null;
    }

    private void OnSerialKillerSpawned(EntityUid uid, SerialKillerComponent component, ComponentStartup args)
    {
        _killerUid = uid;

        // Send CMB fax on spawn
        _wantedSystem.SendFax(_entitySystemManager, _entityManager, "Colony Marshal Bureau", "AUPaperSerialKiller");

        var station = _stationSystem.GetOwningStation(uid);
        if (station == null)
            return;

        // Get fingerprints from the killer - identity is unknown but we have prints
        var fingerprint = _entityManager.GetComponentOrNull<Content.Shared.Forensics.Components.FingerprintComponent>(uid)?.Fingerprint ?? "none found";

        // Add anonymous record with fingerprints
        var generalKey = _stationRecords.GetRecordByName(station.Value, "Serial Killer");
        StationRecordKey key;
        if (generalKey is not uint id)
        {
            key = _stationRecords.AddRecordEntry(station.Value, new GeneralStationRecord
            {
                Name = "Serial Killer (Unknown)",
                Fingerprint = fingerprint,
            });
        }
        else
        {
            key = new StationRecordKey(id, station.Value);
        }

        _stationRecords.AddRecordEntry<CriminalRecord>(key, new CriminalRecord
        {
            Bounty = 2000,
            Status = SecurityStatus.Wanted,
            Reason = "Wanted for multiple homicides - suspect at large",
            InitiatorName = "HQ",
            History = new System.Collections.Generic.List<CrimeHistory>()
        }, null);

        _criminalRecordsConsole.AddScannedRecord(key);
    }

    protected override void ActiveTick(EntityUid uid, SerialKillerRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        if (_killerCaptured || _killerUid == null)
            return;
        if (!_entityManager.EntityExists(_killerUid.Value))
            return;
        if (IsKillerDetained(_killerUid.Value))
        {
            _killerCaptured = true;
            _wantedSystem.SendFax(_entitySystemManager, _entityManager, "Colony Marshal Bureau", "AUPaperSerialKillerCaptured");
            _colonyBudget.AddToBudget(2000);
        }
    }

    private bool IsKillerDetained(EntityUid uid)
    {
        if (_entityManager.TryGetComponent<CuffableComponent>(uid, out var cuffed) && cuffed.CuffedHandCount > 0)
            return true;
        if (_entityManager.TryGetComponent<MobStateComponent>(uid, out var state))
        {
            if (state.CurrentState == MobState.Dead || state.CurrentState == MobState.Invalid)
                return true;
        }
        else
        {
            return true;
        }
        return false;
    }
}

