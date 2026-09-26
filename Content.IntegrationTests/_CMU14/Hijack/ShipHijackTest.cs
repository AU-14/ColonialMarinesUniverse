using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Hijack;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.CMU14.Hijack;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.DoAfter;

namespace Content.IntegrationTests.CMU14.Hijack;

[TestFixture]
public sealed class ShipHijackTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUHijackTestFuelArea
          components:
          - type: Area
            hijackEvacuationArea: true
            hijackEvacuationWeight: 1
            hijackEvacuationType: Add
          - type: RMCAreaPower

        - type: entity
          id: CMUHijackTestEngineArea
          components:
          - type: Area
            alwaysPowered: true
          - type: RMCAreaPower

        - type: entity
          id: CMUHijackTestPump
          parent: CMUHijackFuelPump
        """;

    private (EntityUid Map, EntityUid Pump) CreateShip()
    {
        var maps = Server.System<SharedMapSystem>();
        var map = maps.CreateMap(out var mapId, runMapInit: true);
        SEntMan.AddComponent<CMUShipHijackComponent>(map).CheckMarinePresence = false;
        var grid = maps.CreateGridEntity(mapId);
        maps.SetTile(grid, grid.Comp, Vector2i.Zero, new Tile(1));
        var area = SEntMan.AddComponent<AreaGridComponent>(grid);
        Server.System<AreaSystem>().ReplaceArea(area, Vector2i.Zero, "CMUHijackTestFuelArea");
        var pump = SEntMan.SpawnEntity("CMUHijackTestPump", new EntityCoordinates(grid, new Vector2(0.5f, 0.5f)));
        var ev = new DropshipHijackLandedEvent(map);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
        return (map, pump);
    }

    private void TickObjectives(EntityUid map)
    {
        var state = SEntMan.GetComponent<CMUShipHijackComponent>(map);
        state.NextUpdate = TimeSpan.Zero;
        Server.System<ShipHijackSystem>().Update(0);
    }

    [Test]
    public async Task PumpRejectsBulletsExplosionsAndHealingButPermanentlyBreaksFromAcid()
    {
        await Server.WaitAssertion(() =>
        {
            var ship = CreateShip();
            var damage = Server.System<DamageableSystem>();
            var caustic = new DamageSpecifier { DamageDict = { ["Caustic"] = 2500 } };
            damage.TryChangeDamage(ship.Pump, caustic, true, impact: DamageImpact.Explosion);
            var bullets = new DamageSpecifier { DamageDict = { ["Piercing"] = 5000 } };
            damage.TryChangeDamage(ship.Pump, bullets, true, impact: DamageImpact.Projectile);
            Assert.That(damage.GetTotalDamage((ship.Pump, SEntMan.GetComponent<DamageableComponent>(ship.Pump))), Is.EqualTo(FixedPoint2.Zero));
            var state = SEntMan.GetComponent<CMUShipHijackComponent>(ship.Map);
            state.Stage = CMUShipHijackStage.Idle;
            damage.TryChangeDamage(ship.Pump, caustic, true);
            Assert.That(SEntMan.GetComponent<CMUHijackPumpComponent>(ship.Pump).Broken, Is.False);
            state.Stage = CMUShipHijackStage.Sublight;
            var acid = SEntMan.SpawnEntity(null, SEntMan.GetComponent<TransformComponent>(ship.Pump).Coordinates);
            var corrosion = new CorrodingEvent(acid, 10, 0, XenoAcidStrength.Normal);
            SEntMan.EventBus.RaiseLocalEvent(ship.Pump, ref corrosion);
            Assert.That(corrosion.Cancelled, Is.True);
            Assert.That(SEntMan.HasComponent<DamageableCorrodingComponent>(ship.Pump), Is.True);
            Assert.That(SEntMan.HasComponent<TimedCorrodingComponent>(ship.Pump), Is.False);
            damage.TryChangeDamage(ship.Pump, caustic, true);
            Assert.That(SEntMan.GetComponent<CMUHijackPumpComponent>(ship.Pump).Broken, Is.True);
            damage.TryChangeDamage(ship.Pump, -caustic, true);
            Assert.That(damage.GetTotalDamage((ship.Pump, SEntMan.GetComponent<DamageableComponent>(ship.Pump))), Is.EqualTo(FixedPoint2.New(2500)));
            SEntMan.DeleteEntity(ship.Map);
        });
    }

    [Test]
    public async Task MultitoolOverloadRequiresUnlockAndTrainingAndCompletesItsDoAfter()
    {
        EntityUid reactor = default;
        EntityUid engineer = default;
        EntityUid tool = default;
        EntityCoordinates coordinates = default;
        await Server.WaitAssertion(() =>
        {
            var ship = CreateShip();
            var state = SEntMan.GetComponent<CMUShipHijackComponent>(ship.Map);
            var gridUid = SEntMan.GetComponent<TransformComponent>(ship.Pump).GridUid!.Value;
            var maps = Server.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(gridUid);
            maps.SetTile(gridUid, grid, new Vector2i(2, 0), new Tile(1));
            maps.SetTile(gridUid, grid, new Vector2i(2, 1), new Tile(1));
            Server.System<AreaSystem>().ReplaceArea(SEntMan.GetComponent<AreaGridComponent>(gridUid),
                new Vector2i(2, 0), "CMUHijackTestEngineArea");
            coordinates = new EntityCoordinates(gridUid, new Vector2(2.5f, .5f));
            reactor = SEntMan.SpawnEntity("RMCGeneratorFusion", coordinates);
            SEntMan.AddComponent<CMUReactorOverloadComponent>(reactor);
            engineer = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(gridUid, new Vector2(2.5f, 1.5f)));
            tool = SEntMan.SpawnEntity("CMMultitool", coordinates);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(engineer, tool), Is.True);
            var skills = Server.System<SkillsSystem>();
            skills.SetSkill(engineer, "RMCSkillEngineer", 2);
            Server.System<SharedInteractionSystem>().InteractUsing(engineer, tool, reactor, coordinates);
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(engineer).DoAfters, Is.Empty, "Self-destruct is locked before ship failure.");
            state.Stage = CMUShipHijackStage.FTLCrash;
            state.SelfDestructUnlocked = true;
            skills.SetSkill(engineer, "RMCSkillEngineer", 0);
            Server.System<SharedInteractionSystem>().InteractUsing(engineer, tool, reactor, coordinates);
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(engineer).DoAfters, Is.Empty, "An untrained marine cannot overload the reactor.");
            skills.SetSkill(engineer, "RMCSkillEngineer", 2);
            Assert.That(Server.System<SharedInteractionSystem>().InteractUsing(engineer, tool, reactor, coordinates), Is.True);
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(engineer).DoAfters, Has.Count.EqualTo(1));
            Assert.That(SEntMan.GetComponent<CMUReactorOverloadComponent>(reactor).Overloaded, Is.False);
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUReactorOverloadComponent>(reactor).Overloaded, Is.True);
            Assert.That(Server.System<SharedInteractionSystem>().InteractUsing(engineer, tool, reactor, coordinates), Is.True);
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUReactorOverloadComponent>(reactor).Overloaded, Is.False,
                "A second completed multitool action restores the reactor safeties."));
    }

    [Test]
    public async Task WorkingFueledReactorCountsDownThenPausesWhenFuelIsRemoved()
    {
        await Server.WaitAssertion(() =>
        {
            var ship = CreateShip();
            var state = SEntMan.GetComponent<CMUShipHijackComponent>(ship.Map);
            state.Stage = CMUShipHijackStage.FTLCrash;
            state.SelfDestructUnlocked = true;
            var gridUid = SEntMan.GetComponent<TransformComponent>(ship.Pump).GridUid!.Value;
            var maps = Server.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(gridUid);
            maps.SetTile(gridUid, grid, new Vector2i(2, 0), new Tile(1));
            Server.System<AreaSystem>().ReplaceArea(SEntMan.GetComponent<AreaGridComponent>(gridUid), new Vector2i(2, 0), "CMUHijackTestEngineArea");
            var reactor = SEntMan.SpawnEntity("RMCGeneratorFusion", new EntityCoordinates(gridUid, new Vector2(2.5f, .5f)));
            SEntMan.AddComponent<CMUReactorOverloadComponent>(reactor).Overloaded = true;
            TickObjectives(ship.Map);
            Assert.That(state.OverloadedGenerators, Is.EqualTo(1));
            Assert.That(state.SelfDestructRemaining, Is.EqualTo(862).Within(.01));
            var containers = Server.System<Robust.Shared.Containers.SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(reactor, "rmc_fusion_reactor_cell", out var cell), Is.True);
            foreach (var fuel in cell!.ContainedEntities.ToArray())
                SEntMan.DeleteEntity(fuel);
            TickObjectives(ship.Map);
            var paused = state.SelfDestructRemaining;
            Assert.That(state.OverloadedGenerators, Is.Zero);
            Assert.That(SEntMan.GetComponent<CMUReactorOverloadComponent>(reactor).Overloaded, Is.False);
            TickObjectives(ship.Map);
            Assert.That(state.SelfDestructRemaining, Is.EqualTo(paused));
            SEntMan.DeleteEntity(ship.Map);
        });
    }

    [Test]
    public async Task DetonationGuardsRoundEndAndSpareDepartedMobsAndFridgeOccupants()
    {
        await Server.WaitAssertion(() =>
        {
            var ship = CreateShip();
            var state = SEntMan.GetComponent<CMUShipHijackComponent>(ship.Map);
            var system = Server.System<ShipHijackSystem>();
            var outside = Server.System<SharedMapSystem>().CreateMap(out _, runMapInit: true);
            var coordinates = SEntMan.GetComponent<TransformComponent>(ship.Pump).Coordinates;
            EntityUid Mob()
            {
                var uid = SEntMan.SpawnEntity(null, coordinates);
                SEntMan.AddComponent<MobStateComponent>(uid);
                return uid;
            }
            var doomed = Mob();
            var escaped = Mob();
            var sheltered = Mob();
            var fridge = SEntMan.SpawnEntity(null, coordinates);
            SEntMan.AddComponent<CMUNuclearShelterComponent>(fridge);
            var containers = Server.System<Robust.Shared.Containers.SharedContainerSystem>();
            var inside = containers.EnsureContainer<Robust.Shared.Containers.Container>(fridge, "test-shelter");
            Assert.That(containers.Insert(sheltered, inside), Is.True);
            state.Stage = CMUShipHijackStage.Detonating;
            var roundEnd = new CMUShipRoundEndAttemptEvent();
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref roundEnd);
            Assert.That(roundEnd.Cancelled, Is.True);
            state.DetonationStartedAt = SGameTiming.CurTime - TimeSpan.FromSeconds(12);
            system.Update(0);
            Server.System<SharedTransformSystem>().SetCoordinates(escaped, new EntityCoordinates(outside, Vector2.Zero));
            state.DetonationStartedAt = SGameTiming.CurTime - TimeSpan.FromSeconds(27);
            system.Update(0);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<MobStateComponent>(doomed).CurrentState, Is.EqualTo(MobState.Dead));
                Assert.That(SEntMan.GetComponent<MobStateComponent>(escaped).CurrentState, Is.EqualTo(MobState.Alive));
                Assert.That(SEntMan.GetComponent<MobStateComponent>(sheltered).CurrentState, Is.EqualTo(MobState.Alive));
            });
            state.DetonationStartedAt = SGameTiming.CurTime - TimeSpan.FromSeconds(28);
            system.Update(0);
            Assert.That(state.Stage, Is.EqualTo(CMUShipHijackStage.Destroyed));
            Assert.That(system.HasDetonationInProgress(), Is.False);
            SEntMan.DeleteEntity(ship.Map);
            SEntMan.DeleteEntity(outside);
        });
    }

    [Test]
    public async Task PumpHealthDrivesProgressWithoutApcPowerAndDoesNotAffectOtherShip()
    {
        await Server.WaitAssertion(() =>
        {
            var a = CreateShip();
            var b = CreateShip();
            try
            {
                TickObjectives(a.Map);
                Assert.That(SEntMan.GetComponent<EvacuationProgressComponent>(a.Map).Progress, Is.EqualTo(1));
                SEntMan.GetComponent<CMUHijackPumpComponent>(a.Pump).Broken = true;
                TickObjectives(a.Map);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.GetComponent<CMUShipHijackComponent>(a.Map).Stage, Is.EqualTo(CMUShipHijackStage.GroundCrash));
                    Assert.That(SEntMan.GetComponent<CMUShipHijackComponent>(b.Map).Stage, Is.EqualTo(CMUShipHijackStage.Sublight));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(a.Map);
                SEntMan.DeleteEntity(b.Map);
            }
        });
    }

    [Test]
    public async Task FuelLossAfterHalfProgressTakesFtlFailureBranch()
    {
        await Server.WaitAssertion(() =>
        {
            var ship = CreateShip();
            try
            {
                for (var i = 0; i < 51; i++)
                    TickObjectives(ship.Map);
                var state = SEntMan.GetComponent<CMUShipHijackComponent>(ship.Map);
                Assert.That(state.Stage, Is.EqualTo(CMUShipHijackStage.FTL));
                Assert.That(Server.System<ShipHijackSystem>().CanLaunch(ship.Pump), Is.False);
                SEntMan.GetComponent<CMUHijackPumpComponent>(ship.Pump).Broken = true;
                TickObjectives(ship.Map);
                Assert.That(state.Stage, Is.EqualTo(CMUShipHijackStage.FTLCrash));
                Assert.That(state.SelfDestructUnlocked, Is.False);
                Assert.That(state.GroundImpacted, Is.False);
                state.SelfDestructUnlockAt = TimeSpan.Zero;
                Server.System<ShipHijackSystem>().Update(0);
                Assert.That(state.SelfDestructUnlocked, Is.True);
                Assert.That(state.SelfDestructRemaining, Is.EqualTo(900));
            }
            finally { SEntMan.DeleteEntity(ship.Map); }
        });
    }

    [Test]
    public async Task CompletedFtlRestoresLaunchAndArrivalWithoutUnlockingSelfDestruct()
    {
        await Server.WaitAssertion(() =>
        {
            var ship = CreateShip();
            var system = Server.System<ShipHijackSystem>();
            var state = SEntMan.GetComponent<CMUShipHijackComponent>(ship.Map);
            Assert.That(system.CanLaunch(ship.Pump), Is.False);
            for (var i = 0; i < 25; i++)
                TickObjectives(ship.Map);
            Assert.That(system.CanLaunch(ship.Pump), Is.True);
            for (var i = 25; i < 51; i++)
                TickObjectives(ship.Map);
            Assert.That(system.CanLaunch(ship.Pump), Is.False);
            Assert.That(system.CanArrive(ship.Pump), Is.False);
            state.TransitionAt = TimeSpan.Zero;
            system.Update(0);
            Assert.That(state.FTLEnteredAt, Is.Not.Null);
            for (var i = 51; i < 101; i++)
                TickObjectives(ship.Map);
            Assert.That(state.Stage, Is.EqualTo(CMUShipHijackStage.Arriving));
            Assert.That(system.CanLaunch(ship.Pump), Is.True);
            Assert.That(system.CanArrive(ship.Pump), Is.True);
            state.TransitionAt = TimeSpan.Zero;
            system.Update(0);
            Assert.That(state.Stage, Is.EqualTo(CMUShipHijackStage.Docked));
            Assert.That(state.SelfDestructUnlocked, Is.False);
            SEntMan.DeleteEntity(ship.Map);
        });
    }

    [Test]
    public async Task EvacuationDoesNotStartOrResetReactorMeltdown()
    {
        await Server.WaitAssertion(() =>
        {
            var ship = CreateShip();
            try
            {
                var evac = Server.System<Content.Server._RMC14.Evacuation.EvacuationSystem>();
                var state = SEntMan.GetComponent<CMUShipHijackComponent>(ship.Map);
                state.SelfDestructRemaining = 321;
                evac.ToggleEvacuation(null, null, ship.Map);
                Assert.That(SEntMan.GetComponent<EvacuationProgressComponent>(ship.Map).SelfDestructAt, Is.Null);
                evac.ToggleEvacuation(null, null, ship.Map);
                Assert.That(state.SelfDestructRemaining, Is.EqualTo(321));
            }
            finally { SEntMan.DeleteEntity(ship.Map); }
        });
    }

    [Test]
    public async Task LinkingColonyDoesNotMakeItPartOfTheShip()
    {
        await Server.WaitAssertion(() =>
        {
            var ship = CreateShip();
            var ground = Server.System<SharedMapSystem>().CreateMap(out _, runMapInit: true);
            try
            {
                var levels = Server.System<CMUZLevelsSystem>();
                var network = levels.CreateZNetwork();
                Assert.That(levels.TryAddMapsIntoZNetwork(network, new() { [ground] = 0, [ship.Map] = 1 }), Is.True);
                Assert.That(Server.System<ShipHijackSystem>().TryGetShip(ground, out _), Is.False);
                Assert.That(Server.System<ShipHijackSystem>().TryGetShip(ship.Pump, out _), Is.True);
            }
            finally
            {
                SEntMan.DeleteEntity(ship.Map);
                SEntMan.DeleteEntity(ground);
            }
        });
    }

    [Test]
    public async Task GroundImpactOpensHullAboveWalkableTerrainAndDelaysSelfDestructUnlock()
    {
        EntityUid falling = default;
        EntityUid ground = default;
        await Server.WaitAssertion(() =>
        {
            var ship = CreateShip();
            var state = SEntMan.GetComponent<CMUShipHijackComponent>(ship.Map);
            state.ContinueOnGroundCrash = true;
            var intactGrid = state.ShipGrids[0];
            // Keep an intact section: Robust deletes a grid when its last floor tile is removed.
            Server.System<SharedMapSystem>().SetTile(intactGrid,
                SEntMan.GetComponent<MapGridComponent>(intactGrid), new Vector2i(10, 10), new Tile(1));
            falling = SEntMan.SpawnEntity(null, new EntityCoordinates(intactGrid, new Vector2(.5f, .5f)));
            var physics = SEntMan.AddComponent<PhysicsComponent>(falling);
            Server.System<SharedPhysicsSystem>().SetBodyType(falling, BodyType.Dynamic, body: physics);
            SEntMan.AddComponent<CMUZPhysicsComponent>(falling);
            SEntMan.GetComponent<CMUHijackPumpComponent>(ship.Pump).Broken = true;
            TickObjectives(ship.Map);
            Assert.That(state.Stage, Is.EqualTo(CMUShipHijackStage.GroundCrash));
            Assert.That(state.GroundImpacted, Is.False);
            state.TransitionAt = TimeSpan.Zero;
            Server.System<ShipHijackSystem>().Update(0);
            Assert.Multiple(() =>
            {
                Assert.That(state.GroundImpacted, Is.True);
                Assert.That(state.CrashGroundMap, Is.Not.Null);
                Assert.That(state.SelfDestructUnlocked, Is.False);
                Assert.That(state.SelfDestructUnlockAt, Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromSeconds(15)));
                Assert.That(Server.System<ShipHijackSystem>().CanLaunch(ship.Map, lifeboat: true), Is.False);
            });
            var maps = Server.System<SharedMapSystem>();
            var gridUid = state.ShipGrids[0];
            var grid = SEntMan.GetComponent<MapGridComponent>(gridUid);
            Assert.That(maps.GetTileRef(gridUid, grid, Vector2i.Zero).Tile.IsEmpty, Is.True);
            Assert.That(Server.System<CMUZLevelsSystem>().IsSameZNetwork(ship.Map, state.CrashGroundMap!.Value), Is.True);
            Assert.That(Server.System<ShipHijackSystem>().TryGetShip(state.CrashGroundMap!.Value, out _), Is.False);
            ground = state.CrashGroundMap.Value;
            var fallTransform = SEntMan.GetComponent<TransformComponent>(falling);
            var distance = Server.System<CMUZLevelsSystem>().DistanceToGround(
                (falling, SEntMan.GetComponent<CMUZPhysicsComponent>(falling)), out var sticky);
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(falling) || fallTransform.MapUid == ground, Is.True,
                $"parent={fallTransform.ParentUid}, grid={fallTransform.GridUid}, map={fallTransform.MapUid}, " +
                $"ship={ship.Map}, ground={ground}, position={fallTransform.LocalPosition}, distance={distance}, sticky={sticky}");
            state.SelfDestructUnlockAt = TimeSpan.Zero;
            Server.System<ShipHijackSystem>().Update(0);
            Assert.That(state.SelfDestructUnlocked, Is.True);
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<TransformComponent>(falling).MapUid, Is.EqualTo(ground),
                "A body standing over the crack must fall onto the surface, not remain on the ship grid."));
    }

    [Test]
    public async Task NoLivingMarinesPermanentlyStopsObjectives()
    {
        await Server.WaitAssertion(() =>
        {
            var ship = CreateShip();
            var state = SEntMan.GetComponent<CMUShipHijackComponent>(ship.Map);
            state.CheckMarinePresence = true;
            TickObjectives(ship.Map);
            Assert.That(state.ObjectivesStopped, Is.True);
            state.CheckMarinePresence = false;
            TickObjectives(ship.Map);
            Assert.That(SEntMan.GetComponent<EvacuationProgressComponent>(ship.Map).Progress, Is.Zero);
            SEntMan.DeleteEntity(ship.Map);
        });
    }
}
