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
using Robust.Shared.Maths;

namespace Content.Server.AU14.Round.CLFVeteran;

public sealed class CLFVeteranRuleSystem : GameRuleSystem<CLFVeteranRuleComponent>
{
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly Content.Server.CriminalRecords.Systems.CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly Content.Server.CriminalRecords.Systems.CriminalRecordsConsoleSystem _criminalRecordsConsole = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly WantedSystem _wantedSystem = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;

    private EntityUid? _veteranUid = null;
    private bool _veteranCaptured = false;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CLFVeteranComponent, ComponentStartup>(OnVeteranSpawned);
    }

    protected override void Started(EntityUid uid, CLFVeteranRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        _veteranCaptured = false;
        _veteranUid = null;
    }

    private void OnVeteranSpawned(EntityUid uid, CLFVeteranComponent component, ComponentStartup args)
    {
        _veteranUid = uid;

        _wantedSystem.SendFax(_entitySystemManager, _entityManager, "Colony Marshal Bureau", "AUPaperCLFVeteran");

        // Send CLF fax with veteran's name
        var veteranName = _entityManager.GetComponentOrNull<MetaDataComponent>(uid)?.EntityName ?? "Unknown";
        var clfContent = "[head=3][color=#2e5a1e]Colony Liberation Front[/color][/head]\n\n" +
            "[color=#2e5a1e]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/color]\n\n" +
            "[bold]To:[/bold] [italic]Field Operatives[/italic]\n" +
            "[bold]From:[/bold] [bold]CLF Regional Command[/bold]\n" +
            "[color=#2e5a1e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]\n" +
            "Comrades,\n" +
            $"  A disavowed operative, [bold]{veteranName}[/bold], has been located in the colony. " +
            "Bring them back into the fold, or deal with them accordingly.\n\n" +
            "Freedom or death,\n" +
            "[color=#2e5a1e][bolditalic]CLF Command[/bolditalic][/color]\n" +
            "[color=#2e5a1e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]";
        _wantedSystem.SendCustomFax(
            "Colony Liberation Front",
            "Encrypted Message",
            clfContent,
            "paper_stamp-clf",
            new System.Collections.Generic.List<Content.Shared.Paper.StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#2e5a1e"), StampedName = "CLF" }
            });

        var station = _stationSystem.GetOwningStation(uid);
        if (station == null)
            return;

        var generalKey = _stationRecords.GetRecordByName(station.Value, "CLF Veteran");
        StationRecordKey key;
        if (generalKey is not uint id)
        {
            key = _stationRecords.AddRecordEntry(station.Value, new GeneralStationRecord
            {
                Name = "CLF Veteran (Unknown)",
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
            Reason = "Known CLF insurgent - armed and dangerous",
            InitiatorName = "HQ",
            History = new System.Collections.Generic.List<CrimeHistory>()
        }, null);

        _criminalRecordsConsole.AddScannedRecord(key);
    }

    protected override void ActiveTick(EntityUid uid, CLFVeteranRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        if (_veteranCaptured || _veteranUid == null)
            return;
        if (!_entityManager.EntityExists(_veteranUid.Value))
            return;
        if (IsVeteranDetained(_veteranUid.Value))
        {
            _veteranCaptured = true;
            _wantedSystem.SendFax(_entitySystemManager, _entityManager, "Colony Marshal Bureau", "AUPaperCLFVeteranCaptured");
            _colonyBudget.AddToBudget(2000);
        }
    }

    private bool IsVeteranDetained(EntityUid uid)
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

