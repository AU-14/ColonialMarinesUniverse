using System.Linq;
using System.Numerics;
using Content.Client.CMU14.DroneOperator;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUFlamerDroneTest
{
    [Test]
    public async Task ClawEffectsFollowRenderedNozzlesThroughMovementAndCameraTurns()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        NetEntity netDrone = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var drone = entities.SpawnEntity("CMUFlamerDrone", map.GridCoords);
            var tank = entities.SpawnEntity("CMUFlamerDroneFuelTank", map.GridCoords);
            Assert.That(entities.System<ItemSlotsSystem>().TryInsert((drone, null), SharedGunSystem.MagazineSlot, tank, null), Is.True);
            netDrone = entities.GetNetEntity(drone);
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, drone);
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var drone = entities.GetEntity(netDrone);
            var flamer = entities.GetComponent<CMUFlamerDroneComponent>(drone);
            var transform = entities.System<SharedTransformSystem>();
            var visuals = entities.System<CMUFlamerDroneVisualizerSystem>();
            var timing = pair.Client.ResolveDependency<IGameTiming>();
            var eye = pair.Client.ResolveDependency<IEyeManager>().CurrentEye;
            var originalCamera = eye.Rotation;
            try
            {
                foreach (var firing in new[] { false, true })
                foreach (var camera in new[] { 0, 45, 90, 270 })
                foreach (var facing in new[] { 0, 44, 45, 46, 90, 134, 135, 136, 180, 224, 225, 226, 270, 314, 315, 316 })
                {
                    eye.Rotation = Angle.FromDegrees(camera);
                    var rotation = Angle.FromDegrees(facing);
                    transform.SetLocalPositionNoLerp(drone, new Vector2(facing / 100f, camera / 100f));
                    transform.SetWorldRotationNoLerp(drone, rotation);
                    flamer.FlameUntil = firing ? timing.CurTime + TimeSpan.FromSeconds(1) : TimeSpan.Zero;
                    visuals.FrameUpdate(0);

                    var direction = SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4,
                        (rotation + eye.Rotation).Reduced().FlipPositive());
                    var nozzles = new[] { flamer.FirstClawOffsets[(int) direction], flamer.SecondClawOffsets[(int) direction] };
                    var effects = GetClawEffects(entities, drone);
                    Assert.That(effects, Has.Count.EqualTo(2));
                    var remaining = nozzles.ToList();
                    foreach (var effect in effects)
                    {
                        var sprite = entities.GetComponent<SpriteComponent>(effect);
                        Assert.That(sprite.NoRotation, Is.True);
                        var light = entities.GetComponent<PointLightComponent>(effect);
                        var origin = transform.GetWorldPosition(effect) - transform.GetWorldPosition(drone);
                        var particlePosition = eye.Rotation.RotateVec(origin) + sprite.Offset;
                        var nozzle = remaining.FindIndex(offset => Vector2.Distance(offset, particlePosition) < 0.0001f);
                        Assert.That(nozzle, Is.GreaterThanOrEqualTo(0),
                            $"Flame at {particlePosition} missed its nozzle: hull {facing}, camera {camera}, firing {firing}.");
                        remaining.RemoveAt(nozzle);
                        var lightPosition = origin + transform.GetWorldRotation(effect).RotateVec(light.Offset);
                        Assert.That(Vector2.Distance(eye.Rotation.RotateVec(lightPosition), particlePosition), Is.LessThan(0.0001f),
                            "The flame and its glow must stay on the same nozzle.");
                        Assert.That(sprite.Scale, Is.EqualTo(new Vector2(firing ? 0.5f : 0.3f)));
                    }
                }
            }
            finally
            {
                eye.Rotation = originalCamera;
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LoadedFlamerKeepsPilotFlamesWhileIdleAndExtinguishesWhenDisabled()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid drone = default, tank = default;
        NetEntity netDrone = default;
        await pair.Server.WaitAssertion(() =>
        {
            drone = entities.SpawnEntity("CMUFlamerDrone", map.GridCoords);
            Assert.That(entities.GetComponent<CMUFlamerDroneComponent>(drone).PilotLit, Is.False);
            tank = entities.SpawnEntity("CMUFlamerDroneFuelTank", map.GridCoords);
            Assert.That(entities.System<ItemSlotsSystem>().TryInsert((drone, null), SharedGunSystem.MagazineSlot, tank, null), Is.True);
            Assert.That(entities.GetComponent<CMUFlamerDroneComponent>(drone).PilotLit, Is.True);
            netDrone = entities.GetNetEntity(drone);
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, drone);
        });
        await pair.RunUntilSynced();
        await AssertPilot(true);
        // Several welding-effect lifetimes pass without shooting or consuming fuel.
        await pair.RunSeconds(4);
        await AssertPilot(true);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(AmmoCount(entities, drone), Is.EqualTo(200));
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", 400);
            entities.System<DamageableSystem>().TryChangeDamage(drone, damage, ignoreResistances: true, ignoreGlobalModifiers: true);
            Assert.That(entities.GetComponent<CMUFlamerDroneComponent>(drone).PilotLit, Is.False);
        });
        await pair.RunUntilSynced();
        await AssertPilot(false);
        await pair.Server.WaitAssertion(() =>
        {
            var repair = new DamageSpecifier();
            repair.DamageDict.Add("Blunt", -400);
            entities.System<DamageableSystem>().TryChangeDamage(drone, repair, ignoreResistances: true, ignoreGlobalModifiers: true);
            Assert.That(entities.GetComponent<CMUFlamerDroneComponent>(drone).PilotLit, Is.True);
        });
        await pair.RunUntilSynced();
        await AssertPilot(true);
        await pair.Server.WaitAssertion(() =>
        {
            entities.DeleteEntity(tank);
            Assert.That(entities.GetComponent<CMUFlamerDroneComponent>(drone).PilotLit, Is.False);
        });
        await pair.RunUntilSynced();
        await AssertPilot(false);
        await pair.CleanReturnAsync();

        async Task AssertPilot(bool lit)
        {
            await pair.Client.WaitAssertion(() =>
            {
                var clientEntities = pair.Client.EntMan;
                var clientDrone = clientEntities.GetEntity(netDrone);
                clientEntities.System<CMUFlamerDroneVisualizerSystem>().FrameUpdate(0);
                var effects = GetClawEffects(clientEntities, clientDrone)
                    .Where(uid => clientEntities.GetComponent<SpriteComponent>(uid).Visible).ToList();
                Assert.That(effects, Has.Count.EqualTo(lit ? 2 : 0));
                foreach (var effect in effects)
                {
                    Assert.That(clientEntities.GetComponent<PointLightComponent>(effect).Enabled, Is.True);
                    Assert.That(clientEntities.GetComponent<SpriteComponent>(effect).Scale, Is.EqualTo(new Vector2(0.3f)));
                }
            });
        }
    }

    [Test]
    public async Task AssembleFlamerWithMatchingPartsAndBurnFuelThroughTabletControl()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid user = default, hull = default, assembly = default, tank = default, tablet = default, drone = default;
        NetEntity netDrone = default;
        NetEntity netHull = default;

        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.AddComponent<CMUDroneOperatorComponent>(user);
            hull = entities.SpawnEntity("CMUFlamerDroneHull", map.GridCoords.Offset(new Vector2(0.8f, 0)));
            netHull = entities.GetNetEntity(hull);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            entities.GetComponent<CMUCombatDroneHullComponent>(hull).AssemblyDelay = TimeSpan.FromSeconds(0.1);
            assembly = entities.SpawnEntity("CMUCombatDroneTurretAssembly", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, assembly, checkActionBlocker: false), Is.True);
            Interact(entities, user, assembly, hull);
        });
        await server.WaitRunTicks(20);
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() => AssertHullVisual(pair.Client.EntMan, netHull, Color.FromHex("#9d9d9d")));
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<SharedContainerSystem>().GetContainer(hull, "cmu-combat-drone-turret").ContainedEntities, Is.Empty,
                "A pulse turret must not fit a flamer hull.");
            entities.DeleteEntity(assembly);
            assembly = entities.SpawnEntity("CMUFlamerDroneTurretAssembly", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, assembly, checkActionBlocker: false), Is.True);
            Interact(entities, user, assembly, hull);
        });
        await server.WaitRunTicks(20);
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() => AssertHullVisual(pair.Client.EntMan, netHull, Color.White));
        await server.WaitAssertion(() =>
        {
            var wrongAmmo = entities.SpawnEntity("CMUCombatDroneAmmoBox", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, wrongAmmo, checkActionBlocker: false), Is.True);
            Interact(entities, user, wrongAmmo, hull);
            entities.DeleteEntity(wrongAmmo);
            tank = entities.SpawnEntity("CMUFlamerDroneFuelTank", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, tank, checkActionBlocker: false), Is.True);
            Interact(entities, user, tank, hull);
        });
        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            drone = entities.GetComponent<CMUDroneOperatorComponent>(user).Drone!.Value;
            Assert.That(entities.GetComponent<MetaDataComponent>(drone).EntityPrototype!.ID, Is.EqualTo("CMUFlamerDrone"));
            Assert.That(entities.EntityExists(hull), Is.False);
            Assert.That(entities.EntityExists(assembly), Is.False);
            Assert.That(entities.System<SharedContainerSystem>().GetContainer(drone, SharedGunSystem.MagazineSlot).ContainedEntities,
                Is.EqualTo(new[] { tank }), "Assembly must transfer the supplied fuel tank.");
            Assert.That(AmmoCount(entities, drone), Is.EqualTo(200));
            var bulletBox = entities.SpawnEntity("CMUCombatDroneAmmoBoxAP", map.GridCoords);
            Assert.That(entities.System<ItemSlotsSystem>().TryInsert((drone, null), SharedGunSystem.MagazineSlot, bulletBox, null), Is.False);
            entities.DeleteEntity(bulletBox);
            tablet = entities.SpawnEntity("CMUDroneControlTablet", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, tablet, checkActionBlocker: false), Is.True);
            Interact(entities, user, tablet, drone);
            server.PlayerMan.SetAttachedEntity(pair.Player!, null);
            var minds = entities.System<SharedMindSystem>();
            var mind = minds.CreateMind(null).Owner;
            minds.TransferTo(mind, user);
            entities.EventBus.RaiseLocalEvent(tablet, new UseInHandEvent(user));
            Assert.That(entities.GetComponent<MindComponent>(mind).VisitingEntity, Is.EqualTo(drone));

            var guns = entities.System<SharedGunSystem>();
            var gun = entities.GetComponent<GunComponent>(drone);
            entities.System<SharedTransformSystem>().SetWorldRotation(drone, Angle.Zero);
            var coords = entities.GetComponent<TransformComponent>(drone).Coordinates;
            Assert.That(guns.AttemptShoot((drone, gun), drone, coords.Offset(new Vector2(0, 4))), Is.Null);
            Assert.That(AmmoCount(entities, drone), Is.EqualTo(200));
            Assert.That(entities.GetComponent<CMUFlamerDroneComponent>(drone).FlameUntil, Is.EqualTo(TimeSpan.Zero),
                "A rejected shot must not light the claws.");
            guns.AttemptShoot((drone, gun), drone, coords.Offset(new Vector2(0, -4)));
            Assert.That(AmmoCount(entities, drone), Is.LessThan(200).And.GreaterThan(0));
            Assert.That(entities.GetComponent<CMUFlamerDroneComponent>(drone).FlameUntil, Is.GreaterThan(TimeSpan.Zero));
            netDrone = entities.GetNetEntity(drone);
            server.PlayerMan.SetAttachedEntity(pair.Player!, drone);
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var clientEntities = pair.Client.EntMan;
            var clientDrone = clientEntities.GetEntity(netDrone);
            clientEntities.System<CMUFlamerDroneVisualizerSystem>().FrameUpdate(0);
            var sprites = clientEntities.System<SpriteSystem>();
            var transform = clientEntities.System<SharedTransformSystem>();
            var effects = GetClawEffects(clientEntities, clientDrone);
            Assert.That(effects, Has.Count.EqualTo(2), "A real shot must create a welding effect and glow at each claw tip.");
            var first = effects.Single(uid => EffectOffset(uid).X < 0);
            var second = effects.Single(uid => uid != first);
            foreach (var effect in effects)
            {
                Assert.That(sprites.LayerGetRsiState((effect, null), 0).ToString(), Is.EqualTo("smoke"));
                Assert.That(sprites.LayerGetRsiState((effect, null), 1).ToString(), Is.EqualTo("welding_sparks"));
                var light = clientEntities.GetComponent<PointLightComponent>(effect);
                Assert.That(light.Enabled, Is.True);
                Assert.That(light.CastShadows, Is.False, "Brief tip glows must not consume the shadow-light budget.");
                Assert.That(light.Radius, Is.GreaterThan(0));
                Assert.That(light.Energy, Is.GreaterThan(0));
                Assert.That(light.Offset, Is.EqualTo(Vector2.Zero), "The glow must use the same nozzle origin as the particles.");
                Assert.That(clientEntities.GetComponent<SpriteComponent>(effect).Offset, Is.EqualTo(Vector2.Zero));
            }
            Assert.That(Vector2.Distance(EffectOffset(first), new Vector2(-1, -9) / 32), Is.LessThan(0.0001f));
            transform.SetWorldRotation(clientDrone, Direction.East.ToAngle());
            clientEntities.System<CMUFlamerDroneVisualizerSystem>().FrameUpdate(0);
            Assert.That(Vector2.Distance(EffectOffset(first), new Vector2(4, 12) / 32), Is.LessThan(0.0001f), "The first effect must follow the upper claw in Side A.");
            Assert.That(Vector2.Distance(EffectOffset(second), new Vector2(4, 5) / 32), Is.LessThan(0.0001f), "The second effect must follow the lower claw in Side A.");

            Vector2 EffectOffset(EntityUid effect) => transform.GetWorldPosition(effect) - transform.GetWorldPosition(clientDrone);
        });
        await server.WaitRunTicks(100);
        await server.WaitAssertion(() =>
        {
            var fires = entities.EntityQuery<MetaDataComponent>().Where(meta => meta.EntityPrototype?.ID == "RMCTileFire").ToList();
            Assert.That(fires, Is.Not.Empty, "The flamer must create actual burning tiles, not just a cosmetic effect.");
            Assert.That(entities.System<DamageableSystem>().GetTotalDamage((drone, null)).Float(), Is.Zero,
                "The UGV must remain immune to its own fire.");
            var gun = entities.GetComponent<GunComponent>(drone);
            var solutions = entities.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(tank, "rmc_flamer_tank", out var solution, out _), Is.True);
            solutions.RemoveAllSolution(solution!.Value);
            var flameUntil = entities.GetComponent<CMUFlamerDroneComponent>(drone).FlameUntil;
            var coords = entities.GetComponent<TransformComponent>(drone).Coordinates;
            Assert.That(entities.System<SharedGunSystem>().AttemptShoot((drone, gun), drone, coords.Offset(new Vector2(0, -4))), Is.Null);
            Assert.That(entities.GetComponent<CMUFlamerDroneComponent>(drone).FlameUntil, Is.EqualTo(flameUntil),
                "An empty tank must not restart the claw effects.");
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var clientEntities = pair.Client.EntMan;
            var clientDrone = clientEntities.GetEntity(netDrone);
            clientEntities.System<CMUFlamerDroneVisualizerSystem>().FrameUpdate(0);
            foreach (var effect in GetClawEffects(clientEntities, clientDrone))
            {
                Assert.That(clientEntities.GetComponent<SpriteComponent>(effect).Visible, Is.False);
                Assert.That(clientEntities.GetComponent<PointLightComponent>(effect).Enabled, Is.False,
                    "Particles and glow must switch off together when the burst ends.");
            }
        });
        await pair.Client.WaitRunTicks(2);
        await pair.Client.WaitAssertion(() => Assert.That(GetClawEffects(pair.Client.EntMan, pair.Client.EntMan.GetEntity(netDrone)), Is.Empty,
            "Expired effects must not leave orphaned light entities."));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SuppliedGunSpriteStatesFollowDamageAndRecoverableWreckOnClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid drone = default;
        NetEntity netDrone = default;
        await pair.Server.WaitPost(() =>
        {
            drone = pair.Server.EntMan.SpawnEntity("CMUCombatDrone", map.GridCoords);
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, drone);
            netDrone = pair.Server.EntMan.GetNetEntity(drone);
        });
        var last = 0;
        foreach (var (damage, state) in new[] { (0, "healthy-hull"), (100, "damaged-hull"), (400, "destroyed"), (250, "destroyed"), (190, "damaged-hull"), (0, "healthy-hull") })
        {
            await pair.Server.WaitPost(() =>
            {
                var specifier = new DamageSpecifier();
                specifier.DamageDict.Add("Blunt", damage - last);
                pair.Server.EntMan.System<DamageableSystem>().TryChangeDamage(drone, specifier, ignoreResistances: true, ignoreGlobalModifiers: true);
            });
            last = damage;
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var clientDrone = entities.GetEntity(netDrone);
                Assert.That(entities.System<SpriteSystem>().LayerGetRsiState((clientDrone, null), 0).ToString(), Is.EqualTo(state));
                var turret = entities.GetComponent<CMUCombatDroneComponent>(clientDrone).TurretVisual!.Value;
                Assert.That(entities.GetComponent<SpriteComponent>(turret)[0].Visible, Is.EqualTo(state != "destroyed"));
                if (state != "destroyed")
                    Assert.That(entities.System<SpriteSystem>().LayerGetRsiState((turret, null), 0).ToString(), Is.EqualTo(state.Replace("hull", "turret")));
            });
        }
        await pair.CleanReturnAsync();
    }

    private static List<EntityUid> GetClawEffects(IEntityManager entities, EntityUid drone)
    {
        var effects = new List<EntityUid>();
        var query = entities.EntityQueryEnumerator<SpriteComponent, PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var transform))
        {
            if (transform.ParentUid == drone)
                effects.Add(uid);
        }
        return effects;
    }

    private static void AssertHullVisual(IEntityManager entities, NetEntity netHull, Color color)
    {
        var hull = entities.GetEntity(netHull);
        var sprite = entities.GetComponent<SpriteComponent>(hull);
        Assert.That(entities.System<SpriteSystem>().LayerGetRsiState((hull, sprite), 0).ToString(), Is.EqualTo("body"),
            "Both flamer assembly stages must use a state in flamer_ugv.rsi, not inherited gun UGV states.");
        Assert.That(sprite[0].Color, Is.EqualTo(color), "Installing the incinerator must restore the hull's full color.");
    }

    private static int AmmoCount(IEntityManager entities, EntityUid drone)
    {
        var ev = new GetAmmoCountEvent();
        entities.EventBus.RaiseLocalEvent(drone, ref ev);
        return ev.Count;
    }

    private static void Interact(IEntityManager entities, EntityUid user, EntityUid used, EntityUid target)
    {
        var ev = new InteractUsingEvent(user, used, target, entities.GetComponent<TransformComponent>(target).Coordinates);
        entities.EventBus.RaiseLocalEvent(target, ev);
        Assert.That(ev.Handled, Is.True);
    }
}
