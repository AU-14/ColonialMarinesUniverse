using Content.Server.Antag;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.Systems;
using Content.Server.CriminalRecords.Systems;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared._RMC14.Language.Components;
using Content.Shared.StationRecords.Components;
using Content.Shared.StationRecords.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.CMU14.Round.Antags.ColonyBounty;
using Robust.Shared.GameObjects;
using System.Linq;
namespace Content.Server.CMU14.Round.Antags.ColonyBounty;

/// <summary>
/// Shared bookkeeping for bounty-carrying colony antags: anonymous wanted record with a
/// witness description on spawn, one-shot payout into the colony budget once the antag is
/// booked Detained on the records console or confirmed dead.
/// </summary>
public sealed partial class ColonyBountySystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly CriminalRecordsConsoleSystem _criminalRecordsConsole = default!;
    [Dependency] private readonly CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private readonly SuspectDescriptionSystem _suspectDescription = default!;
    [Dependency] private readonly WantedSystem _wanted = default!;

    // Evidence scans already logged to a record; rescanning the same item must not spam the history
    private readonly HashSet<(uint Record, EntityUid Item)> _loggedEvidence = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ForensicScannerScannedEvent>(OnForensicScan);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
        => _loggedEvidence.Clear();

    public override void Update(float frameTime)
    {
        var enumerator = EntityManager.AllEntityQueryEnumerator<ColonyBountyComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            // Registration runs on the first update after spawn so runtime-added bounties
            // (cannibal, arsonist, saboteur) see their configured fields.
            if (!comp.Registered)
            {
                comp.Registered = true;
                RegisterWantedRecord(uid, comp);
            }

            if (comp.Paid
                || comp.CapturedFaxPaper is not { } paper
                || !Resolved(uid, comp, out var captured))
                continue;

            comp.Paid = true;
            comp.Captured = captured;
            if (comp.Captured && !comp.CoverBlown)
                BlowCover(uid, comp);
            _wanted.SendPaperToGroup(ColonyCmbFax.MarshalBureauFaxGroup, paper, comp.CapturedFaxExtraRecipient);
            _colonyBudget.AddToBudget(comp.Bounty);
        }
    }

    private void RegisterWantedRecord(EntityUid uid, ColonyBountyComponent comp)
    {
        var station = _station.GetOwningStation(uid);
        if (station == null)
        {
            comp.Registered = false;
            return;
        }

        var name = MetaData(uid).EntityName;
        var recordName = UniqueRecordName(station.Value, comp.RecordName ?? comp.RecordNamePrefix + name);
        // Cache the own record for the per-tick Detained check; null means it doesn't exist yet
        comp.OwnRecordId = _stationRecords.GetRecordByName(station.Value, name);
        comp.OwnRecordName = name;
        var key = _stationRecords.AddRecordEntry(station.Value, new GeneralStationRecord
        {
            Name = recordName,
            Fingerprint = comp.IncludePrints
                ? EntityManager.GetComponentOrNull<FingerprintComponent>(uid)?.Fingerprint ?? "none found"
                : null,
            DNA = comp.IncludeDna
                ? EntityManager.GetComponentOrNull<DnaComponent>(uid)?.DNA ?? "none found"
                : null,
        });

        // The alias must stay anonymous; identity comes from the fuzzed description and prints
        var description = _suspectDescription.Describe(
            uid, _suspectDescription.RandomWitness(uid, station));
        var reason = string.IsNullOrEmpty(description)
            ? comp.Reason
            : Loc.GetString("suspect-description-reason",
                ("reason", comp.Reason), ("description", description));
        if (Languages(uid) is { } langs)
            reason += " " + langs;

        _stationRecords.AddRecordEntry<CriminalRecord>(key, new CriminalRecord
        {
            Bounty = comp.Bounty,
            Status = SecurityStatus.Wanted,
            Reason = reason,
            InitiatorName = "HQ",
            History = new List<CrimeHistory>(),
        }, null);

        comp.AliasRecordId = key.Id;
        _criminalRecordsConsole.AddScannedRecord(key);
    }

    /// <summary>
    /// Raises the alias record's bounty to the component's value, so escalation is visible
    /// on the console and freshly printed posters.
    /// </summary>
    public void SyncRecordBounty(EntityUid uid, ColonyBountyComponent comp)
    {
        if (comp.AliasRecordId is not { } id)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null
            || !_stationRecords.TryGetRecord<CriminalRecord>(new StationRecordKey(id, station.Value), out var record))
            return;

        record.Bounty = comp.Bounty;
        _stationRecords.Synchronize(new StationRecordKey(id, station.Value));
    }

    // Two "Serial Killer (Unknown)" on one record would silently overwrite each other's
    // criminal entry and prints; every bounty gets its own alias.
    private string UniqueRecordName(Entity<StationRecordsComponent?> station, string baseName)
    {
        if (_stationRecords.GetRecordByName(station, baseName) == null)
            return baseName;

        for (var n = 2;; n++)
        {
            var candidate = $"{baseName} #{n}";
            if (_stationRecords.GetRecordByName(station, candidate) == null)
                return candidate;
        }
    }

    private void OnForensicScan(ref ForensicScannerScannedEvent args)
    {
        var target = args.Target;

        if (TryComp<ColonyBountyComponent>(target, out var bounty))
        {
            // A forensic scan files the antag's own record; from that moment HQ could cross-reference them
            if (!bounty.CoverBlown)
                BlowCover(target, bounty);
            return;
        }

        // People scans stay manual comparisons on purpose; only evidence items write history
        if (HasComp<HumanoidProfileComponent>(target))
            return;

        LogEvidence(args.Scanner, target);
    }

    private void BlowCover(EntityUid uid, ColonyBountyComponent comp)
    {
        comp.CoverBlown = true;
        _adminLogger.Add(LogType.Mind, LogImpact.Medium,
            $"{ToPrettyString(uid):name}'s bounty cover was blown");
        _antag.SendBriefing(uid, Loc.GetString("cmu-bounty-cover-blown"), Color.Red, null);
    }

    private void LogEvidence(EntityUid scanner, EntityUid item)
    {
        if (!TryComp<ForensicScannerComponent>(scanner, out var scannerComp))
            return;

        if (scannerComp.Fingerprints.Count == 0 && scannerComp.DNAs.Count == 0)
            return;

        var station = _station.GetOwningStation(scanner);
        if (station == null)
            return;

        var itemName = MetaData(item).EntityName;
        foreach (var (key, record) in _stationRecords.GetRecordsOfType<CriminalRecord>(station.Value))
        {
            if (record.Status != SecurityStatus.Wanted)
                continue;

            if (!_stationRecords.TryGetRecord<GeneralStationRecord>(new StationRecordKey(key, station.Value), out var general))
                continue;

            var isPrints = general.Fingerprint != null && scannerComp.Fingerprints.Contains(general.Fingerprint);
            var isDna = general.DNA != null && scannerComp.DNAs.Contains(general.DNA);
            if ((!isPrints && !isDna) || !_loggedEvidence.Add((key, item)))
                continue;

            var kind = isPrints ? "cmu-bounty-evidence-prints" : "cmu-bounty-evidence-dna";
            _criminalRecords.TryAddHistory(new StationRecordKey(key, station.Value),
                Loc.GetString("cmu-bounty-evidence-history",
                    ("kind", Loc.GetString(kind)), ("item", itemName)),
                "Forensic Scanner");
        }
    }

    private string? Languages(EntityUid uid)
    {
        if (!TryComp<LanguageComponent>(uid, out var langs))
            return null;

        var named = langs.SpokenLanguages
            .Where(l => l != langs.DefaultLanguage)
            .Select(l => Loc.GetString($"language-{l}-name"))
            .ToList();

        return named.Count == 0
            ? null
            : Loc.GetString("suspect-description-languages",
                ("languages", string.Join(", ", named)));
    }

    private bool Resolved(EntityUid uid, ColonyBountyComponent comp, out bool captured)
    {
        captured = false;

        if (comp.DeadCounts
            && EntityManager.GetComponentOrNull<MobStateComponent>(uid)?.CurrentState
                is MobState.Dead or MobState.Invalid)
            return true;

        if (!comp.CaptureCounts)
            return false;

        var station = _station.GetOwningStation(uid);
        if (station == null)
            return false;

        if (comp.OwnRecordId is not { } id)
        {
            // First check or the colonist record didn't exist at registration; retry under the spawn name
            if (_stationRecords.GetRecordByName(station.Value, comp.OwnRecordName ?? MetaData(uid).EntityName)
                is not { } found)
                return false;

            comp.OwnRecordId = id = found;
        }

        if (IsDetained(new StationRecordKey(id, station.Value)))
        {
            captured = true;
            return true;
        }

        // A renamed antag (the replicant) is also caught through the record of the identity
        // it now wears. Either Detained booking resolves the bounty; Paid keeps it single.
        if (MetaData(uid).EntityName == comp.OwnRecordName)
            return false;

        if (comp.CopiedRecordId is not { } copied)
        {
            if (_stationRecords.GetRecordByName(station.Value, MetaData(uid).EntityName) is not { } foundCopy)
                return false;

            comp.CopiedRecordId = copied = foundCopy;
        }

        if (!IsDetained(new StationRecordKey(copied, station.Value)))
            return false;

        captured = true;
        return true;
    }

    private bool IsDetained(StationRecordKey key)
        => _stationRecords.TryGetRecord<CriminalRecord>(key, out var record)
            && record.Status == SecurityStatus.Detained;
}

/// <summary>
/// Formats CMB-branded faxes so every colony antag fax to the CMB looks identical.
/// </summary>
public static class ColonyCmbFax
{
    /// <summary>
    /// Fax group every CMB fax machine carries; the single root for CMB fax targeting.
    /// </summary>
    public const string MarshalBureauFaxGroup = "marshal-bureau";

    /// <summary>
    /// CMB letterhead paper every CMB fax prints on.
    /// </summary>
    public const string CmbPaperPrototype = "CMUPaperCMB";

    private const string Underline = "[color=#134975]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]";

    /// <summary>
    /// Builds a full CMB fax. <paramref name="middle"/> is inserted between the body and the
    /// signature, e.g. the runaway synth's suspect list.
    /// </summary>
    public static string Build(string heading, string body, string middle = "")
        => "[color=#383838]█[/color][color=#ffffff]░░[/color][color=#8c0000]█ [color=#383838]█▄[/color] █ [/color][head=3]Colonial Marshall Bureau[/head]\n\n"
           + "[color=#383838]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/color]\n"
           + "[color=#8c0000]▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀[/color]\n\n"
           + $"[head=2][color=goldenrod]{heading}[/color][/head]\n\n"
           + "[bold]To:[/bold] [italic]CMB Office Staff[/italic]\n"
           + "[bold]From:[/bold] [bold]CMB Sectoral HQ[/bold]\n"
           + Underline + "\n"
           + "Sheriff,\n"
           + $"  {body}\n\n"
           + middle
           + "Signed,\n"
           + "[color=#dfc189][bolditalic]Regional HQ[/bolditalic][/color]\n"
           + Underline;
}
