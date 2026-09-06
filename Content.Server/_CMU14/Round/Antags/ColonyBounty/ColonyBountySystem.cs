using Content.Server.AU14.ColonyEconomy;
using Content.Server.AU14.Systems;
using Content.Server.CriminalRecords.Systems;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Cuffs.Components;
using Content.Shared.CriminalRecords;
using Content.Shared.Forensics.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared._CMU14.Round.Antags.ColonyBounty;
using Robust.Shared.GameObjects;
namespace Content.Server._CMU14.Round.Antags.ColonyBounty;

/// <summary>
/// Shared bookkeeping for bounty-carrying colony antags: wanted record on spawn,
/// one-shot capture or death payout into the colony budget.
/// </summary>
public sealed partial class ColonyBountySystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly CriminalRecordsConsoleSystem _criminalRecordsConsole = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private readonly WantedSystem _wanted = default!;

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
                || !Resolved(uid, comp))
                continue;

            comp.Paid = true;
            comp.Captured = comp.CuffedCounts
                && EntityManager.GetComponentOrNull<CuffableComponent>(uid)?.CuffedHandCount > 0;
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
        var recordName = comp.RecordName ?? comp.RecordNamePrefix + name;
        var lookup = comp.AttachToOwnRecord ? name : recordName;
        StationRecordKey key;
        if (_stationRecords.GetRecordByName(station.Value, lookup) is { } id)
            key = new StationRecordKey(id, station.Value);
        else
        {
            key = _stationRecords.AddRecordEntry(station.Value, new GeneralStationRecord
            {
                Name = recordName,
                Fingerprint = comp.IncludePrints
                    ? EntityManager.GetComponentOrNull<FingerprintComponent>(uid)?.Fingerprint ?? "none found"
                    : null,
                DNA = comp.IncludeDna
                    ? EntityManager.GetComponentOrNull<DnaComponent>(uid)?.DNA ?? "none found"
                    : null,
            });
        }

        _stationRecords.AddRecordEntry<CriminalRecord>(key, new CriminalRecord
        {
            Bounty = comp.Bounty,
            Status = SecurityStatus.Wanted,
            Reason = comp.Reason,
            InitiatorName = "HQ",
            History = new List<CrimeHistory>(),
        }, null);

        _criminalRecordsConsole.AddScannedRecord(key);
    }

    private bool Resolved(EntityUid uid, ColonyBountyComponent comp)
    {
        if (comp.CuffedCounts
            && EntityManager.GetComponentOrNull<CuffableComponent>(uid)?.CuffedHandCount > 0)
            return true;

        return comp.DeadCounts
            && EntityManager.GetComponentOrNull<MobStateComponent>(uid)?.CurrentState
                is MobState.Dead or MobState.Invalid;
    }
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
