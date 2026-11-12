using System.Linq;
using Content.Server.CMU14.Yautja;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Body;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.EntityTable;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaRanksPreserveTest
{
    [Test]
    public async Task DeadYautjaCantBeRevivedButHumansCan()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var pred = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);

            mobState.ChangeMobState(pred, MobState.Critical);
            Assert.That(entMan.HasComponent<UnrevivableComponent>(pred), Is.False, "crit isn't death");

            mobState.ChangeMobState(pred, MobState.Dead);
            mobState.ChangeMobState(human, MobState.Dead);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<UnrevivableComponent>(pred), Is.True);
                Assert.That(entMan.GetComponent<UnrevivableComponent>(pred).FromRevivalTimer, Is.False,
                    "cryo must not be able to undo it");
                Assert.That(entMan.HasComponent<UnrevivableComponent>(human), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaLanceGibsHumansOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pred = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);

            void Hit(EntityUid target)
            {
                var bolt = entMan.SpawnEntity("CMUYautjaCasterLanceBolt", MapCoordinates.Nullspace);
                var ev = new ProjectileHitEvent(new DamageSpecifier(), target);
                entMan.EventBus.RaiseLocalEvent(bolt, ref ev);
                entMan.DeleteEntity(bolt);
            }

            Hit(pred);
            Hit(human);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.Deleted(human) || entMan.IsQueuedForDeletion(human), Is.True, "human gibbed");
                Assert.That(entMan.Deleted(pred) || entMan.IsQueuedForDeletion(pred), Is.False, "pred survives the gib");
                Assert.That(entMan.HasComponent<BodyComponent>(pred), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ImmobilizerSparesWhoeverFiredIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var shooter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bolt = entMan.SpawnEntity("CMUYautjaCasterImmobilizerBolt", map.GridCoords);
            entMan.System<SharedProjectileSystem>()
                .SetShooter(bolt, entMan.GetComponent<ProjectileComponent>(bolt), shooter);

            var ev = new ProjectileHitEvent(new DamageSpecifier(), target, shooter);
            entMan.EventBus.RaiseLocalEvent(bolt, ref ev);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<StunnedComponent>(target), Is.True, "the area stun still lands");
                Assert.That(entMan.HasComponent<StunnedComponent>(shooter), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveSerpentsGetTheirOwnHive()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var console = entMan.System<YautjaHuntConsoleSystem>();
            var first = entMan.SpawnEntity("CMXenoWarrior", map.GridCoords);
            var second = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var primeHive = entMan.GetComponentOrNull<HiveMemberComponent>(first)?.Hive;

            console.AssignPreserveHive(first);
            console.AssignPreserveHive(second);

            var hive = entMan.GetComponent<HiveMemberComponent>(first).Hive;
            Assert.Multiple(() =>
            {
                Assert.That(hive, Is.Not.Null);
                Assert.That(hive, Is.Not.EqualTo(primeHive));
                Assert.That(entMan.GetComponent<MetaDataComponent>(hive!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaPreserveHive"));
                Assert.That(entMan.GetComponent<HiveMemberComponent>(second).Hive, Is.EqualTo(hive),
                    "one preserve hive per round, not one per call");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ResearchBracerNeverRollsOutsideAHunt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var tables = entMan.System<EntityTableSystem>();
            var proto = server.ProtoMan.Index<EntityTablePrototype>("CMUCrateSecureWYRandomExp");

            var query = new YautjaHuntActiveQueryEvent();
            entMan.EventBus.RaiseEvent(EventSource.Local, ref query);
            Assert.That(query.Active, Is.False, "no hunt rule in a plain test round");
            for (var i = 0; i < 2000; i++)
            {
                Assert.That(tables.GetSpawns(proto).Select(id => id.Id), Does.Not.Contain("CMUYautjaResearchBracer"));
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ResearchBracerStaysOffTheTrackerEarly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var menu = entMan.System<YautjaBracerMenuSystem>();
            var research = entMan.SpawnEntity("CMUYautjaResearchBracer", map.GridCoords);
            var normal = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<YautjaTrackedItemComponent>(research), Is.True);
                Assert.That(menu.ShouldHideFromTracker(research), Is.True);
                Assert.That(menu.ShouldHideFromTracker(normal), Is.False);
            });

            // past the mark it shows up like any other lost tech
            entMan.GetComponent<YautjaTrackerHiddenUntilComponent>(research).RoundTime = TimeSpan.Zero;
            Assert.That(menu.ShouldHideFromTracker(research), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
