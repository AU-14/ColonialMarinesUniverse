// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server._RMC14.Dropship;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.WorldMarket;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Government;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.WorldMarket;
using Content.Shared.Mobs.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Government;

// The trade shuttle (see ColonyWorldTrade.cs). The heads pick a landing zone and a city at the console of the foreman and call the shuttle: the map of an existing shuttle is loaded, the goods that were ordered are put
// on it, and it flies to the zone. A shuttle that is on a zone holds it (the zone knows its ship), so no other shuttle may land there. It stays a time while the colonists unload it and put on it what they sell, and
// then it goes back (it will not go while there are people aboard), the map is cleared, and when the time of the road to the city is out the goods are paid for: 70% to the budget, 30% to the colonists who put them
// on. One shuttle serves the colony.
public sealed partial class ColonyGovernmentSystem
{
    public const string TradeShuttlePath = "/Maps/CMU14/Dropships/rmc_ert_pmc_shuttle.yml";
    public const string ShuttleReturnProto = "CMDropshipDestinationThirdPartyReturn";
    public const int LandedMinutes = 5;
    public const int AwayMinutes = 3;
    public const float ShuttleStartup = 4f;
    public const float ShuttleTravel = 15f;
    public const int FlightTimeoutSeconds = 150;

    [Dependency] private readonly DropshipSystem _dropship = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly Content.Server.Stack.StackSystem _stacks = default!;

    #region Calling

    // The shuttle is called: the zone must be free, a city must be picked, and the shuttle must be at rest.
    private void OnCallShuttle(Entity<ForemanConsoleComponent> console, ref CallShuttleMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        comp.ShuttleFailure = string.Empty;
        if (comp.Phase != ShuttlePhase.Idle || TradeCityInfo(comp) == null)
        {
            Refresh();
            return;
        }

        if (comp.ShuttleZone is not { } zone || !TryComp(zone, out DropshipDestinationComponent? destination) || destination.Ship != null)
        {
            comp.ShuttleFailure = Loc.GetString("rumc-world-fail-zone");
            Refresh();
            return;
        }

        if (!TryLoadShuttle(comp, zone, out var shuttle, out var navigation, out var returnMarker))
        {
            comp.ShuttleFailure = Loc.GetString("rumc-world-fail-load");
            ClearShuttle(comp);
            Refresh();
            return;
        }

        comp.Shuttle = shuttle;
        comp.ShuttleReturn = returnMarker;
        SpawnCart(comp, navigation.Owner);

        if (!TryFlyTo(navigation, zone, ShuttleStartup, ShuttleTravel))
        {
            comp.ShuttleFailure = Loc.GetString("rumc-world-fail-flight");
            AbortCall(comp);
            Refresh();
            return;
        }

        comp.Phase = ShuttlePhase.Calling;
        comp.ShuttleUntil = _timing.CurTime + TimeSpan.FromSeconds(FlightTimeoutSeconds);
        Refresh();
    }

    // Starts the flight and tells whether it really began: the call returns true or false, but the setup of the flight may still be vetoed, so what is left is looked at.
    private bool TryFlyTo(Entity<DropshipNavigationComputerComponent> navigation, EntityUid destination, float startup, float travel)
    {
        var ship = Transform(navigation).GridUid;
        try
        {
            _dropship.FlyTo(navigation, destination, null, startupTime: startup, hyperspaceTime: travel);
        }
        catch (Exception exception)
        {
            Log.Warning($"Trade shuttle flight failed to start: {exception}");
            return false;
        }

        return ship is { } grid && HasComp<FTLComponent>(grid);
    }

    // The map of the shuttle is loaded and made ready: the grid, its navigation console, and a mark on its own map to come back to. It is only for trade, so it has no faction that keeps it from the zone.
    private bool TryLoadShuttle(ColonyGovernmentComponent comp, EntityUid zone, out EntityUid shuttle, out Entity<DropshipNavigationComputerComponent> navigation, out EntityUid returnMarker)
    {
        shuttle = default;
        navigation = default;
        returnMarker = default;

        try
        {
            if (!_mapLoader.TryLoadGeneric(new ResPath(TradeShuttlePath), out var result, new MapLoadOptions
                {
                    DeserializationOptions = DeserializationOptions.Default with
                    {
                        InitializeMaps = true,
                        LogOrphanedGrids = false,
                    },
                }))
            {
                return false;
            }

            comp.ShuttleRoots.UnionWith(result.RootNodes);
            foreach (var map in result.Maps)
                comp.ShuttleRoots.Add(map.Owner);

            foreach (var grid in result.Grids)
            {
                shuttle = grid.Owner;
                break;
            }

            if (shuttle == default || !TryFindNavigation(shuttle, out navigation))
                return false;

            EnsureComp<TradeShuttleComponent>(shuttle);
            RemComp<WhitelistedShuttleComponent>(navigation.Owner);

            var returnCoords = _transform.ToMapCoordinates(Transform(shuttle).Coordinates);
            returnMarker = Spawn(ShuttleReturnProto, returnCoords);
            EnsureComp<ThirdPartyDropshipReturnDestinationComponent>(returnMarker).Shuttle = shuttle;
            _dropship.SetDestinationShip(returnMarker, shuttle);
            _dropship.SetDestinationHome(returnMarker, true);
            EnsureComp<DropshipComponent>(shuttle);
            _dropship.SetDropshipDestination(shuttle, returnMarker);
            return true;
        }
        catch (Exception exception)
        {
            Log.Warning($"Trade shuttle could not be prepared: {exception}");
            return false;
        }
    }

    private bool TryFindNavigation(EntityUid shuttle, out Entity<DropshipNavigationComputerComponent> navigation)
    {
        var query = EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid == shuttle)
            {
                navigation = (uid, comp);
                return true;
            }
        }

        navigation = default;
        return false;
    }

    // What was ordered is put on the shuttle, next to the console: it is a stack for a good of a stack, and a thing for the other kind.
    private void SpawnCart(ColonyGovernmentComponent comp, EntityUid at)
    {
        var coords = Transform(at).Coordinates;
        foreach (var line in comp.Cart)
        {
            if (!_proto.TryIndex<ColonyWorldGoodPrototype>(line.Good, out var good))
                continue;

            if (good.Stack is { } stackId)
            {
                _stacks.SpawnMultipleAtPosition(stackId, line.Amount, coords);
            }
            else if (good.Spawn is { } spawn)
            {
                for (var i = 0; i < line.Amount; i++)
                    Spawn(spawn, coords);
            }
        }
    }

    // A call that did not come to the zone: the orders are given back, and the map is cleared.
    private void AbortCall(ColonyGovernmentComponent comp)
    {
        foreach (var line in comp.Cart.ToList())
            Refund(comp, line);

        ClearShuttle(comp);
        comp.Phase = ShuttlePhase.Idle;
    }

    private void ClearShuttle(ColonyGovernmentComponent comp)
    {
        foreach (var root in comp.ShuttleRoots)
        {
            if (Exists(root))
                QueueDel(root);
        }

        comp.ShuttleRoots.Clear();
        if (comp.Shuttle is { } shuttle && Exists(shuttle))
            QueueDel(shuttle);

        if (comp.ShuttleReturn is { } marker && Exists(marker))
            QueueDel(marker);

        comp.Shuttle = null;
        comp.ShuttleReturn = null;
    }

    #endregion

    #region Landing and leaving

    private void OnFtlCompleted(ref FTLCompletedEvent args)
    {
        if (!TryGovernment(out var government) || government.Comp.Shuttle != args.Entity)
            return;

        var comp = government.Comp;
        var now = _timing.CurTime;
        switch (comp.Phase)
        {
            case ShuttlePhase.Calling:
                // It has come down: what was ordered is delivered, and the colonists have a time to unload it and to load what they sell.
                comp.Cart.Clear();
                comp.Phase = ShuttlePhase.Landed;
                comp.ShuttleUntil = now + TimeSpan.FromMinutes(LandedMinutes);
                break;
            case ShuttlePhase.Departing:
                // It is on its own map again: it is cleared when no one is aboard, and the goods are paid for when the road is done.
                comp.Phase = ShuttlePhase.Away;
                comp.ShuttleUntil = now + TimeSpan.FromMinutes(AwayMinutes);
                break;
            default:
                return;
        }

        Refresh();
    }

    private bool HasPassengers(EntityUid shuttle)
    {
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == shuttle)
                return true;
        }

        return false;
    }

    private void OnDepartShuttle(Entity<ForemanConsoleComponent> console, ref DepartShuttleMessage msg)
    {
        if (!TryGovernment(out var government) || government.Comp.Phase != ShuttlePhase.Landed)
            return;

        TryDepart(government.Comp);
        Refresh();
    }

    // The shuttle goes back with what was put on it. It does not go while there are people aboard.
    private bool TryDepart(ColonyGovernmentComponent comp)
    {
        comp.ShuttleFailure = string.Empty;
        if (comp.Shuttle is not { } shuttle || comp.ShuttleReturn is not { } marker || !TryFindNavigation(shuttle, out var navigation))
        {
            AbortLanded(comp);
            return false;
        }

        if (HasPassengers(shuttle))
        {
            comp.ShuttleFailure = Loc.GetString("rumc-world-fail-passengers");
            return false;
        }

        // The goods that are paid for are the goods that are on the shuttle now: what was left lying on the floor goes with the shuttle and is lost.
        if (TradeCityInfo(comp) is { } city && AllShipment(comp).Count > 0)
            SettleShipment(comp, city);

        if (!TryFlyTo(navigation, marker, ShuttleStartup, ShuttleTravel))
        {
            comp.ShuttleFailure = Loc.GetString("rumc-world-fail-flight");
            return false;
        }

        comp.Phase = ShuttlePhase.Departing;
        comp.ShuttleUntil = _timing.CurTime + TimeSpan.FromSeconds(FlightTimeoutSeconds);
        return true;
    }

    // The goods that the colonists put on the shuttle are appraised: 70% of the sum is for the budget, 30% is shared by those who put them on, by the worth of what each put. It is paid when the road is done.
    private void SettleShipment(ColonyGovernmentComponent comp, WorldMarketCityInfo city)
    {
        var byOwner = new Dictionary<EntityUid, int>();
        var total = Appraise(comp, city, true, byOwner);
        AllShipment(comp, true);

        var players = (int) Math.Round(total * (1f - ColonyShare));
        var shares = new List<PayoutShare>();
        foreach (var (owner, worth) in byOwner)
        {
            var sum = total == 0 ? 0 : (int) Math.Round(players * (worth / (float) total));
            if (sum > 0 && Exists(owner))
                shares.Add(new PayoutShare(owner, MetaData(owner).EntityName, sum));
        }

        // What is not shared (the shares of the ones who are gone, the rounding) stays with the colony.
        var colony = total - shares.Sum(s => s.Sum);
        comp.Payouts.Add(new PendingPayout(_timing.CurTime + TimeSpan.FromMinutes(AwayMinutes), city.Name, colony, shares));
        comp.Shipment.Clear();
    }

    // The shuttle that has lost its console or its marker is not left: everything is cleared and the zone is free.
    private void AbortLanded(ColonyGovernmentComponent comp)
    {
        if (TradeCityInfo(comp) is { } city && AllShipment(comp).Count > 0)
            SettleShipment(comp, city);

        ClearShuttle(comp);
        comp.Phase = ShuttlePhase.Idle;
    }

    #endregion

    #region The clock

    private void UpdateShuttle(ColonyGovernmentComponent comp, TimeSpan now)
    {
        // The payments for the goods that have come to the city.
        foreach (var payout in comp.Payouts.Where(p => p.At <= now).ToList())
        {
            comp.Payouts.Remove(payout);
            if (payout.Colony > 0)
                _budget.AddToBudget(payout.Colony);

            foreach (var share in payout.Shares)
            {
                if (!Exists(share.Owner) || !Pay(share.Owner, share.Sum))
                    _budget.AddToBudget(share.Sum);
                else
                    _popup.PopupEntity(Loc.GetString("rumc-world-paid", ("sum", share.Sum), ("city", payout.City)), share.Owner, share.Owner);
            }
        }

        switch (comp.Phase)
        {
            case ShuttlePhase.Calling:
                // A flight that has not ended in time is given up.
                if (now >= comp.ShuttleUntil)
                {
                    comp.ShuttleFailure = Loc.GetString("rumc-world-fail-flight");
                    AbortCall(comp);
                }

                break;

            case ShuttlePhase.Landed:
                // The time on the zone is out: it goes, if no one is aboard (the check is made again every few seconds).
                if (now >= comp.ShuttleUntil && !TryDepart(comp))
                    comp.ShuttleUntil = now + TimeSpan.FromSeconds(10);

                break;

            case ShuttlePhase.Departing:
                if (now >= comp.ShuttleUntil)
                {
                    ClearShuttle(comp);
                    comp.Phase = ShuttlePhase.Away;
                    comp.ShuttleUntil = now + TimeSpan.FromMinutes(AwayMinutes);
                }

                break;

            case ShuttlePhase.Away:
                // The shuttle is cleared once it is back on its own map and empty, then the colony may call another.
                if (comp.Shuttle != null && !HasPassengers(comp.Shuttle.Value))
                    ClearShuttle(comp);

                if (now >= comp.ShuttleUntil && comp.Shuttle == null)
                {
                    comp.Phase = ShuttlePhase.Idle;
                    comp.TradeCity = string.Empty;
                }

                break;
        }
    }

    #endregion
}
