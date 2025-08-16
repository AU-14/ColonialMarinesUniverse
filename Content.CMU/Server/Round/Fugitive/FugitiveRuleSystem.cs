using Content.Server.AU14.ColonyEconomy;
using Content.Server.GameTicking.Rules;
using Content.Server.AU14.Round.Antags;
using Content.Server.AU14.Round.fugitive;
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
using Content.Shared.Security;
using Content.Server.CriminalRecords.Systems;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.AU14.round.Fugitive;

public sealed class FugitiveRuleSystem : GameRuleSystem<FugitiveRuleComponent>
{
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly Content.Server.CriminalRecords.Systems.CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly Content.Server.CriminalRecords.Systems.CriminalRecordsConsoleSystem _criminalRecordsConsole = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly Content.Server.AU14.Systems.WantedSystem _wantedSystem = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;

    private EntityUid? _fugitiveUid = null;
    private bool _fugitiveCaptured = false;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FugitiveComponent, ComponentStartup>(OnFugitiveSpawned);
    }

    protected override void Started(EntityUid uid, FugitiveRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        _fugitiveCaptured = false;
        _fugitiveUid = null;
    }

    private void OnFugitiveSpawned(EntityUid uid, FugitiveComponent component, ComponentStartup args)
    {
        _fugitiveUid = uid;

        // Send CMB fax on fugitive spawn
        _wantedSystem.SendFax(_entitySystemManager, _entityManager, "Colony Marshal Bureau", "AUPaperFugitive");

        var station = _stationSystem.GetOwningStation(uid);
        if (station == null)
            return;

        // Get the fugitive's name, prints, and DNA
        var name = _entityManager.GetComponentOrNull<MetaDataComponent>(uid)?.EntityName ?? "Fugitive";
        var fingerprint = _entityManager.GetComponentOrNull<Content.Shared.Forensics.Components.FingerprintComponent>(uid)?.Fingerprint ?? "none found";
        var dna = _entityManager.GetComponentOrNull<Content.Shared.Forensics.Components.DnaComponent>(uid)?.DNA ?? "none found";

        // Add a general record if not present
        var generalKey = _stationRecords.GetRecordByName(station.Value, name);
        StationRecordKey key;
        if (generalKey is not uint id)
        {
            key = _stationRecords.AddRecordEntry(station.Value, new GeneralStationRecord
            {
                Name = "Fugitive: " + name,
                Fingerprint = fingerprint,
                DNA = dna
            });
        }
        else
        {
            key = new StationRecordKey(id, station.Value);
        }

        // Add the criminal record with bounty, wanted status, etc.
        _stationRecords.AddRecordEntry<CriminalRecord>(key, new CriminalRecord
        {
            Bounty = 1800,
            Status = SecurityStatus.Wanted,
            Reason = "Fugitive from justice",
            InitiatorName = "HQ",
            History = new System.Collections.Generic.List<CrimeHistory>()
        }, null);

        _criminalRecordsConsole.AddScannedRecord(key);
    }

    protected override void ActiveTick(EntityUid uid, FugitiveRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        if (_fugitiveCaptured || _fugitiveUid == null)
            return;
        if (!_entityManager.EntityExists(_fugitiveUid.Value))
            return;
        if (IsFugitiveDetained(_fugitiveUid.Value))
        {
            _fugitiveCaptured = true;
            var name = _entityManager.GetComponentOrNull<MetaDataComponent>(_fugitiveUid.Value)?.EntityName ?? "The fugitive";
            _wantedSystem.SendFax(_entitySystemManager, _entityManager, "Colony Marshal Bureau", "AUPaperFugitiveCaptured");
            _colonyBudget.AddToBudget(1800);
        }
    }

    /// <summary>
    /// Returns true if the fugitive is considered detained (cuffed or dead or missing MobState)
    /// </summary>
    private bool IsFugitiveDetained(EntityUid uid)
    {
        if (_entityManager.TryGetComponent<Content.Shared.Cuffs.Components.CuffableComponent>(uid, out var cuffed) && cuffed.CuffedHandCount > 0)
            return true;
        if (_entityManager.TryGetComponent<MobStateComponent>(uid, out var state))
        {
            if (state.CurrentState == MobState.Dead || state.CurrentState == MobState.Invalid)
                return true;
        }
        else
        {
            // If they don't have MobStateComponent, treat as dead
            return true;
        }
        return false;
    }
}
