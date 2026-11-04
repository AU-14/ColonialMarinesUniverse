#pragma warning disable RA0002 // Tests configure area permissions directly.
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ShipWeapons;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.CMU14.ZLevels.Ordnance;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.ShipWeapons;

[TestFixture]
public sealed class CMURailcannonImpactTest : GameTest
{
    [Test]
    public async Task RailcannonMakesCircularHoleThroughNetworkAndLowersCeilingTier()
    {
        var cleanup = new List<EntityUid>();
        var levelTargets = new List<(EntityUid Center, EntityUid Edge, EntityUid Corner, EntityUid Area, MapId MapId)>();
        EntityCoordinates strikeCoordinates = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = SEntMan.System<SharedMapSystem>();
                var zLevels = SEntMan.System<CMUZLevelsSystem>();
                var areas = SEntMan.System<AreaSystem>();
                var network = zLevels.CreateZNetwork();
                cleanup.Add(network);

                var members = new Dictionary<EntityUid, int>();
                var tileId = Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
                for (var depth = -1; depth <= 1; depth++)
                {
                    var map = maps.CreateMap(out var mapId);
                    cleanup.Add(map);
                    var grid = maps.CreateGridEntity(mapId);
                    var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(grid);

                    for (var x = -3; x <= 3; x++)
                    for (var y = -3; y <= 3; y++)
                    {
                        var tile = new Vector2i(x, y);
                        maps.SetTile(grid, tile, new Tile(tileId));
                        areas.ReplaceArea(areaGrid, tile, "RMCAreaDesertDamInterior");
                    }

                    var area = SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]);
                    area.OB = area.CAS = area.Fulton = area.SupplyDrop = area.MortarFire = true;
                    area.MortarPlacement = area.Medevac = area.Paradropping = true;
                    area.Lasing = false;

                    var center = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, new Vector2(0.5f)));
                    var edge = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, new Vector2(2.5f, 0.5f)));
                    var corner = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, new Vector2(2.5f, 2.5f)));
                    foreach (var target in new[] { center, edge, corner })
                    {
                        SEntMan.EnsureComponent<DamageableComponent>(target);
                        SEntMan.EnsureComponent<InjurableComponent>(target);
                    }

                    levelTargets.Add((center, edge, corner, areaGrid.AreaEntities["RMCAreaDesertDamInterior"], mapId));
                    members.Add(map, depth);
                    if (depth == 0)
                        strikeCoordinates = new EntityCoordinates(grid, new Vector2(0.5f));
                }

                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, members), Is.True);
            });

            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                var strike = SEntMan.SpawnEntity("CMUShipWeaponStrike", strikeCoordinates);
                SEntMan.EnsureComponent<CMUShipWeaponStrikeComponent>(strike).Weapon = "CMURailcannonStrike";
                SEntMan.System<CMUShipWeaponsSystem>().Update(0);

                var damage = SEntMan.System<DamageableSystem>();
                var ordnance = SEntMan.System<CMUTopDownOrdnanceSystem>();
                foreach (var (center, edge, corner, areaUid, mapId) in levelTargets)
                {
                    Assert.That(damage.GetTotalDamage((center, SComp<DamageableComponent>(center))).Float(),
                        Is.EqualTo(4000), $"center target on {mapId}");
                    Assert.That(damage.GetTotalDamage((edge, SComp<DamageableComponent>(edge))).Float(),
                        Is.EqualTo(4000), $"circular boundary target on {mapId}");
                    Assert.That(damage.GetTotalDamage((corner, SComp<DamageableComponent>(corner))).Float(),
                        Is.Zero, $"outside circular footprint on {mapId}");

                    Assert.That(ordnance.IsOpening(new MapCoordinates(new Vector2(0.5f), mapId)),
                        Is.True, $"hole at the strike column on {mapId}");
                    Assert.That(ordnance.IsOpening(new MapCoordinates(new Vector2(2.5f, 0.5f), mapId)),
                        Is.True, $"hole on the circular boundary on {mapId}");
                    Assert.That(ordnance.IsOpening(new MapCoordinates(new Vector2(2.5f, 2.5f), mapId)),
                        Is.False, $"no diagonal spill on {mapId}");

                    var area = SComp<AreaComponent>(areaUid);
                    Assert.That(area.OB && area.CAS && area.Fulton && area.SupplyDrop && area.MortarFire &&
                                area.MortarPlacement && area.Lasing && area.Medevac && area.Paradropping,
                        Is.True, $"ceiling tier 0 on {mapId}");
                }
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in cleanup)
                {
                    if (!SEntMan.Deleted(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
        }
    }
}
