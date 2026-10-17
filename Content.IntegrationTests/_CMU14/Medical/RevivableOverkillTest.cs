using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Gibbing;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
[TestOf(typeof(RMCGibSystem))]
public sealed class RevivableOverkillTest : GameTest
{
    [Test]
    public async Task DamageOverkillProtectsCriticalTargetButNotAliveCatastropheOrExplicitDeath()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        EntityUid protectedTarget = default;
        EntityUid aliveTarget = default;
        EntityUid explicitTarget = default;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;
            var mobState = server.System<MobStateSystem>();
            var damageable = server.System<DamageableSystem>();

            protectedTarget = SpawnCertainGibHuman();
            mobState.ChangeMobState(protectedTarget, MobState.Critical);
            damageable.TryChangeDamage(protectedTarget, Damage(1000), ignoreResistances: true);

            aliveTarget = SpawnCertainGibHuman();
            damageable.TryChangeDamage(aliveTarget, Damage(1000), ignoreResistances: true);

            explicitTarget = SpawnCertainGibHuman();
            mobState.ChangeMobState(explicitTarget, MobState.Critical);
            mobState.ChangeMobState(explicitTarget, MobState.Dead);

            EntityUid SpawnCertainGibHuman()
            {
                var target = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                var gib = entities.EnsureComponent<RMCGibOnDeathComponent>(target);
                gib.GibChance = 1f;
                gib.DamageGibMultiplier = 0f;
                entities.Dirty(target, gib);
                return target;
            }
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.Deleted(protectedTarget), Is.False,
                    "damage taking an already-critical revivable target to dead must not gib it");
                Assert.That(server.System<MobStateSystem>().IsDead(protectedTarget), Is.True);
                Assert.That(server.EntMan.Deleted(aliveTarget), Is.True,
                    "a catastrophic damage hit against a living target must retain its existing gib outcome");
                Assert.That(server.EntMan.Deleted(explicitTarget), Is.True,
                    "an explicitly invoked death transition must retain its existing gib outcome");
            });
        });
    }

    [Test]
    public async Task ExplosionGibUsesStateFromBeforeDamage()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        EntityUid protectedTarget = default;
        EntityUid aliveTarget = default;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;
            var mobState = server.System<MobStateSystem>();

            protectedTarget = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            mobState.ChangeMobState(protectedTarget, MobState.Critical);
            var criticalExplosion = new ExplosionReceivedEvent(
                "RMCOB",
                MapCoordinates.Nullspace,
                Damage(1000),
                MobState.Critical);
            entities.EventBus.RaiseLocalEvent(protectedTarget, ref criticalExplosion);

            aliveTarget = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            mobState.ChangeMobState(aliveTarget, MobState.Dead);
            var aliveExplosion = new ExplosionReceivedEvent(
                "RMCOB",
                MapCoordinates.Nullspace,
                Damage(1000),
                MobState.Alive);
            entities.EventBus.RaiseLocalEvent(aliveTarget, ref aliveExplosion);
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.Deleted(protectedTarget), Is.False,
                    "a blast against an already-critical revivable target must not gib it");
                Assert.That(server.EntMan.Deleted(aliveTarget), Is.True,
                    "a blast that struck a living target must retain its catastrophic gib outcome");
            });
        });
    }

    private static DamageSpecifier Damage(float blunt)
    {
        return new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = FixedPoint2.New(blunt),
            },
        };
    }
}
