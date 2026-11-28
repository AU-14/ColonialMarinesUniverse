using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Ops.ThirdParty;

/// <summary>
///     A ship loaded by <see cref="ThirdPartySystem.TryLoadThreatDropship"/>, parked at its own deep-space return pad.
///     Until <see cref="ThirdPartySystem.AcceptThreatDropship"/> is called, disposing <c>Preparation</c>
///     deletes the whole load (maps, grids and the return pad).
/// </summary>
/// <param name="Grid">The main ship grid.</param>
/// <param name="Nav">The ship's navigation console.</param>
/// <param name="ReturnPad">The generated deep-space return pad the ship is parked on.</param>
/// <param name="Grids">The main grid plus any interior decks.</param>
/// <param name="LeaderSeats">Free passenger seats reserved for leader bodies.</param>
/// <param name="MemberSeats">The remaining free passenger seats.</param>
/// <param name="Preparation">Owns the load until accepted.</param>
public sealed record ThirdPartyDropshipLoad(
    EntityUid Grid,
    Entity<DropshipNavigationComputerComponent> Nav,
    EntityUid ReturnPad,
    IReadOnlyList<EntityUid> Grids,
    IReadOnlyList<EntityUid> LeaderSeats,
    IReadOnlyList<EntityUid> MemberSeats,
    IDisposable Preparation);

public sealed partial class ThirdPartySystem
{
    /// <summary>
    ///     Loads a ship the way third-party shuttles are loaded (grid, deep-space return pad, flight consoles and an
    ///     open, auto-returning navigation console) and splits its free passenger seats into leader and member seats.
    ///     The caller owns <see cref="ThirdPartyDropshipLoad.Preparation"/>: dispose it when done, after
    ///     <see cref="AcceptThreatDropship"/> if the load should be kept.
    /// </summary>
    /// <param name="path">The ship map or grid file.</param>
    /// <param name="navConsole">Navigation console spawned at DSPilot markers.</param>
    /// <param name="leaderSeatCount">How many of the free seats go to <see cref="ThirdPartyDropshipLoad.LeaderSeats"/>.</param>
    /// <param name="load">The loaded ship, or null on failure (nothing is left behind).</param>
    public bool TryLoadThreatDropship(ResPath path,
        EntProtoId navConsole,
        int leaderSeatCount,
        [NotNullWhen(true)] out ThirdPartyDropshipLoad? load)
    {
        load = null;
        var preparation = new ShuttlePreparation(_mapLoader);
        try
        {
            var options = DeserializationOptions.Default with { InitializeMaps = true };
            if (!TryLoadDropshipGrid(path, options, preparation, out var mainGridUid))
            {
                preparation.Dispose();
                return false;
            }

            var shipGrids = new List<EntityUid> { mainGridUid };
            var departure = Transform(mainGridUid).Coordinates;
            if (TryComp<MultiDeckDropshipComponent>(mainGridUid, out var ship))
            {
                if (!ship.DecksInitialized)
                {
                    _sawmill.Error($"[ThirdPartySystem] Threat dropship {path} decks did not initialize.");
                    preparation.Dispose();
                    return false;
                }

                // Return destinations mark ground level, not the cabin's Z level.
                departure = _multiDeck.GetGroundCoordinates(mainGridUid);
                foreach (var (offset, deck) in ship.Decks)
                {
                    if (!ship.ExteriorDecks.Contains(offset))
                        shipGrids.Add(deck);
                }
            }

            var returnPad = _entityManager.SpawnEntity("CMDropshipDestinationThirdPartyReturn",
                _transform.ToMapCoordinates(departure));
            // The return marker can live on a generated deck map outside the original load.
            preparation.Result!.Entities.Add(returnPad);
            EnsureComp<ThirdPartyDropshipReturnDestinationComponent>(returnPad).Shuttle = mainGridUid;
            EnsureComp<DropshipDestinationComponent>(returnPad);
            _sharedDropshipSystem.SetDestinationShip(returnPad, mainGridUid);
            _sharedDropshipSystem.SetDestinationHome(returnPad, true);

            EnsureComp<DropshipComponent>(mainGridUid);
            _sharedDropshipSystem.SetDropshipDestination(mainGridUid, returnPad);

            SpawnDropshipConsoles(mainGridUid, navConsole);
            // Threat ships always fly with an open, auto-returning console, whatever the grid file carries.
            foreach (var grid in shipGrids)
            {
                OpenNavComputersForThirdParty(grid);
            }

            if (!TryFindNavigation(shipGrids, out var nav))
            {
                _sawmill.Error($"[ThirdPartySystem] Threat dropship {path} has no navigation console.");
                preparation.Dispose();
                return false;
            }

            var seats = CollectDropshipSeats(shipGrids);
            var leaderSeats = seats.Take(Math.Clamp(leaderSeatCount, 0, seats.Count)).ToList();
            var memberSeats = seats.Skip(leaderSeats.Count).ToList();
            load = new ThirdPartyDropshipLoad(mainGridUid, nav, returnPad, shipGrids, leaderSeats, memberSeats,
                preparation);
            return true;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"[ThirdPartySystem] Failed to prepare threat dropship {path}: {ex}");
            preparation.Dispose();
            load = null;
            return false;
        }
    }

    /// <summary>Keeps a load from <see cref="TryLoadThreatDropship"/> when its preparation is disposed.</summary>
    public void AcceptThreatDropship(ThirdPartyDropshipLoad load)
    {
        if (load.Preparation is ShuttlePreparation preparation)
            preparation.Accepted = true;
    }

    /// <summary>Buckles a freshly spawned body into a ship seat. Returns false, leaving it standing, on failure.</summary>
    public bool TryBuckleIntoSeat(EntityUid body, EntityUid seat)
    {
        if (!HasComp<StrapComponent>(seat))
            return false;

        if (_buckle.TryBuckle(body, null, seat, popup: false))
            return true;

        _sawmill.Warning($"[ThirdPartySystem] {ToPrettyString(body)} could not buckle into {ToPrettyString(seat)}; leaving it standing.");
        return false;
    }

    private bool TryFindNavigation(List<EntityUid> grids, out Entity<DropshipNavigationComputerComponent> nav)
    {
        foreach (var grid in grids)
        {
            var children = Transform(grid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (TryComp<DropshipNavigationComputerComponent>(child, out var comp))
                {
                    nav = (child, comp);
                    return true;
                }
            }
        }

        nav = default;
        return false;
    }
}
