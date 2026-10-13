using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Punch;
using Content.Shared.Actions;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Abilities;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoAbilityTest
{
    private const string Lab = "CMUWendigoLab";
    private const string Human = "CMMobHuman";
    private const string Item = "CMScrewdriver";

    // (a) a pounce hit strips the target's hands on top of the leap's own paralysis; allies are never hit.
    [Test]
    public async Task PounceHitDropsHeldItemsAndSparesAllies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wendigo = default, ally = default, human = default, item = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            wendigo = entMan.SpawnEntity(Lab, map.GridCoords);
            ally = entMan.SpawnEntity(Lab, map.GridCoords.Offset(new Vector2(0, 1)));
            human = entMan.SpawnEntity(Human, map.GridCoords.Offset(new Vector2(1, 0)));
            item = entMan.SpawnEntity(Item, map.GridCoords.Offset(new Vector2(1, 0)));
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(human, item), Is.True);

            var allyAttempt = new XenoLeapHitAttempt(wendigo);
            entMan.EventBus.RaiseLocalEvent(ally, ref allyAttempt);
            Assert.That(allyAttempt.Cancelled, Is.True, "a Wendigo pounce must not hit a Wendigo ally (it stops there, like a xeno pouncing a hive mate)");

            var humanAttempt = new XenoLeapHitAttempt(wendigo);
            entMan.EventBus.RaiseLocalEvent(human, ref humanAttempt);
            Assert.That(humanAttempt.Cancelled, Is.False, "a Wendigo pounce must still hit a human");

            // The hit event alone (raised on the leaper by XenoLeapSystem.ApplyLeapingHitEffects) strips the hands.
            var leaping = entMan.EnsureComponent<XenoLeapingComponent>(wendigo);
            var hit = new XenoLeapHitEvent(leaping, human);
            entMan.EventBus.RaiseLocalEvent(wendigo, ref hit);
            entMan.RemoveComponent<XenoLeapingComponent>(wendigo);
            Assert.That(hands.IsHolding(human, item), Is.False, "the pounce hit didn't drop the held item");

            // The leap's own paralysis (XenoLeapSystem, server-side, not forced) puts the human down.
            var knockdown = entMan.GetComponent<XenoLeapComponent>(wendigo).KnockdownTime;
            Assert.That(entMan.System<SharedStunSystem>().TryParalyze(human, knockdown, true), Is.True);
            Assert.That(entMan.HasComponent<KnockedDownComponent>(human), Is.True, "the pounced human isn't down");
        });

        await pair.CleanReturnAsync();
    }

    // (b) punch swing only adds the punch while toggled on, and then puts Punch on cooldown.
    [Test]
    public async Task PunchSwingPunchesOnlyWhenToggled()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wendigo = default, human = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            wendigo = entMan.SpawnEntity(Lab, map.GridCoords);
            human = entMan.SpawnEntity(Human, map.GridCoords.Offset(new Vector2(1, 0)));
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var actions = entMan.System<SharedActionsSystem>();
            var punchActions = entMan.System<SharedRMCActionsSystem>().GetActionsWithEvent<XenoPunchActionEvent>(wendigo).ToList();
            var swing = entMan.GetComponent<CMUWendigoPunchSwingComponent>(wendigo);
            Assert.That(swing.Enabled, Is.False);
            Assert.That(punchActions, Is.Not.Empty, "the lab Wendigo has no Punch action");

            var before = damageable.GetTotalDamage(human);
            var untoggled = new MeleeHitEvent(new List<EntityUid> { human }, wendigo, wendigo, new DamageSpecifier(), new Vector2(1, 0));
            entMan.EventBus.RaiseLocalEvent(wendigo, untoggled);
            Assert.That(damageable.GetTotalDamage(human), Is.EqualTo(before), "an untoggled swing punched");
            Assert.That(punchActions.Any(a => actions.IsCooldownActive(a.Comp)), Is.False);

            entMan.System<CMUWendigoPunchSwingSystem>().SetEnabled((wendigo, swing), true);
            var toggled = new MeleeHitEvent(new List<EntityUid> { human }, wendigo, wendigo, new DamageSpecifier(), new Vector2(1, 0));
            entMan.EventBus.RaiseLocalEvent(wendigo, toggled);
            Assert.That(damageable.GetTotalDamage(human), Is.GreaterThan(before), "a toggled wide swing didn't punch");
            Assert.That(punchActions.All(a => actions.IsCooldownActive(a.Comp)), Is.True, "Punch wasn't put on cooldown");
        });

        await pair.CleanReturnAsync();
    }

    // (c) the sweep cone hits the human in front, not the one behind, and never an allied Wendigo.
    [Test]
    public async Task ClawedSweepHitsOnlyEnemiesInTheCone()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wendigo = default, ally = default, front = default, behind = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            wendigo = entMan.SpawnEntity(Lab, map.GridCoords);
            front = entMan.SpawnEntity(Human, map.GridCoords.Offset(new Vector2(3, 0)));
            behind = entMan.SpawnEntity(Human, map.GridCoords.Offset(new Vector2(-3, 0)));
            ally = entMan.SpawnEntity(Lab, map.GridCoords.Offset(new Vector2(2, 1.5f)));
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var frontBefore = damageable.GetTotalDamage(front);
            var behindBefore = damageable.GetTotalDamage(behind);
            var allyBefore = damageable.GetTotalDamage(ally);

            var hit = entMan.System<CMUClawedSweepSystem>().PerformSweep(wendigo, new Vector2(1, 0).ToWorldAngle());

            Assert.Multiple(() =>
            {
                Assert.That(hit, Does.Contain(front));
                Assert.That(hit, Does.Not.Contain(behind));
                Assert.That(hit, Does.Not.Contain(ally));
                // Several strikes per target: more than (strikes - 1) strikes' worth must land on an unarmored human.
                var sweep = entMan.GetComponent<CMUClawedSweepComponent>(wendigo);
                var perStrike = sweep.Damage.GetTotal();
                Assert.That(sweep.Strikes, Is.GreaterThan(1), "the sweep must rake several limbs");
                Assert.That(damageable.GetTotalDamage(front) - frontBefore, Is.GreaterThan(perStrike * (sweep.Strikes - 1)),
                    "the human in front was not hit by every strike");
                Assert.That(entMan.HasComponent<KnockedDownComponent>(front), Is.True, "the human in front isn't down");
                Assert.That(damageable.GetTotalDamage(behind), Is.EqualTo(behindBefore), "the human behind was hit");
                Assert.That(entMan.HasComponent<KnockedDownComponent>(behind), Is.False);
                Assert.That(damageable.GetTotalDamage(ally), Is.EqualTo(allyBefore), "the allied Wendigo was hit");
            });
        });

        await pair.CleanReturnAsync();
    }
}
