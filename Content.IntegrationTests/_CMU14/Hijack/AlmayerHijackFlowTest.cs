using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Hijack;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Hijack;
using Content.Shared.CMU14.util;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Hijack;

public sealed partial class AlmayerHijackMapTest
{
    [TestCase(null)]
    [TestCase("USCM")]
    [TestCase("HAZOPS")]
    public async Task PlatoonDropshipsArriveAtSeparateAlmayerHangarsOnlyOnce(string? platoonId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        EntityUid map = default;
        EntityUid[] ships = [];
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            map = LoadAlmayer(pair);
            var platoons = pair.Server.System<PlatoonSpawnRuleSystem>();
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            platoons.SelectedGovforPlatoon = platoonId == null ? null : prototypes.Index<PlatoonPrototype>(platoonId);
            // Exercise the standalone round hook, then the Govfor/Distress shared path.
            entities.EventBus.RaiseEvent(EventSource.Local,
                new GameRunLevelChangedEvent(GameRunLevel.PreRoundLobby, GameRunLevel.InRound));
            Assert.That(pair.Server.System<PlatoonSpawnRuleSystem>().TryInitializeAlmayerDropships("govfor"), Is.True);
            var supply = entities.GetComponent<CMUAlmayerSupplyComponent>(map);
            ships = supply.InitialDropships.Values.ToArray();
            Assert.That(ships, Has.Length.EqualTo(2));
            var expected = prototypes.Index<PlatoonPrototype>(platoonId ?? supply.DefaultPlatoon.Id).CompatibleDropships;
            Assert.That(supply.InitialDropships.Keys, Is.EquivalentTo(expected),
                "Almayer must use the chosen platoon's maps rather than a fixed Alamo/Normandy pair.");
            Assert.That(ships.Select(uid => entities.GetComponent<DropshipComponent>(uid).Destination).Distinct().Count(), Is.EqualTo(2));
        });
        await pair.Server.WaitRunTicks(900);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var transform = pair.Server.System<SharedTransformSystem>();
            foreach (var ship in ships)
            {
                var dropship = entities.GetComponent<DropshipComponent>(ship);
                var destination = dropship.Destination!.Value;
                Assert.That(entities.GetComponent<TransformComponent>(ship).MapUid, Is.EqualTo(map));
                Assert.That(entities.GetComponent<DropshipDestinationComponent>(destination).Ship, Is.EqualTo(ship));
                var expected = transform.GetMapCoordinates(destination).Position - new Vector2(.5f, .5f);
                Assert.That(Vector2.Distance(transform.GetMapCoordinates(ship).Position, expected), Is.LessThan(.05f));
                Assert.That(dropship.Crashed, Is.False);
            }
            var supply = entities.GetComponent<CMUAlmayerSupplyComponent>(map);
            pair.Server.System<PlatoonSpawnRuleSystem>().InitializeAlmayerDropships(map, supply);
            Assert.That(supply.InitialDropships.Values, Is.EquivalentTo(ships), "A second round-start path must not duplicate or relaunch either ship.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AuthoredAlmayerPumpsDriveTheGroundAndFtlFailureBranches(bool enterFtl)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var map = LoadAlmayer(pair);
            var state = entities.GetComponent<CMUShipHijackComponent>(map);
            state.CheckMarinePresence = false;
            state.ContinueOnGroundCrash = true;
            var maps = server.System<SharedMapSystem>();
            var planet = maps.CreateMap(out var planetId, runMapInit: true);
            entities.AddComponent<RMCPlanetComponent>(planet);
            var terrain = maps.CreateGridEntity(planetId);
            maps.SetTile(terrain, terrain.Comp, Vector2i.Zero, new Tile(1));
            var landed = new DropshipHijackLandedEvent(map);
            entities.EventBus.RaiseEvent(EventSource.Local, ref landed);
            var system = server.System<ShipHijackSystem>();
            var progress = entities.GetComponent<EvacuationProgressComponent>(map);
            void Tick()
            {
                state.NextUpdate = TimeSpan.Zero;
                system.Update(0);
            }
            Tick();
            Assert.That(progress.Progress, Is.GreaterThan(0), "The mapped pump areas must contribute fuel without synthetic test areas.");
            if (enterFtl)
            {
                for (var i = 0; i < 200 && !state.InFTL; i++)
                    Tick();
                Assert.That(state.InFTL, Is.True);
                Assert.That(progress.Progress, Is.GreaterThanOrEqualTo(50));
            }
            foreach (var pump in state.Pumps.Values)
                server.System<DamageableSystem>().TryChangeDamage(pump!.Value,
                    new DamageSpecifier { DamageDict = { ["Caustic"] = 2500 } }, true);
            Tick();
            Assert.That(state.Stage, Is.EqualTo(enterFtl ? CMUShipHijackStage.FTLCrash : CMUShipHijackStage.GroundCrash));
            var hullTiles = state.ShipGrids.ToDictionary(uid => uid,
                uid => maps.GetAllTiles(uid, entities.GetComponent<MapGridComponent>(uid)).Count());
            state.TransitionAt = TimeSpan.Zero;
            system.Update(0);
            Assert.That(state.SelfDestructUnlocked, Is.False);
            if (enterFtl)
            {
                Assert.That(state.InFTL, Is.False);
                Assert.That(state.PumpExplosionAt, Is.Not.Null);
                state.PumpExplosionAt = TimeSpan.Zero;
                system.Update(0);
                Assert.That(state.PumpBlastTargets, Has.Count.EqualTo(48));
            }
            else
            {
                Assert.That(state.GroundImpacted, Is.True);
                Assert.That(state.CrashGroundMap, Is.EqualTo(planet));
                Assert.That(state.ShipGrids.Any(uid => maps.GetAllTiles(uid,
                    entities.GetComponent<MapGridComponent>(uid)).Count() < hullTiles[uid]), Is.True,
                    "Ground impact must open actual holes in Almayer's hull.");
                Assert.That(server.System<CMUZLevelsSystem>().IsSameZNetwork(map, planet), Is.True);
                Assert.That(system.TryGetShip(planet, out _), Is.False);
            }
            state.SelfDestructUnlockAt = TimeSpan.Zero;
            system.Update(0);
            Assert.That(state.SelfDestructUnlocked, Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
