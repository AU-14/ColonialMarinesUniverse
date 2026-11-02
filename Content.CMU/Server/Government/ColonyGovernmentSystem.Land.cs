// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Construction;
using Content.Shared.CMU14.Government;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Government;

// The land of the city. The mayor marks out zones with a surveyor's tablet (housing, trade, production) by standing in the middle of the place and picking the verb; the people who want to build come
// to the console and ask for a plot, and the mayor gives it (or not) in the window: a plot is five tiles by five in a zone of the kind that was asked for, rented for a sum out of the bank account of
// the holder every cycle (the land tax is added to the rent). With the enforcement on, the building menu is closed to the one who would build outside the zones (unless he has the permit of the mayor, which
// is a licence), or on the plot of another.
public sealed partial class ColonyGovernmentSystem
{
    public const string SurveyorProto = "RUMCSurveyorTablet";
    public const string BuildPermit = "RUMCLicenseBuild";
    public const string DeedCardProto = "RUMCCityDeedCard";
    public const int MaxZones = 8;
    public const int MaxTablets = 2;
    public const int SurveyCost = 150;
    public const int RentCycleSeconds = 180;
    public const int MaxRequests = 10;
    public const int MaxPlotsPerPerson = 2;
    public const int MaxTax = 20;

    // How many rents a holder may miss before the plot is taken back.
    public const int MissedLimit = 2;

    private static readonly int[] SurveySizes = { 5, 10, 15 };

    #region The marks

    // The tablet: its verbs mark a zone where the mayor stands, change the size of the next mark, or take off the zone he stands in.
    private void OnTabletVerbs(Entity<SurveyorTabletComponent> tablet, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;

        foreach (var kind in _proto.EnumeratePrototypes<ColonyZoneKindPrototype>().OrderBy(k => k.Kind))
        {
            var zoneKind = kind.Kind;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("rumc-land-verb-mark", ("kind", Loc.GetString(kind.Name)), ("size", tablet.Comp.Size)),
                Priority = 4,
                Act = () => MarkZone(tablet, user, zoneKind),
            });
        }

        foreach (var size in SurveySizes)
        {
            var picked = size;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("rumc-land-verb-size", ("size", picked)),
                Priority = 3,
                Act = () =>
                {
                    tablet.Comp.Size = picked;
                    Dirty(tablet);
                    _popup.PopupEntity(Loc.GetString("rumc-land-size-set", ("size", picked)), tablet, user);
                },
            });
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("rumc-land-verb-unmark"),
            Priority = 2,
            Act = () => UnmarkZone(user),
        });
    }

    private void OnTabletInit(Entity<SurveyorTabletComponent> tablet, ref MapInitEvent args)
    {
        SyncTablets();
    }

    // The zones that are marked go to every tablet, so that the client of the one who holds it can draw them over the ground.
    private void SyncTablets()
    {
        var tablets = EntityQueryEnumerator<SurveyorTabletComponent>();
        var zones = TryGovernment(out var government) ? government.Comp.Zones : new List<LandZone>();

        while (tablets.MoveNext(out var uid, out var tablet))
        {
            tablet.Marks = zones.Select(z => new SurveyorMark(GetNetEntity(z.Grid), z.Min, z.Size, z.Kind)).ToList();
            tablet.Full = zones.Count >= MaxZones;
            Dirty(uid, tablet);
        }
    }

    private void OnTabletExamined(Entity<SurveyorTabletComponent> tablet, ref ExaminedEvent args)
    {
        var text = Loc.GetString("rumc-land-tablet-examine", ("size", tablet.Comp.Size));
        if (TryGovernment(out var government) && ZoneAt(government.Comp, _transform.GetMapCoordinates(args.Examiner)) is { } zone)
            text += " " + Loc.GetString("rumc-land-tablet-in-zone", ("zone", ZoneName(zone)));

        args.PushMarkup(text);
    }

    private void MarkZone(Entity<SurveyorTabletComponent> tablet, EntityUid user, ZoneKind kind)
    {
        var comp = EnsureGovernment().Comp;
        if (Transform(user).GridUid is not { } grid)
        {
            _popup.PopupEntity(Loc.GetString("rumc-land-no-grid"), tablet, user);
            return;
        }

        if (comp.Zones.Count >= MaxZones)
        {
            _popup.PopupEntity(Loc.GetString("rumc-land-too-many", ("max", MaxZones)), tablet, user);
            return;
        }

        if (_budget.GetBudget() < SurveyCost)
        {
            _popup.PopupEntity(Loc.GetString("rumc-land-no-money", ("cost", SurveyCost)), tablet, user);
            return;
        }

        var size = tablet.Comp.Size;
        var point = Vector2.Transform(_transform.GetWorldPosition(user), _transform.GetInvWorldMatrix(grid));
        var min = new Vector2i((int) MathF.Floor(point.X) - size / 2, (int) MathF.Floor(point.Y) - size / 2);

        foreach (var other in comp.Zones.Where(z => z.Grid == grid))
        {
            if (min.X < other.Min.X + other.Size && other.Min.X < min.X + size && min.Y < other.Min.Y + other.Size && other.Min.Y < min.Y + size)
            {
                _popup.PopupEntity(Loc.GetString("rumc-land-overlap", ("zone", ZoneName(other))), tablet, user);
                return;
            }
        }

        _budget.AddToBudget(-SurveyCost);
        var zone = new LandZone(comp.NextZoneId++, kind, grid, min, size);
        comp.Zones.Add(zone);

        _popup.PopupEntity(Loc.GetString("rumc-land-marked", ("zone", ZoneName(zone)), ("size", size)), tablet, user);
        Refresh();
    }

    // The zone that the mayor stands in is taken off, and the plots in it go with it.
    private void UnmarkZone(EntityUid user)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        if (ZoneAt(comp, _transform.GetMapCoordinates(user)) is not { } zone)
        {
            _popup.PopupEntity(Loc.GetString("rumc-land-not-in-zone"), user, user);
            return;
        }

        RemoveZone(comp, zone);
        _popup.PopupEntity(Loc.GetString("rumc-land-unmarked", ("zone", ZoneName(zone))), user, user);
        Refresh();
    }

    private void RemoveZone(ColonyGovernmentComponent comp, LandZone zone)
    {
        CancelAuctionsInZone(comp, zone.Id);
        foreach (var plot in comp.Plots.Where(p => p.Zone == zone.Id).ToList())
            TakePlotBack(comp, plot, false);

        comp.Zones.Remove(zone);
    }

    private void OnRemoveZone(Entity<ColonyGovernmentConsoleComponent> console, ref RemoveZoneMessage msg)
    {
        var zoneId = msg.Zone;
        if (!TryGovernment(out var government) || government.Comp.Zones.FirstOrDefault(z => z.Id == zoneId) is not { } zone)
            return;

        RemoveZone(government.Comp, zone);
        Refresh();
    }

    private void OnIssueSurveyor(Entity<ColonyGovernmentConsoleComponent> console, ref IssueSurveyorMessage msg)
    {
        var tablets = 0;
        var existing = EntityQueryEnumerator<SurveyorTabletComponent>();
        while (existing.MoveNext(out _, out _))
            tablets++;

        if (tablets >= MaxTablets)
            return;

        var tablet = Spawn(SurveyorProto, Transform(console.Owner).Coordinates);
        _hands.TryPickupAnyHand(msg.Actor, tablet);
    }

    #endregion

    #region The plots

    // The people ask at the console: the request goes to the window of the mayor.
    private void OnConsoleVerbs(Entity<ColonyGovernmentConsoleComponent> console, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var person = args.User;
        var consoleUid = console.Owner;

        foreach (var kind in _proto.EnumeratePrototypes<ColonyZoneKindPrototype>().OrderBy(k => k.Kind))
        {
            var zoneKind = kind.Kind;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("rumc-land-verb-request", ("kind", Loc.GetString(kind.Name))),
                Priority = 2,
                Act = () => RequestPlot(consoleUid, person, zoneKind),
            });
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("rumc-land-verb-zones"),
            Priority = 1,
            Act = () => ShowZones(consoleUid, person),
        });
    }

    private void RequestPlot(EntityUid console, EntityUid person, ZoneKind kind)
    {
        var comp = EnsureGovernment().Comp;
        var name = MetaData(person).EntityName;

        if (comp.Requests.Any(r => r.Person == person) || comp.Requests.Count >= MaxRequests)
        {
            _popup.PopupEntity(Loc.GetString("rumc-land-request-refused"), console, person);
            return;
        }

        if (comp.Plots.Count(p => p.Holder == name) >= MaxPlotsPerPerson)
        {
            _popup.PopupEntity(Loc.GetString("rumc-land-request-has-plots", ("max", MaxPlotsPerPerson)), console, person);
            return;
        }

        comp.Requests.Add(new PlotRequest(comp.NextRequestId++, person, name, kind));
        _popup.PopupEntity(Loc.GetString("rumc-land-request-sent", ("kind", KindName(kind))), console, person);
        Refresh();
    }

    // What is marked, for the one who asks: where the zones are and what they are for.
    private void ShowZones(EntityUid console, EntityUid person)
    {
        if (!TryGovernment(out var government) || government.Comp.Zones.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("rumc-land-zones-empty"), console, person);
            return;
        }

        var lines = government.Comp.Zones.Select(z => Loc.GetString("rumc-land-zones-line", ("zone", ZoneName(z)), ("x", z.Min.X), ("y", z.Min.Y), ("size", z.Size)));
        _popup.PopupEntity(string.Join("\n", lines), console, person);
    }

    private void OnAllotPlot(Entity<ColonyGovernmentConsoleComponent> console, ref AllotPlotMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var requestId = msg.Request;
        var request = comp.Requests.FirstOrDefault(r => r.Id == requestId);
        if (request == null)
            return;

        if (!Exists(request.Person))
        {
            comp.Requests.Remove(request);
            Refresh();
            return;
        }

        if (FreeCell(comp, request.Kind) is not { } free)
            return;

        var plot = new LandPlot(comp.NextPlotId++, free.zone.Id, free.cell, request.Name, request.Person, _timing.CurTime);
        comp.Plots.Add(plot);
        comp.Requests.Remove(request);

        // The deed is printed and put into the hands of the mayor, who gives it to the holder.
        plot.Card = PrintDeed(plot, free.zone, console.Owner);
        _hands.TryPickupAnyHand(msg.Actor, plot.Card.Value);

        _popup.PopupEntity(Loc.GetString("rumc-land-allotted", ("zone", ZoneName(free.zone)), ("x", PlotCorner(free.zone, free.cell).X), ("y", PlotCorner(free.zone, free.cell).Y)),
            request.Person, request.Person);
        Refresh();
    }

    // The deed of a plot: a card that says where it is and whose. It has no end of its own; it is put down by the console.
    private EntityUid PrintDeed(LandPlot plot, LandZone zone, EntityUid console)
    {
        var corner = PlotCorner(zone, plot.Cell);
        var card = Spawn(DeedCardProto, Transform(console).Coordinates);
        if (TryComp(card, out CityLicenseCardComponent? cardComp))
        {
            cardComp.Serial = $"P-{plot.Id}";
            cardComp.TypeName = Loc.GetString("rumc-land-card-type", ("zone", ZoneName(zone)), ("x", corner.X), ("y", corner.Y));
            cardComp.Holder = plot.Holder;
            cardComp.Permanent = true;
        }

        return card;
    }

    private void OnDeclineRequest(Entity<ColonyGovernmentConsoleComponent> console, ref DeclineRequestMessage msg)
    {
        var requestId = msg.Request;
        if (!TryGovernment(out var government) || government.Comp.Requests.FirstOrDefault(r => r.Id == requestId) is not { } request)
            return;

        government.Comp.Requests.Remove(request);
        if (Exists(request.Person))
            _popup.PopupEntity(Loc.GetString("rumc-land-declined"), request.Person, request.Person);

        Refresh();
    }

    private void OnRevokePlot(Entity<ColonyGovernmentConsoleComponent> console, ref RevokePlotMessage msg)
    {
        var plotId = msg.Plot;
        if (!TryGovernment(out var government) || government.Comp.Plots.FirstOrDefault(p => p.Id == plotId) is not { } plot)
            return;

        TakePlotBack(government.Comp, plot, true);
        Refresh();
    }

    private void TakePlotBack(ColonyGovernmentComponent comp, LandPlot plot, bool tell)
    {
        comp.Plots.Remove(plot);
        if (plot.Card is { } card && TryComp(card, out CityLicenseCardComponent? cardComp))
            cardComp.Revoked = true;

        if (tell && plot.HolderEntity is { } holder && Exists(holder))
            _popup.PopupEntity(Loc.GetString("rumc-land-taken-back", ("plot", plot.Id)), holder, holder);
    }

    private void OnSetLandTax(Entity<ColonyGovernmentConsoleComponent> console, ref SetLandTaxMessage msg)
    {
        EnsureGovernment().Comp.LandTax = Math.Clamp(msg.Tax, 0, MaxTax);
        Refresh();
    }

    private void OnSetEnforce(Entity<ColonyGovernmentConsoleComponent> console, ref SetEnforceMessage msg)
    {
        EnsureGovernment().Comp.Enforce = msg.Enforce;
        Refresh();
    }

    // The first free plot in a zone of the kind that was asked for.
    private (LandZone zone, Vector2i cell)? FreeCell(ColonyGovernmentComponent comp, ZoneKind kind)
    {
        foreach (var zone in comp.Zones.Where(z => z.Kind == kind && Exists(z.Grid)).OrderBy(z => z.Id))
        {
            var cells = zone.Size / LandPlot.Side;
            for (var x = 0; x < cells; x++)
            {
                for (var y = 0; y < cells; y++)
                {
                    var cell = new Vector2i(x, y);
                    if (!comp.Plots.Any(p => p.Zone == zone.Id && p.Cell == cell) && !comp.Auctions.Any(a => a.Zone == zone.Id && a.Cell == cell))
                        return (zone, cell);
                }
            }
        }

        return null;
    }

    private static Vector2i PlotCorner(LandZone zone, Vector2i cell)
    {
        return zone.Min + cell * LandPlot.Side;
    }

    #endregion

    #region The rent

    private void UpdateLand(ColonyGovernmentComponent comp, TimeSpan now)
    {
        // The people who asked and are gone, and the zones whose grid is gone, are forgotten.
        comp.Requests.RemoveAll(r => !Exists(r.Person));

        if (comp.NextRent == TimeSpan.Zero)
            comp.NextRent = now + TimeSpan.FromSeconds(RentCycleSeconds);

        if (now < comp.NextRent)
            return;

        comp.NextRent += TimeSpan.FromSeconds(RentCycleSeconds);

        foreach (var plot in comp.Plots.ToList())
        {
            var rent = RentOf(comp, plot);
            if (plot.HolderEntity is { } holder && Exists(holder) && _bank.FindOwnedCard(holder) is { } owned && owned.card.AccountBalance >= rent)
            {
                owned.card.AccountBalance -= rent;
                Dirty(owned.uid, owned.card);
                _budget.AddToBudget(rent);
                plot.Missed = 0;
                AddProgress(comp, holder, PlanKind.Rent, 1, comp.Zones.FirstOrDefault(z => z.Id == plot.Zone)?.Kind);
                continue;
            }

            plot.Missed++;
            if (plot.Missed >= MissedLimit)
            {
                TakePlotBack(comp, plot, true);
                continue;
            }

            if (plot.HolderEntity is { } missed && Exists(missed))
                _popup.PopupEntity(Loc.GetString("rumc-land-rent-missed", ("plot", plot.Id), ("rent", rent)), missed, missed);
        }
    }

    private int RentOf(ColonyGovernmentComponent comp, LandPlot plot)
    {
        var zone = comp.Zones.FirstOrDefault(z => z.Id == plot.Zone);
        var baseRent = zone == null ? 0 : KindPrototype(zone.Kind)?.RentPerCycle ?? 0;
        return Math.Max(0, baseRent * (100 + comp.LandTax) / 100);
    }

    #endregion

    #region The enforcement

    // The building menu asks here before it starts a work: with the enforcement on, a thing is built in a zone, on a plot of the one who builds it or on no plot, or by the one who has the permit of the mayor
    // (and nowhere on the plot of another). It is asked only on the maps where a console of the administration is.
    private void OnConstructionAttempt(ref RMCConstructionAttemptEvent ev)
    {
        if (ev.Cancelled || ev.User is not { } user || !TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var map = _transform.ToMapCoordinates(ev.Location);
        if (!OnCityMap(map.MapId))
            return;

        var zone = ZoneAt(comp, map);

        if (!comp.Enforce)
            return;

        var name = MetaData(user).EntityName;
        if (zone == null)
        {
            if (!HasBuildPermit(comp, name))
            {
                ev.Popup = Loc.GetString("rumc-land-denied-outside");
                ev.Cancelled = true;
            }

            return;
        }

        if (PlotAt(comp, zone, map) is { } plot && plot.Holder != name)
        {
            ev.Popup = Loc.GetString("rumc-land-denied-plot", ("holder", plot.Holder));
            ev.Cancelled = true;
        }
    }

    private bool OnCityMap(MapId map)
    {
        var consoles = EntityQueryEnumerator<ColonyGovernmentConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == map)
                return true;
        }

        return false;
    }

    private bool HasBuildPermit(ColonyGovernmentComponent comp, string name)
    {
        var now = _timing.CurTime;
        return comp.Licenses.Values.Any(r => r.Type == BuildPermit && !r.Revoked && r.Holder == name && r.ExpiresAt > now);
    }

    #endregion

    #region Where things are

    private LandZone? ZoneAt(ColonyGovernmentComponent comp, MapCoordinates map)
    {
        foreach (var zone in comp.Zones)
        {
            if (GridPoint(zone, map) is { } point && point.X >= zone.Min.X && point.X < zone.Min.X + zone.Size && point.Y >= zone.Min.Y && point.Y < zone.Min.Y + zone.Size)
                return zone;
        }

        return null;
    }

    private LandPlot? PlotAt(ColonyGovernmentComponent comp, LandZone zone, MapCoordinates map)
    {
        if (GridPoint(zone, map) is not { } point)
            return null;

        var cell = new Vector2i((int) MathF.Floor((point.X - zone.Min.X) / LandPlot.Side), (int) MathF.Floor((point.Y - zone.Min.Y) / LandPlot.Side));
        return comp.Plots.FirstOrDefault(p => p.Zone == zone.Id && p.Cell == cell);
    }

    // A place of the world in the tiles of the grid of a zone (null if the grid is gone or on another map).
    private Vector2? GridPoint(LandZone zone, MapCoordinates map)
    {
        if (!Exists(zone.Grid) || Transform(zone.Grid).MapID != map.MapId)
            return null;

        return Vector2.Transform(map.Position, _transform.GetInvWorldMatrix(zone.Grid));
    }

    private ColonyZoneKindPrototype? KindPrototype(ZoneKind kind)
    {
        return _proto.EnumeratePrototypes<ColonyZoneKindPrototype>().FirstOrDefault(p => p.Kind == kind);
    }

    private string KindName(ZoneKind kind)
    {
        return KindPrototype(kind) is { } proto ? Loc.GetString(proto.Name) : kind.ToString();
    }

    private string ZoneName(LandZone zone)
    {
        return Loc.GetString("rumc-land-zone-name", ("kind", KindName(zone.Kind)), ("id", zone.Id));
    }

    #endregion

    #region The state for the window

    private LandState BuildLand(ColonyGovernmentComponent comp)
    {
        var kinds = _proto.EnumeratePrototypes<ColonyZoneKindPrototype>()
            .OrderBy(k => k.Kind)
            .Select(k => new ZoneKindEntry(k.Kind, Loc.GetString(k.Name), Loc.GetString(k.Description), k.RentPerCycle))
            .ToList();

        var zones = comp.Zones
            .Select(z => new ZoneEntry(z.Id, ZoneName(z), z.Size, z.Min.X, z.Min.Y, comp.Plots.Count(p => p.Zone == z.Id), (z.Size / LandPlot.Side) * (z.Size / LandPlot.Side)))
            .ToList();

        var plots = new List<PlotEntry>();
        var income = 0;
        foreach (var plot in comp.Plots)
        {
            var zone = comp.Zones.FirstOrDefault(z => z.Id == plot.Zone);
            if (zone == null)
                continue;

            var rent = RentOf(comp, plot);
            income += rent;
            var corner = PlotCorner(zone, plot.Cell);
            plots.Add(new PlotEntry(plot.Id, ZoneName(zone), plot.Holder, rent, plot.Missed, $"X {corner.X}, Y {corner.Y}"));
        }

        var requests = comp.Requests
            .Select(r => new RequestEntry(r.Id, r.Name, KindName(r.Kind), FreeCell(comp, r.Kind) != null))
            .ToList();

        _proto.TryIndex<ColonyLicensePrototype>(BuildPermit, out var permit);
        return new LandState(comp.LandTax, comp.Enforce, MaxZones, SurveyCost, RentCycleSeconds, income,
            permit == null ? string.Empty : Loc.GetString(permit.Name), permit?.Fee ?? 0, kinds, zones, plots, requests, comp.SalesTax);
    }

    #endregion
}
