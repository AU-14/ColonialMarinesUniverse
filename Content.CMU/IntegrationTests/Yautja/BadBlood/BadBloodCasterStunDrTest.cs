using Content.Shared._RMC14.Slow;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// COUPLING-601: a Bad Blood caster bolt stuns, then for the stun plus 6 s another Bad Blood stun or immobilizer bolt
// only slows for 2 s. The hunters' bolts have no window and refresh the stun on every hit, as before.
// A refreshed stun is told apart from the first by its end time, so a few ticks pass between the bolts. Every bolt is
// spawned right before it hits, away from the mobs, so physics never gets to collide it with anything.
[TestFixture]
public sealed class BadBloodCasterStunDrTest
{
    private const string BadBloodStunBolt = "CMUYautjaBadBloodCasterStunBolt";
    private const string BadBloodImmobilizerBolt = "CMUYautjaBadBloodCasterImmobilizerBolt";
    private const string HunterStunBolt = "CMUYautjaCasterStunBolt";
    private const int TicksBetweenBolts = 5;

    [Test]
    public async Task ASecondBoltInsideTheWindowOnlySlows()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid shooter = default;
        EntityUid marine = default;
        EntityUid bolt = default;
        EntityUid sphere = default;
        TimeSpan? firstStunEnds = null;
        var firstUntil = TimeSpan.Zero;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            // the shooter stands well outside the immobilizer's radius
            shooter = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords.Offset(new Vector2(10, 0)));
            marine = entMan.SpawnEntity(BadBloodTestHelpers.MarinePrototype, map.GridCoords.Offset(new Vector2(1, 0)));
            bolt = entMan.SpawnEntity(BadBloodStunBolt, map.GridCoords);
            var now = server.Timing.CurTime;

            // bolt 1 stuns for 3 s and opens the window
            Hit(entMan, bolt, marine, shooter);
            firstStunEnds = StunEnds(entMan, marine);
            Assert.That(firstStunEnds, Is.EqualTo(now + TimeSpan.FromSeconds(3)), "the first bolt stuns for 3 s");
            Assert.That(entMan.HasComponent<RMCSlowdownComponent>(marine), Is.False, "a stun is not a slow");
            firstUntil = entMan.GetComponent<CMUCasterStunImmunityComponent>(marine).Until;
            Assert.That(firstUntil, Is.EqualTo(now + TimeSpan.FromSeconds(3) + TimeSpan.FromSeconds(6)),
                "immune for the 3 s stun plus the 6 s window");
        });

        await pair.RunTicksSync(TicksBetweenBolts);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var now = server.Timing.CurTime;
            Assert.That(now, Is.GreaterThan(firstStunEnds!.Value - TimeSpan.FromSeconds(3)), "Test setup: time moved on");

            // bolt 2 inside the window: the stun is not refreshed, a 2 s slow lands instead
            if (entMan.EntityExists(bolt))
                entMan.DeleteEntity(bolt);
            bolt = entMan.SpawnEntity(BadBloodStunBolt, map.GridCoords);
            Hit(entMan, bolt, marine, shooter);
            Assert.That(StunEnds(entMan, marine), Is.EqualTo(firstStunEnds), "a second bolt inside the window must not stun");
            Assert.That(entMan.GetComponent<RMCSlowdownComponent>(marine).ExpiresAt,
                Is.EqualTo(now + TimeSpan.FromSeconds(2)).Within(TimeSpan.FromMilliseconds(1)),
                "it slows for 2 s instead");
            var immunity = entMan.GetComponent<CMUCasterStunImmunityComponent>(marine);
            Assert.That(immunity.Until, Is.EqualTo(firstUntil), "a slowed hit does not extend the window");

            // the immobilizer shares the window
            sphere = entMan.SpawnEntity(BadBloodImmobilizerBolt, map.GridCoords.Offset(new Vector2(1, 0)));
            Hit(entMan, sphere, marine, shooter);
            Assert.That(StunEnds(entMan, marine), Is.EqualTo(firstStunEnds),
                "a Bad Blood immobilizer inside the window must not stun either");

            // once the window has run out, the next bolt stuns again and opens a new window
            immunity.Until = now - TimeSpan.FromSeconds(1);
            Hit(entMan, bolt, marine, shooter);
            Assert.That(StunEnds(entMan, marine), Is.EqualTo(now + TimeSpan.FromSeconds(3)),
                "after the window a bolt stuns again");
            Assert.That(immunity.Until, Is.EqualTo(now + TimeSpan.FromSeconds(9)));

            foreach (var uid in new[] { shooter, marine, bolt, sphere })
            {
                if (entMan.EntityExists(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterBoltsStunEveryHitAndIgnoreTheWindow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid shooter = default;
        EntityUid marine = default;
        EntityUid other = default;
        EntityUid hunterBolt = default;
        EntityUid badBloodBolt = default;
        TimeSpan? marineStunEnds = null;
        TimeSpan? otherStunEnds = null;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            shooter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(10, 0)));
            marine = entMan.SpawnEntity(BadBloodTestHelpers.MarinePrototype, map.GridCoords.Offset(new Vector2(1, 0)));
            other = entMan.SpawnEntity(BadBloodTestHelpers.MarinePrototype, map.GridCoords.Offset(new Vector2(2, 0)));
            hunterBolt = entMan.SpawnEntity(HunterStunBolt, map.GridCoords);
            badBloodBolt = entMan.SpawnEntity(BadBloodStunBolt, map.GridCoords);

            Hit(entMan, hunterBolt, marine, shooter);
            marineStunEnds = StunEnds(entMan, marine);
            Assert.That(marineStunEnds, Is.Not.Null);
            Assert.That(entMan.HasComponent<CMUCasterStunImmunityComponent>(marine), Is.False,
                "the hunters' stun bolt leaves no immunity behind");

            Hit(entMan, badBloodBolt, other, shooter);
            otherStunEnds = StunEnds(entMan, other);
            Assert.That(entMan.HasComponent<CMUCasterStunImmunityComponent>(other), Is.True);
        });

        await pair.RunTicksSync(TicksBetweenBolts);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (entMan.EntityExists(hunterBolt))
                entMan.DeleteEntity(hunterBolt);
            hunterBolt = entMan.SpawnEntity(HunterStunBolt, map.GridCoords);

            // a second hunter bolt refreshes the stun, as it always did
            Hit(entMan, hunterBolt, marine, shooter);
            Assert.That(StunEnds(entMan, marine), Is.GreaterThan(marineStunEnds!.Value),
                "the hunters' stun bolt has no diminishing returns");

            // and it still stuns a target a Bad Blood bolt just made immune
            Hit(entMan, hunterBolt, other, shooter);
            Assert.That(StunEnds(entMan, other), Is.GreaterThan(otherStunEnds!.Value),
                "the hunters' bolt ignores the Bad Blood window");

            foreach (var uid in new[] { shooter, marine, other, hunterBolt, badBloodBolt })
            {
                if (entMan.EntityExists(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static TimeSpan? StunEnds(IEntityManager entMan, EntityUid target)
    {
        return entMan.System<StatusEffectsSystem>().TryGetTime(target, SharedStunSystem.StunId, out var stun)
            ? stun.EndEffectTime
            : null;
    }

    private static void Hit(IEntityManager entMan, EntityUid projectile, EntityUid target, EntityUid shooter)
    {
        var damage = new DamageSpecifier(entMan.GetComponent<ProjectileComponent>(projectile).Damage);
        var hit = new ProjectileHitEvent(damage, target, shooter);
        entMan.EventBus.RaiseLocalEvent(projectile, ref hit);
    }
}
