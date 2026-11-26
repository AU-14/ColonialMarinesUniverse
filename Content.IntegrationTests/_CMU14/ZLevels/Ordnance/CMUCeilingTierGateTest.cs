#pragma warning disable RA0002 // The authored-roof fixture writes AreaGridComponent state directly; AreaSystem has no seeding API.
using System.Collections.Generic;
using System.Numerics;
using Content.Server._RMC14.Dropship.Weapon;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.ZLevels.Ordnance;

/// <summary>
/// The derived ceiling tier: open sky is free, a roof is opened one fire sequence at a time, caves take
/// only rock-penetrating munitions, and a breach turns a roof back into open sky.
/// </summary>
[TestFixture]
public sealed class CMUCeilingTierGateTest
{
    [Test]
    public async Task DerivedTierGatesAerialOrdnance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var map = entMan.System<SharedMapSystem>();
            var zLevels = entMan.System<CMUZLevelsSystem>();
            var areas = entMan.System<AreaSystem>();
            var weapons = entMan.System<DropshipWeaponSystem>();
            var breaches = entMan.System<DropshipZLevelBreachSystem>();
            var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();
            var plating = new Tile(tileDefinitions["Plating"].TileId);

            var caveMap = map.CreateMap(out var caveId);
            var surfaceMap = map.CreateMap(out var surfaceId);
            var skyMap = map.CreateMap(out var skyId);
            var network = zLevels.CreateZNetwork();

            try
            {
                var cave = map.CreateGridEntity(caveId);
                var surface = map.CreateGridEntity(surfaceId);
                var sky = map.CreateGridEntity(skyId);
                map.SetTile(cave, Vector2i.Zero, plating);
                map.SetTile(surface, Vector2i.Zero, plating);
                map.SetTile(surface, new Vector2i(10, 10), plating);
                map.SetTile(surface, new Vector2i(12, 12), plating);
                map.SetTile(sky, new Vector2i(10, 10), plating);

                // Scoping checkpoint: the distant solid tile must survive the column queries and breach carve.
                Assert.That(map.TryGetTileRef(surface.Owner, surface.Comp, new Vector2i(12, 12), out var farTile), Is.True);
                Assert.That(farTile.Tile, Is.EqualTo(plating), "surface (12,12) should start as plating.");

                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
                {
                    [caveMap] = -1,
                    [surfaceMap] = 0,
                    [skyMap] = 1,
                }), Is.True);

                var open = new EntityCoordinates(surface.Owner, new Vector2(0.5f, 0.5f));
                var roofed = new EntityCoordinates(surface.Owner, new Vector2(10.5f, 10.5f));
                var caveCoords = new EntityCoordinates(cave.Owner, new Vector2(0.5f, 0.5f));

                Assert.That(areas.CMUTryGetCeilingTier(open, out var authored), Is.EqualTo(0));
                Assert.That(authored, Is.False);
                Assert.That(areas.CMUTryGetCeilingTier(roofed, out _), Is.EqualTo(3));
                Assert.That(areas.CMUTryGetCeilingTier(caveCoords, out _), Is.EqualTo(4));

                // Open sky: everything flies.
                Assert.That(weapons.CanChinGunImpactAt(open, 0), Is.True);
                Assert.That(areas.CanOrbitalBombard(open, out _), Is.True);

                // Roof: pen 0 never, a non-breacher never, a breacher that cannot reach it never, a breacher that can does.
                Assert.That(weapons.CanChinGunImpactAt(roofed, 0), Is.False);
                Assert.That(weapons.CanChinGunImpactAt(roofed, 1, 0, false), Is.False);
                Assert.That(weapons.CanChinGunImpactAt(roofed, 0, 1, false), Is.False);
                Assert.That(weapons.CanChinGunImpactAt(roofed, 1, 1, false), Is.True);
                Assert.That(areas.CanOrbitalBombard(roofed, out _), Is.True);

                // Cave: nothing but rock-penetrating munitions, and only as deep as their rating.
                Assert.That(weapons.CanChinGunImpactAt(caveCoords, 5), Is.False);
                Assert.That(weapons.CanChinGunImpactAt(caveCoords, 5, 2, false), Is.False);
                Assert.That(weapons.CanChinGunImpactAt(caveCoords, 0, 2, true), Is.False);
                Assert.That(weapons.CanChinGunImpactAt(caveCoords, 5, 2, true), Is.True);
                Assert.That(areas.CanOrbitalBombard(caveCoords, out _), Is.False);
                Assert.That(areas.CanCAS(caveCoords), Is.False);

                // The derived roof tightens every aerial action, not only CAS. Tier 3 still takes OB, which lands on the roof.
                var roofedMap = new MapCoordinates(new Vector2(10.5f, 10.5f), surfaceId);
                Assert.That(areas.CanSupplyDrop(roofedMap), Is.False);
                Assert.That(areas.CanMedevac(roofed), Is.False);
                Assert.That(areas.CanMortarFire(roofed), Is.False);
                Assert.That(areas.CanSupplyDrop(new MapCoordinates(new Vector2(0.5f, 0.5f), caveId)), Is.False);
                Assert.That(areas.CanOrbitalBombard(open, out _), Is.True);

                // Scoping checkpoint: the column queries above must not have touched the tile.
                Assert.That(map.TryGetTileRef(surface.Owner, surface.Comp, new Vector2i(12, 12), out var preBreachTile), Is.True);
                Assert.That(preBreachTile.Tile, Is.EqualTo(plating), "surface (12,12) must still be plating before the carve.");

                // Breach-first: opening the roof makes the column open sky again.
                breaches.CarveBreach(new MapCoordinates(new Vector2(10.5f, 10.5f), skyId), 1);
                Assert.That(areas.CMUTryGetCeilingTier(roofed, out _), Is.EqualTo(0));
                Assert.That(weapons.CanChinGunImpactAt(roofed, 0), Is.True);

                // The breach touched only its own square: the nearest solid tile outside the ring is still solid.
                Assert.That(map.TryGetTileRef(surface.Owner, surface.Comp, new Vector2i(12, 12), out var caveCeiling), Is.True);
                Assert.That(caveCeiling.Tile, Is.EqualTo(plating));

                // An authored roof is absolute: refused for every munition, never carved.
                var authoredTile = new Vector2i(20, 20);
                map.SetTile(surface, authoredTile, plating);
                var areaGrid = entMan.EnsureComponent<AreaGridComponent>(surface.Owner);
                areaGrid.Areas[authoredTile] = "RMCAreaKutjevoInteriorColonyNorth";
                areaGrid.AreaEntities["RMCAreaKutjevoInteriorColonyNorth"] = entMan.Spawn("RMCAreaKutjevoInteriorColonyNorth", MapCoordinates.Nullspace);
                var authoredCoords = new EntityCoordinates(surface.Owner, new Vector2(20.5f, 20.5f));
                Assert.That(areas.CMUTryGetCeilingTier(authoredCoords, out var isAuthored), Is.EqualTo(3));
                Assert.That(isAuthored, Is.True);
                Assert.That(weapons.CanChinGunImpactAt(authoredCoords, 5, 2, true), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(network))
                    entMan.DeleteEntity(network);

                map.DeleteMap(skyId);
                map.DeleteMap(surfaceId);
                map.DeleteMap(caveId);
            }
        });

        await pair.CleanReturnAsync();
    }
}
