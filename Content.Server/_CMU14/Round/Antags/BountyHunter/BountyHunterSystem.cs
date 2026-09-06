using Content.Server.Antag;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared._CMU14.Round.Antags.BountyHunter;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Round.Antags.BountyHunter;

/// <summary>
/// Briefs the bounty hunter with every wanted record on the colony's books,
/// delayed so other antags' records exist first.
/// </summary>
public sealed partial class BountyHunterSystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BountyHunterComponent, ComponentStartup>(OnHunterSpawned);
    }

    private void OnHunterSpawned(EntityUid uid, BountyHunterComponent comp, ComponentStartup args)
    {
        comp.NextFax = _timing.CurTime + comp.FaxDelay;
    }

    public override void Update(float frameTime)
    {
        var enumerator = EntityManager.AllEntityQueryEnumerator<BountyHunterComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (comp.Faxed || _timing.CurTime < comp.NextFax)
                continue;

            comp.Faxed = true;
            _antag.SendBriefing(uid, BuildTargetList(uid, comp), Color.FromHex("#b0901b"), null);
        }
    }

    private string BuildTargetList(EntityUid hunter, BountyHunterComponent comp)
    {
        var lines = new List<string>();
        var station = _station.GetOwningStation(hunter);
        if (station != null)
        {
            foreach (var (key, record) in _stationRecords.GetRecordsOfType<CriminalRecord>(station.Value))
            {
                if (record.Status != SecurityStatus.Wanted || record.Bounty <= 0)
                    continue;

                var name = _stationRecords.TryGetRecord<GeneralStationRecord>(new StationRecordKey(key, station.Value), out var general)
                    ? general.Name
                    : "Unknown";
                lines.Add($"- {name}: {record.Bounty} credits ({record.Reason})");
            }
        }

        comp.TargetCount = lines.Count;
        return lines.Count == 0
            ? Loc.GetString("cmu-bounty-hunter-empty")
            : Loc.GetString("cmu-bounty-hunter-list") + "\n" + string.Join("\n", lines);
    }
}
