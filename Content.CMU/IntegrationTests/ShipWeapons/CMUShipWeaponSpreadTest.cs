#pragma warning disable RA0002 // Configure the strike and area fixture directly.
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Components;
using Content.Server.CMU14.ShipWeapons;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.Doors.Components;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.CMU14.ShipWeapons;

[TestFixture]
public sealed class CMUShipWeaponSpreadTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase("CMUSmokeStrike")]
    [TestCase("CMUGasStrike")]
    [TestCase("CMUEMSStrike")]
    public async Task SupportStrikesSeedEveryNetworkLevelAndSpreadOnlyThroughVerticalOpenings(string weapon)
    {
        var map = await Pair.CreateTestMap();
        EntityUid upper = default, lowerField = default, roof = default;
        EntityUid lowerVictim = default, upperVictim = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            upper = maps.CreateMap(runMapInit: true);
            var upperGrid = SEntMan.EnsureComponent<MapGridComponent>(upper);
            var levels = SEntMan.System<CMUZLevelsSystem>();
            var network = levels.CreateZNetwork();
            Assert.That(levels.TryAddMapsIntoZNetwork(network, new() { [map.MapUid] = 0, [upper] = 1 }), Is.True);
            var areas = SEntMan.System<AreaSystem>();
            var lowerArea = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
            var upperArea = SEntMan.EnsureComponent<AreaGridComponent>(upper);
            for (var x = 0; x <= 45; x++)
            {
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
                maps.SetTile(upper, upperGrid, new Vector2i(x, 0), map.Tile.Tile);
                areas.ReplaceArea(lowerArea, new Vector2i(x, 0), "RMCAreaDesertDamInterior");
                areas.ReplaceArea(upperArea, new Vector2i(x, 0), "RMCAreaDesertDamInterior");
            }
            SEntMan.SpawnAtPosition("CMWallMetal", new EntityCoordinates(map.Grid, new Vector2(31.5f, .5f)));
            SEntMan.SpawnAtPosition("CMWallMetal", new EntityCoordinates(map.Grid, new Vector2(43.5f, .5f)));
            if (weapon == "CMUEMSStrike")
            {
                lowerVictim = SEntMan.SpawnAtPosition("CMMobHuman", new EntityCoordinates(map.Grid, new Vector2(.5f)));
                upperVictim = SEntMan.SpawnAtPosition("CMMobHuman", new EntityCoordinates(upper, new Vector2(.5f)));
            }
            var strike = SEntMan.SpawnAtPosition("CMUShipWeaponStrike", new EntityCoordinates(map.Grid, new Vector2(.5f)));
            SEntMan.EnsureComponent<CMUShipWeaponStrikeComponent>(strike).Weapon = weapon;
            SEntMan.System<CMUShipWeaponsSystem>().Update(0);
            var fields = SEntMan.EntityQuery<CMUShipWeaponCloudSpreadComponent>().ToArray();
            Assert.That(fields, Has.Length.EqualTo(2), "unrelated maps receive no fields");
            foreach (var field in fields)
                Assert.That(field.CoveredTiles.Contains(new Vector2i(30, 0)), Is.True,
                    "each deck receives the initial 30-tile footprint despite intervening floors");
            lowerField = fields.Single(f => SComp<TransformComponent>(f.Owner).MapID == map.MapId).Owner;
        });

        await Pair.RunSeconds(3.1f);
        await Server.WaitAssertion(() =>
        {
            var lower = SComp<CMUShipWeaponCloudSpreadComponent>(lowerField);
            Assert.That(lower.CoveredTiles.Contains(new Vector2i(32, 0)), Is.False,
                "a solid upper floor prevents gas bypassing the lower wall vertically");
            if (weapon == "CMUEMSStrike")
            {
                var status = SEntMan.System<StatusEffectsSystem>();
                Assert.That(status.TryGetStatusEffect(lowerVictim, CMUShipWeaponsSystem.EmsStun, out _), Is.True);
                Assert.That(status.TryGetStatusEffect(upperVictim, CMUShipWeaponsSystem.EmsStun, out _), Is.True);
            }
            SEntMan.System<SharedMapSystem>().SetTile(upper, SComp<MapGridComponent>(upper),
                new Vector2i(32, 0), Tile.Empty);
            roof = SEntMan.SpawnAtPosition(null, new EntityCoordinates(map.Grid, new Vector2(32.5f, .5f)));
            SEntMan.EnsureComponent<IsRoofComponent>(roof);
        });

        await Pair.RunSeconds(1.1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUShipWeaponCloudSpreadComponent>(lowerField).CoveredTiles
                .Contains(new Vector2i(32, 0)), Is.False,
                "an explicit roof blocks vertical spread even with the upper floor removed");
            SEntMan.DeleteEntity(roof);
        });

        await Pair.RunSeconds(1.1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUShipWeaponCloudSpreadComponent>(lowerField).CoveredTiles
                .Contains(new Vector2i(32, 0)), Is.True,
                "an opened floor lets the upper cloud descend behind the lower wall");
        });

        await Pair.RunSeconds(52);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUShipWeaponCloudSpreadComponent>(lowerField).CoveredTiles
                .Contains(new Vector2i(45, 0)), Is.False);
            SEntMan.System<SharedMapSystem>().SetTile(upper, SComp<MapGridComponent>(upper),
                new Vector2i(44, 0), Tile.Empty);
        });

        await Pair.RunSeconds(2.1f);
        await Server.WaitAssertion(() =>
        {
            var lower = SComp<CMUShipWeaponCloudSpreadComponent>(lowerField);
            Assert.That(lower.CoveredTiles.Contains(new Vector2i(45, 0)), Is.True);
            Assert.That(lower.EndsAt - SGameTiming.CurTime, Is.GreaterThan(TimeSpan.FromSeconds(6)));
            foreach (var fieldUid in lower.LevelFields.Values)
            {
                var field = SComp<CMUShipWeaponCloudSpreadComponent>(fieldUid);
                Assert.That(field.EndsAt, Is.EqualTo(lower.EndsAt), "delayed spread extends all decks together");
                if (weapon == "CMUEMSStrike")
                {
                    var ems = SComp<CMUShipWeaponEmsFieldComponent>(fieldUid);
                    Assert.That(ems.EndsAt, Is.EqualTo(lower.EndsAt));
                    var victim = fieldUid == lowerField ? lowerVictim : upperVictim;
                    Assert.That(SEntMan.System<StatusEffectsSystem>().TryGetTime(
                        victim, CMUShipWeaponsSystem.EmsStun, out var stun), Is.True);
                    Assert.That(stun.EndEffectTime, Is.EqualTo(lower.EndsAt + ems.StunAfterField),
                        "extended EMS fields preserve the common recovery deadline on every deck");
                }
            }
        });

        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(lowerField), Is.False,
                "late full-range spread survives beyond the original 60-second deadline");
            var lower = SComp<CMUShipWeaponCloudSpreadComponent>(lowerField);
            Assert.That(lower.CoveredTiles.Contains(new Vector2i(45, 0)), Is.True);
        });

        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(lowerField), Is.True));
    }

    [TestCase("CMUSmokeStrike")]
    [TestCase("CMUGasStrike")]
    [TestCase("CMUEMSStrike")]
    public async Task SupportCloudsSpreadThroughDoorsButNotWallsAndExpireTogether(string weapon)
    {
        var map = await Pair.CreateTestMap();
        EntityUid field = default, reached = default, blocked = default;
        var startedAt = TimeSpan.Zero;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
            var areas = SEntMan.System<AreaSystem>();
            var plating = Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
            for (var i = -46; i <= 46; i++)
            {
                foreach (var tile in new[] { new Vector2i(i, 0), new Vector2i(0, i) })
                {
                    maps.SetTile(map.Grid, tile, new Tile(plating));
                    areas.ReplaceArea(areaGrid, tile, "RMCAreaDesertDamInterior");
                }
            }
            maps.SetTile(map.Grid, new Vector2i(40, 4), new Tile(plating));
            areas.ReplaceArea(areaGrid, new Vector2i(40, 4), "RMCAreaDesertDamInterior");
            SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]).OB = true;
            areas.ReplaceArea(areaGrid, new Vector2i(0, -31), "RMCAreaDesertDam");
            SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDam"]).OB = false;

            var wall = SEntMan.SpawnAtPosition("CMWallMetal", Coordinates(-31, 0));
            var door = SEntMan.SpawnAtPosition("CMAirlock", Coordinates(0, 31));
            Assert.That(SComp<TransformComponent>(wall).Anchored, Is.True);
            Assert.That(SComp<AirtightComponent>(wall).AirBlocked, Is.True);
            Assert.That(SComp<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed));
            Assert.That(SComp<AirtightComponent>(door).AirBlocked, Is.True);
            if (weapon == "CMUEMSStrike")
            {
                reached = SEntMan.SpawnAtPosition("CMMobHuman", Coordinates(31, 0));
                blocked = SEntMan.SpawnAtPosition("CMMobHuman", Coordinates(-32, 0));
            }

            var profile = SProtoMan.Index<CMUShipWeaponPrototype>(weapon);
            Assert.That(profile.CloudRadius, Is.EqualTo(30));
            Assert.That(profile.CloudMaxRadius, Is.EqualTo(45));
            Assert.That(profile.FootprintRadius, Is.EqualTo(45.75f));
            Assert.That(profile.FieldDuration, Is.EqualTo(TimeSpan.FromSeconds(60)));
            var strike = SEntMan.SpawnAtPosition("CMUShipWeaponStrike", Coordinates(0, 0));
            SEntMan.EnsureComponent<CMUShipWeaponStrikeComponent>(strike).Weapon = weapon;
            startedAt = SGameTiming.CurTime;
            SEntMan.System<CMUShipWeaponsSystem>().Update(0);
            field = SEntMan.EntityQuery<CMUShipWeaponCloudSpreadComponent>().Single().Owner;
            var spread = SComp<CMUShipWeaponCloudSpreadComponent>(field);
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(30, 0)), Is.True);
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(31, 0)), Is.False);
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(0, 31)), Is.False);
            if (weapon == "CMUEMSStrike")
                Assert.That(SEntMan.System<StatusEffectsSystem>().TryGetStatusEffect(
                    reached, CMUShipWeaponsSystem.EmsStun, out _), Is.False);
        });

        await Pair.RunSeconds(1.1f);
        await Server.WaitAssertion(() =>
        {
            var spread = SComp<CMUShipWeaponCloudSpreadComponent>(field);
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(31, 0)), Is.True);
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(32, 0)), Is.False,
                "spread advances only one neighboring tile per second");
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(0, 31)), Is.True,
                "closed doors allow clouds through");
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(-31, 0)), Is.False);
        });

        await Pair.RunSeconds(15);
        await Server.WaitAssertion(() =>
        {
            var spread = SComp<CMUShipWeaponCloudSpreadComponent>(field);
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(45, 0)), Is.True);
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(0, 45)), Is.True,
                "spread continues beyond the closed door");
            Assert.That(spread.CoveredTiles.Any(t => t.X * t.X + t.Y * t.Y > 45 * 45), Is.False);
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(-32, 0)), Is.False,
                "spread cannot jump over a wall");
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(0, -32)), Is.False,
                "spread cannot cross orbital protection");
            Assert.That(spread.CoveredTiles.Contains(new Vector2i(40, 4)), Is.False,
                "disconnected ground cannot be reached across space");
            if (weapon == "CMUEMSStrike")
            {
                var status = SEntMan.System<StatusEffectsSystem>();
                Assert.That(status.TryGetTime(reached, CMUShipWeaponsSystem.EmsStun, out var time), Is.True);
                Assert.That(time.EndEffectTime, Is.EqualTo(startedAt + TimeSpan.FromSeconds(95)));
                Assert.That(status.TryGetStatusEffect(blocked, CMUShipWeaponsSystem.EmsStun, out _), Is.False,
                    "EMS follows the reached tiles, not an unrestricted radius");
            }

            var profile = SProtoMan.Index<CMUShipWeaponPrototype>(weapon);
            var clouds = SEntMan.EntityQuery<MetaDataComponent>()
                .Where(m => m.EntityPrototype?.ID == profile.Cloud).ToArray();
            Assert.That(clouds.Length, Is.EqualTo(spread.CoveredTiles.Count), "no duplicate clouds per strike tile");
            foreach (var cloud in clouds)
                Assert.That(SComp<TimedDespawnComponent>(cloud.Owner).Lifetime,
                    Is.EqualTo((spread.EndsAt - SGameTiming.CurTime).TotalSeconds).Within(0.1));

            if (weapon == "CMUEMSStrike")
                reached = SEntMan.SpawnAtPosition("CMMobHuman",
                    new EntityCoordinates(map.Grid, new Vector2(45.9f, 0.9f)));
        });

        await Pair.RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            if (weapon == "CMUEMSStrike")
                Assert.That(SEntMan.System<StatusEffectsSystem>().TryGetStatusEffect(
                    reached, CMUShipWeaponsSystem.EmsStun, out _), Is.True,
                    "EMS covers the entire reached outer tile, including its far corner");
        });

        await Pair.RunSeconds(7);
        await Server.WaitAssertion(() =>
        {
            var spread = SComp<CMUShipWeaponCloudSpreadComponent>(field);
            var cloud = SProtoMan.Index<CMUShipWeaponPrototype>(weapon).Cloud;
            var edgeClouds = SEntMan.EntityQuery<MetaDataComponent, TransformComponent>()
                .Where(e => e.Item1.EntityPrototype?.ID == cloud &&
                    e.Item2.Coordinates.Position.X >= 45).ToArray();
            Assert.That(edgeClouds, Is.Not.Empty, "full 45-tile range persists for at least seven seconds");
            Assert.That(spread.EndsAt, Is.GreaterThan(SGameTiming.CurTime));
        });

        await Pair.RunSeconds(36.8f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(field), Is.True);
            var cloud = SProtoMan.Index<CMUShipWeaponPrototype>(weapon).Cloud;
            Assert.That(SEntMan.EntityQuery<MetaDataComponent>()
                .Any(m => m.EntityPrototype?.ID == cloud), Is.False, "outer clouds do not outlive the impact");
        });

        EntityCoordinates Coordinates(int x, int y) =>
            new(map.Grid, new Vector2(x + 0.5f, y + 0.5f));
    }
}
