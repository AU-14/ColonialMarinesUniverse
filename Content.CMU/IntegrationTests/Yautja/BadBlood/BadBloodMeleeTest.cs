#pragma warning disable RA0002 // the tests set bracer charge and backdate the Blooded-kill mark directly
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Weapons.Melee.Events;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-604, ECS-609, STATE-603: a Bad Blood melee weapon staggers a living marine (once per cooldown), pays bracer
// charge back per hit on one, and a worthy kill within 5 s of the hit is a Blooded kill worth +1 Honour. The
// honourable weapons do none of it.
[TestFixture]
public sealed class BadBloodMeleeTest
{
    private const string BadBloodScimitar = "CMUYautjaBadBloodScimitar";
    private const string BadBloodBlades = "CMUYautjaBadBloodWristBlades";
    private const string HunterScimitar = "CMUYautjaScimitar";
    private const string HunterBlades = "CMUYautjaWristBlades";
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";

    [Test]
    public async Task TheScimitarStaggersAMarineOncePerCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid grunt = default;
        EntityUid marine = default;
        EntityUid scimitar = default;
        var firstSlowEnds = TimeSpan.Zero;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            grunt = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
            marine = BadBloodTestHelpers.SpawnMarineNear(server, map.GridCoords);
            scimitar = entMan.SpawnEntity(BadBloodScimitar, map.GridCoords);
            var honourable = entMan.SpawnEntity(HunterScimitar, map.GridCoords);
            var otherMarine = BadBloodTestHelpers.SpawnMarineNear(server, map.GridCoords, new Vector2(0, 2));
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 4)));
            Assert.That(entMan.HasComponent<RMCSpeciesSlowdownModifierComponent>(marine), Is.True,
                "Test setup: a marine can be slowed");

            Hit(entMan, scimitar, grunt, marine);
            firstSlowEnds = entMan.GetComponent<RMCSlowdownComponent>(marine).ExpiresAt;
            Assert.That(firstSlowEnds,
                Is.EqualTo(server.Timing.CurTime + TimeSpan.FromSeconds(1.5)).Within(TimeSpan.FromMilliseconds(1)),
                "a Bad Blood scimitar hit staggers a marine for 1.5 s");

            // the honourable scimitar is unchanged: no stagger, no mark
            Hit(entMan, honourable, grunt, otherMarine);
            Assert.That(entMan.HasComponent<RMCSlowdownComponent>(otherMarine), Is.False,
                "an honourable scimitar does not stagger");
            Assert.That(entMan.HasComponent<BadBloodMeleeMarkComponent>(otherMarine), Is.False);

            // the clan's own kind is never staggered
            Hit(entMan, scimitar, grunt, hunter);
            Assert.That(entMan.HasComponent<RMCSlowdownComponent>(hunter), Is.False, "a Yautja is not staggered");

            foreach (var uid in new[] { honourable, otherMarine, hunter })
            {
                entMan.DeleteEntity(uid);
            }
        });

        // inside the 2.5 s cooldown, while the first stagger still runs
        await pair.RunSeconds(1f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Hit(entMan, scimitar, grunt, marine);
            Assert.That(entMan.GetComponent<RMCSlowdownComponent>(marine).ExpiresAt, Is.EqualTo(firstSlowEnds),
                "a second hit inside the cooldown does not extend the stagger");
        });

        // past the cooldown
        await pair.RunSeconds(2f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Hit(entMan, scimitar, grunt, marine);
            Assert.That(entMan.GetComponent<RMCSlowdownComponent>(marine).ExpiresAt,
                Is.EqualTo(server.Timing.CurTime + TimeSpan.FromSeconds(1.5)).Within(TimeSpan.FromMilliseconds(1)),
                "after the cooldown the next hit staggers again");

            foreach (var uid in new[] { grunt, marine, scimitar })
            {
                if (entMan.EntityExists(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BladeHitsOnALivingMarineRefundBracerCharge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var grunt = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
            if (inventory.TryUnequip(grunt, "gloves", out var oldBracer, silent: true, force: true))
                entMan.DeleteEntity(oldBracer.Value);

            var bracer = entMan.SpawnEntity(BadBloodBracer, map.GridCoords);
            Assert.That(inventory.TryEquip(grunt, bracer, "gloves", silent: true, force: true), Is.True);
            var blades = entMan.SpawnEntity(BadBloodBlades, map.GridCoords);
            var honourable = entMan.SpawnEntity(HunterBlades, map.GridCoords);
            var marine = BadBloodTestHelpers.SpawnMarineNear(server, map.GridCoords);
            var corpse = BadBloodTestHelpers.SpawnMarineNear(server, map.GridCoords, new Vector2(0, 2));
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 4)));
            entMan.System<MobStateSystem>().ChangeMobState(corpse, MobState.Dead);

            try
            {
                var charge = entMan.GetComponent<YautjaBracerComponent>(bracer);
                var start = FixedPoint2.New(100);
                charge.Charge = start;

                Hit(entMan, blades, grunt, marine);
                Assert.That(charge.Charge, Is.EqualTo(start + 10), "a blade hit on a marine pays 10 charge");

                Hit(entMan, blades, grunt, hunter);
                Assert.That(charge.Charge, Is.EqualTo(start + 10), "a hit on a Yautja pays nothing");

                Hit(entMan, blades, grunt, corpse);
                Assert.That(charge.Charge, Is.EqualTo(start + 10), "a hit on a corpse pays nothing");

                Hit(entMan, honourable, grunt, marine);
                Assert.That(charge.Charge, Is.EqualTo(start + 10), "the honourable blades pay nothing");

                // clamped to the bracer's maximum
                charge.Charge = charge.MaxCharge - 5;
                Hit(entMan, blades, grunt, marine);
                Assert.That(charge.Charge, Is.EqualTo(charge.MaxCharge));
            }
            finally
            {
                foreach (var uid in new[] { grunt, blades, honourable, marine, corpse, hunter })
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AWorthyKillSoonAfterAMeleeHitIsBlooded()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(pair, map, grunts: 1);
        var server = pair.Server;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var rules = server.System<BadBloodCampRuleSystem>();
                var mobState = server.System<MobStateSystem>();
                Assert.That(rules.TryGetRule(out _), Is.True, "Test setup: the Bad Blood rule runs");

                var grunt = party.Grunts[0];
                var blades = entMan.SpawnEntity(BadBloodBlades, map.GridCoords);
                var honourable = entMan.SpawnEntity(HunterBlades, map.GridCoords);
                var spawned = new List<EntityUid> { blades, honourable };

                EntityUid Marine(int row)
                {
                    var marine = BadBloodTestHelpers.SpawnMarineNear(server, map.GridCoords, new Vector2(0, 2 + row));
                    spawned.Add(marine);
                    return marine;
                }

                try
                {
                    var honour = rules.GetHonour(grunt);

                    // a blade hit, then the marine dies within the window
                    var blooded = Marine(0);
                    Hit(entMan, blades, grunt, blooded);
                    mobState.ChangeMobState(blooded, MobState.Dead);
                    // CMU14 STATE-701: a grunt (no caste, not the Hunt Leader) earns 1 more
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(honour + FixedPoint4.New(2)),
                        "a worthy kill within 5 s of the hit is +1 Honour, +1 more for a grunt");
                    honour = rules.GetHonour(grunt);

                    // a caste-holder earns the plain +1
                    Assert.That(server.System<BadBloodCasteSystem>().TryTakeCaste(grunt, "Tracker"), Is.True);
                    var casteKill = Marine(3);
                    Hit(entMan, blades, grunt, casteKill);
                    mobState.ChangeMobState(casteKill, MobState.Dead);
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(honour + FixedPoint4.New(1)),
                        "a caste-holder's Blooded kill is +1 Honour");
                    honour = rules.GetHonour(grunt);

                    // the hit was 6 s ago
                    var late = Marine(1);
                    Hit(entMan, blades, grunt, late);
                    entMan.GetComponent<BadBloodMeleeMarkComponent>(late).At -= TimeSpan.FromSeconds(6);
                    mobState.ChangeMobState(late, MobState.Dead);
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(honour), "a kill 6 s after the hit is not Blooded");

                    // no melee hit at all
                    var untouched = Marine(2);
                    mobState.ChangeMobState(untouched, MobState.Dead);
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(honour), "a kill with no melee hit is not Blooded");

                    // an unworthy target: a synth
                    var synth = Marine(3);
                    entMan.EnsureComponent<SynthComponent>(synth);
                    Hit(entMan, blades, grunt, synth);
                    mobState.ChangeMobState(synth, MobState.Dead);
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(honour), "an unworthy kill is not Blooded");

                    // the honourable blades leave no mark
                    var hunterKill = Marine(4);
                    Hit(entMan, honourable, grunt, hunterKill);
                    mobState.ChangeMobState(hunterKill, MobState.Dead);
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(honour), "the honourable blades never pay Honour");
                }
                finally
                {
                    foreach (var uid in spawned)
                    {
                        if (entMan.EntityExists(uid))
                            entMan.DeleteEntity(uid);
                    }
                }
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(pair, party);
        }

        await pair.CleanReturnAsync();
    }

    private static void Hit(IEntityManager entMan, EntityUid weapon, EntityUid user, EntityUid target)
    {
        var hit = new MeleeHitEvent(new List<EntityUid> { target }, user, weapon, new DamageSpecifier(), null);
        entMan.EventBus.RaiseLocalEvent(weapon, hit);
    }
}
