using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.Paper;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server.CMU14.Round.Antags.ColonyBounty;

/// <summary>
/// Prints public wanted posters from a criminal records console: alias, bounty and witness
/// description only. Posters never carry prints or DNA; those stay in the console room.
/// </summary>
public sealed partial class WantedPosterSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, GetVerbsEvent<Verb>>(AddPosterVerb);
    }

    private void AddPosterVerb(Entity<CriminalRecordsConsoleComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("wanted-poster-verb-text"),
            Act = () => PrintPosters(ent, user),
        });
    }

    private void PrintPosters(Entity<CriminalRecordsConsoleComponent> ent, EntityUid user)
    {
        if (!_access.IsAllowed(user, ent))
        {
            _popup.PopupEntity(Loc.GetString("criminal-records-permission-denied"), ent, user);
            return;
        }

        var station = _station.GetOwningStation(ent);
        if (station == null)
            return;

        var count = 0;
        foreach (var (key, record) in _stationRecords.GetRecordsOfType<CriminalRecord>(station.Value))
        {
            if (record.Status != SecurityStatus.Wanted || record.Bounty <= 0)
                continue;

            if (!_stationRecords.TryGetRecord<GeneralStationRecord>(
                    new StationRecordKey(key, station.Value), out var general))
                continue;

            var paper = Spawn(ColonyCmbFax.CmbPaperPrototype, Transform(ent).Coordinates);
            if (!TryComp<PaperComponent>(paper, out var paperComp))
                continue;

            _paper.SetContent((paper, paperComp), Loc.GetString("wanted-poster-content",
                ("name", general.Name),
                ("bounty", record.Bounty),
                ("reason", record.Reason ?? string.Empty)));
            count++;
        }

        _popup.PopupEntity(Loc.GetString(count == 0
            ? "wanted-poster-none"
            : "wanted-poster-printed", ("count", count)), ent, user);
    }
}
