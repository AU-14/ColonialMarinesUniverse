#pragma warning disable RA0002 // reads caster modes, thresholds and slowdowns, and sets the bracer's charge for the test
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Slow;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 ARCH-605: the Berserker caste. Its thresholds are raised by 80, it takes 85% damage, sprints at 90%, staggers
// 1.5x as long with Bad Blood melee, and carries no stun bolt: its caster falls back to the immobilizer instead of
// getting stuck. A Bad Blood without the caste is unchanged.
[TestFixture]
public sealed class BadBloodBerserkerTest : GameTest
{
    private const string Scimitar = "CMUYautjaBadBloodScimitar";
    private static readonly ProtoId<BadBloodCastePrototype> Berserker = "Berserker";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task BerserkerIsTougherSlowerAndStaggersLonger()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var castes = entMan.System<BadBloodCasteSystem>();
                var thresholds = entMan.System<MobThresholdSystem>();
                var (berserker, plain) = (party.Grunts[0], party.Grunts[1]);

                Assert.That(thresholds.TryGetThresholdForState(berserker, MobState.Critical, out var critBefore), Is.True);
                Assert.That(thresholds.TryGetThresholdForState(berserker, MobState.Dead, out var deadBefore), Is.True);
                var move = entMan.GetComponent<MovementSpeedModifierComponent>(berserker);
                var sprintBefore = move.CurrentSprintSpeed;
                var walkBefore = move.CurrentWalkSpeed;

                Assert.That(castes.TryTakeCaste(berserker, Berserker), Is.True);
                Assert.That(entMan.HasComponent<BadBloodBerserkerComponent>(berserker), Is.True, "the caste adds its component");

                Assert.That(thresholds.TryGetThresholdForState(berserker, MobState.Critical, out var critAfter), Is.True);
                Assert.That(thresholds.TryGetThresholdForState(berserker, MobState.Dead, out var deadAfter), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(critAfter, Is.EqualTo(critBefore + FixedPoint2.New(80)), "crit threshold +80");
                    Assert.That(deadAfter, Is.EqualTo(deadBefore + FixedPoint2.New(80)), "dead threshold +80");
                    Assert.That(move.CurrentSprintSpeed, Is.EqualTo(sprintBefore * 0.9f).Within(0.001f), "sprints at 90%");
                    Assert.That(move.CurrentWalkSpeed, Is.EqualTo(walkBefore).Within(0.001f), "walks as before");
                });

                // the same hit on both: the Berserker takes 85% of what another Bad Blood takes
                var berserkerHit = Modify(entMan, berserker);
                var plainHit = Modify(entMan, plain);
                Assert.That(plainHit.Float(), Is.GreaterThan(0f), "Test setup: the hit lands");
                Assert.That(berserkerHit.Float(), Is.EqualTo(plainHit.Float() * 0.85f).Within(0.05f),
                    "a Berserker takes 85% of the damage");

                // a heal is not cut
                Assert.That(Modify(entMan, berserker, -50), Is.EqualTo(Modify(entMan, plain, -50)), "healing is not cut");

                // a Berserker's scimitar staggers 1.5x as long as another Bad Blood's
                var scimitar = entMan.SpawnEntity(Scimitar, map.GridCoords);
                var first = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(0, 2));
                var second = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(0, 4));
                spawned.AddRange(new[] { scimitar, first, second });
                var now = Server.Timing.CurTime;

                Hit(entMan, scimitar, plain, first);
                Hit(entMan, scimitar, berserker, second);
                var plainStagger = entMan.GetComponent<RMCSlowdownComponent>(first).ExpiresAt - now;
                var berserkerStagger = entMan.GetComponent<RMCSlowdownComponent>(second).ExpiresAt - now;
                Assert.That(plainStagger, Is.GreaterThan(TimeSpan.Zero), "Test setup: a Bad Blood hit staggers");
                Assert.That(berserkerStagger.TotalSeconds, Is.EqualTo(plainStagger.TotalSeconds * 1.5).Within(0.001),
                    "a Berserker staggers 1.5x as long");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // CMU14 TEST-01: the caste on a body without thresholds raises nothing and logs no error (teardown fails on error logs)
    [Test]
    public async Task BerserkerOnBodyWithoutThresholdsDoesNothing()
    {
        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bare = entMan.Spawn(null, MapCoordinates.Nullspace);
            Assert.That(entMan.HasComponent<MobThresholdsComponent>(bare), Is.False, "Test setup: no thresholds");

            var comp = entMan.AddComponent<BadBloodBerserkerComponent>(bare);
            Assert.That(comp.ThresholdsRaised, Is.False, "nothing was raised, so shutdown has nothing to take off");

            entMan.RemoveComponent<BadBloodBerserkerComponent>(bare);
            entMan.DeleteEntity(bare);
        });
    }

    [Test]
    public async Task BerserkerCasterHasNoStunBoltAndFallsBackToTheImmobilizer()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var unlocks = Server.System<BadBloodEradicatorUnlockSystem>();
                var (berserker, plain) = (party.Grunts[0], party.Grunts[1]);
                Assert.That(entMan.System<BadBloodCasteSystem>().TryTakeCaste(berserker, Berserker), Is.True);
                unlocks.SyncHolders();

                // another Bad Blood keeps the stun bolt
                Assert.That(Ask(entMan, plain, EntityUid.Invalid, BadBloodEradicatorSystem.StunMode).Cancelled, Is.False,
                    "a Bad Blood without the caste keeps the stun bolt");

                var (caster, casterComp) = DeployedCaster(berserker, out var bracer);
                entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 5000;
                Assert.That(casterComp.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.StunMode),
                    "Test setup: the caster lands on the stun bolt");

                // use in hand only asks: refused, and nothing moves
                Assert.That(Ask(entMan, berserker, caster, BadBloodEradicatorSystem.StunMode).Cancelled, Is.True,
                    "a Berserker carries no stun bolt");
                Assert.That(casterComp.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.StunMode));

                // pulling the trigger on the stun bolt refuses the shot and falls back to the immobilizer
                Assert.That(Shoot(entMan, berserker, caster).Cancelled, Is.True, "no stun bolt is fired");
                Assert.That(casterComp.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.ImmobilizerMode),
                    "the caster falls back to the immobilizer");
                var shot = Shoot(entMan, berserker, caster);
                Assert.That(shot.Cancelled, Is.False, $"the immobilizer fires ({shot.Message})");

                // nothing else earned: use in hand stays on the immobilizer and never steps onto the stun bolt
                Assert.That(Cycle((caster, casterComp), berserker, 3), Is.All.EqualTo(BadBloodEradicatorSystem.ImmobilizerMode));

                // with the lethal bolt the cycle is immobilizer -> lethal -> immobilizer, skipping the stun bolt
                Assert.That(unlocks.UnlockLethal(), Is.True);
                Assert.That(Cycle((caster, casterComp), berserker, 2),
                    Is.EqualTo(new[] { BadBloodEradicatorSystem.LethalMode, BadBloodEradicatorSystem.ImmobilizerMode }));
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    private static FixedPoint2 Modify(IEntityManager entMan, EntityUid target, int amount = 100)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Slash"] = amount;
        var ev = new DamageModifyEvent(damage);
        entMan.EventBus.RaiseLocalEvent(target, ev);
        return ev.Damage.GetTotal();
    }

    private static void Hit(IEntityManager entMan, EntityUid weapon, EntityUid user, EntityUid target)
    {
        var hit = new MeleeHitEvent(new List<EntityUid> { target }, user, weapon, new DamageSpecifier(), null);
        entMan.EventBus.RaiseLocalEvent(weapon, hit);
    }

    private static YautjaCasterModeAttemptEvent Ask(IEntityManager entMan, EntityUid user, EntityUid caster, int mode)
    {
        var attempt = new YautjaCasterModeAttemptEvent(caster, mode) { Silent = true };
        entMan.EventBus.RaiseLocalEvent(user, ref attempt);
        return attempt;
    }

    private static AttemptShootEvent Shoot(IEntityManager entMan, EntityUid user, EntityUid caster)
    {
        var coords = entMan.GetComponent<TransformComponent>(user).Coordinates;
        var attempt = new AttemptShootEvent(user, null, coords, coords);
        entMan.EventBus.RaiseLocalEvent(caster, ref attempt);
        return attempt;
    }

    // the caster the Bad Blood's own bracer carries, marked deployed so it fires from that bracer
    private Entity<YautjaCasterComponent> DeployedCaster(EntityUid badBlood, out EntityUid bracer)
    {
        var entMan = Server.EntMan;
        Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(badBlood, "gloves", out var worn), Is.True,
            "a Bad Blood lands wearing a bracer");
        bracer = worn!.Value;
        var gear = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
        Assert.That(gear.Gear.TryGetValue(YautjaGearKind.Caster, out var caster), Is.True,
            "the bracer carries a plasma caster");
        var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
        stored.Bracer = bracer;
        stored.Deployed = true;
        var comp = entMan.GetComponent<YautjaCasterComponent>(caster);
        Assert.That(comp.CycleEarnedModes, Is.True, "the Bad Blood caster cycles its earned modes");
        return (caster, comp);
    }

    // presses use in hand on the caster that many times and returns the mode after each press
    private int[] Cycle(Entity<YautjaCasterComponent> caster, EntityUid user, int times)
    {
        var modes = new int[times];
        for (var i = 0; i < times; i++)
        {
            Server.EntMan.EventBus.RaiseLocalEvent(caster.Owner, new UseInHandEvent(user));
            modes[i] = caster.Comp.CurrentMode;
        }

        return modes;
    }

    private async Task Cleanup(BadBloodTestParty party, List<EntityUid> spawned)
    {
        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        await Server.WaitPost(() =>
        {
            foreach (var uid in spawned.Concat(party.All))
            {
                if (Server.EntMan.EntityExists(uid))
                    Server.EntMan.DeleteEntity(uid);
            }
        });
    }
}
