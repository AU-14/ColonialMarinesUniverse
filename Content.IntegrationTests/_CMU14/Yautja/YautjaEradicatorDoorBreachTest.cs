using Content.Server.Explosion.EntitySystems;
using Robust.Shared.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Explosion.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

// CMU14 Test: the plasma eradicator's detonation behaviour.
//
// Reference: cmss13-devs/cmss13. aoe_lethal has no impact visual of its own - it only calls cell_explosion - and
// /obj/structure/machinery/door/ex_act qdels a closed door for any severity above EXPLOSION_THRESHOLD_LOW (100),
// so doors die to the blast rather than to the projectile.
//
// CMU specifics under test:
//  - TriggerOnCollide raises one AttemptTriggerEvent per colliding fixture and ExplodeOnTriggerSystem explodes on
//    every trigger, so the bolt carries a latch and must detonate exactly once.
//  - Door destruction is driven by the detonation (TriggerEvent), sweeping the blast radius, not by a direct hit.
[TestFixture]
public sealed class YautjaEradicatorDoorBreachTest
{
    private static EntityUid SpawnBolt(IEntityManager entMan, EntityCoordinates coords)
    {
        return entMan.SpawnEntity("CMUYautjaCasterEradicatorBolt", coords);
    }

    [Test]
    public async Task EradicatorDetonatesOnlyOncePerBolt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bolt = SpawnBolt(entMan, map.GridCoords);

            // Two collision attempts on the same bolt, as TriggerOnCollide does for a wall plus its neighbours.
            var first = new AttemptTriggerEvent(null);
            entMan.EventBus.RaiseLocalEvent(bolt, ref first);
            var second = new AttemptTriggerEvent(null);
            entMan.EventBus.RaiseLocalEvent(bolt, ref second);

            Assert.Multiple(() =>
            {
                Assert.That(first.Cancelled, Is.False, "The first collision detonates the eradicator.");
                Assert.That(second.Cancelled, Is.True,
                    "A second collision in the same tick is cancelled, so the blast happens exactly once.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EradicatorDetonationBreachesNearbyOrdinaryAirlock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var triggers = entMan.System<TriggerSystem>();

            // The bolt lands beside the door, not on it: the explosion has to do the breaching.
            var door = entMan.SpawnEntity("CMAirlockShuttle", map.GridCoords);
            var bolt = SpawnBolt(entMan, map.GridCoords.Offset(new System.Numerics.Vector2(0.5f, 0)));

            triggers.Trigger(bolt);

            Assert.That(entMan.IsQueuedForDeletion(door), Is.True,
                "An ordinary airlock (1000 threshold) inside the blast is destroyed by the detonation itself.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EradicatorDetonationSparesReinforcedDoor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var triggers = entMan.System<TriggerSystem>();

            var reinforced = entMan.SpawnEntity("CMAirlockMaintReinforced", map.GridCoords);
            var bolt = SpawnBolt(entMan, map.GridCoords.Offset(new System.Numerics.Vector2(0.5f, 0)));

            triggers.Trigger(bolt);

            Assert.That(entMan.IsQueuedForDeletion(reinforced), Is.False,
                "The reinforced maintenance hatch (1200 threshold) survives one blast.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EradicatorDetonationCriticallyInjuresAnAdjacentPerson()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var human = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var triggers = entMan.System<TriggerSystem>();

            human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bolt = SpawnBolt(entMan, map.GridCoords.Offset(new System.Numerics.Vector2(0.5f, 0)));

            triggers.Trigger(bolt);
        });

        // Explosions are queued, so the damage lands on later ticks.
        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            Assert.That(mobState.IsAlive(human), Is.False,
                "A person standing in the blast is left crit or dead, not merely hurt.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EradicatorBlastReachesOneTileFurtherThanBefore()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<Robust.Shared.Prototypes.IPrototypeManager>();
            var explosions = pair.Server.EntMan.System<ExplosionSystem>();

            var factory = pair.Server.ResolveDependency<IComponentFactory>();
            Assert.That(prototypes.TryIndex<EntityPrototype>("CMUYautjaCasterEradicatorBolt", out var proto), Is.True);
            Assert.That(proto!.TryComp<ExplosiveComponent>(out var explosive, factory), Is.True);

            // Uses the engine's own cone maths rather than restating the YAML: R = cbrt(3T / (slope * pi)) here.
            var current = explosions.IntensityToRadius(
                explosive!.TotalIntensity, explosive.IntensitySlope, explosive.MaxIntensity);
            var previous = explosions.IntensityToRadius(190f, 6f, 190f);

            Assert.That(current - previous, Is.EqualTo(1f).Within(0.1f),
                "The blast radius is one tile larger than the pre-adjustment 190/6/190 configuration.");
        });

        await pair.CleanReturnAsync();
    }
}
