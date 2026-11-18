using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Stamina;
using Content.Shared.CMU14.Fireman;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// RB-13 to RB-18 (EW Joe rebalance, node N2): a second hard CC inside the target's window lands at half length,
/// a fired sequence blocks every sequence for the shared cooldown, a held victim's struggle restarts once per hold
/// whatever hits it, and a choke knocks out once per hold.
/// </summary>
[TestFixture]
public sealed class EWJoeCcWindowTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";

    private static TimeSpan KnockdownLeft(IEntityManager entities, IGameTiming timing, EntityUid uid)
    {
        return entities.GetComponent<KnockedDownComponent>(uid).NextUpdate - timing.CurTime;
    }

    [Test]
    public async Task AFlashStunOpensTheWindowAndIsHalvedInsideIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var abilities = entities.System<EWJoeAbilitySystem>();
            var system = entities.System<EWJoeLiturgySystem>();
            var (joe, _) = EWJoeLiturgyTest.SpawnJoe(entities, map, 2);
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            var victim = entities.SpawnEntity(Human, map.GridCoords);

            // the server's answer to the flash's duration question, outside then inside the window
            var outside = new EWJoeFlashStunEvent(victim, comp.TestOfFaithStun);
            entities.EventBus.RaiseLocalEvent(joe, ref outside);
            Assert.That(outside.Handled, "the Liturgy meter answers the flash");
            Assert.That(outside.Duration, Is.EqualTo(comp.TestOfFaithStun), "full length outside the window");

            Assert.That(abilities.TryFlash((joe, comp), victim), "the flash lands");
            Assert.That(system.IsCcImmune(joe, victim), "and its stun opens the target's window (RB-13)");

            var inside = new EWJoeFlashStunEvent(victim, comp.TestOfFaithStun);
            entities.EventBus.RaiseLocalEvent(joe, ref inside);
            Assert.That(inside.Duration.TotalSeconds,
                Is.EqualTo(comp.TestOfFaithStun.TotalSeconds * entities.GetComponent<EWJoeLiturgyComponent>(joe).CcWindowMultiplier).Within(0.01),
                "a flash inside the window stuns at half");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ASecondHardCcInsideTheWindowLandsAtHalf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, victim = default;
        EWJoeLiturgyComponent liturgy = default!;
        var full = TimeSpan.Zero;
        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();
            var system = entities.System<EWJoeLiturgySystem>();
            (joe, liturgy) = EWJoeLiturgyTest.SpawnJoe(entities, map, 1);
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            victim = entities.SpawnEntity(Human, map.GridCoords);

            combat.Sweep((joe, comp), victim);
            full = KnockdownLeft(entities, timing, victim);
            Assert.That(full, Is.GreaterThan(TimeSpan.Zero), "the first Sweep knocks down at full length");
            Assert.That(system.IsCcImmune(joe, victim), "and opens the window");

            // stand the body up by hand so the second knockdown's own length is measured
            entities.RemoveComponent<KnockedDownComponent>(victim);
            combat.Sweep((joe, comp), victim);
            Assert.That(KnockdownLeft(entities, timing, victim).TotalSeconds,
                Is.EqualTo(full.TotalSeconds * liturgy.CcWindowMultiplier).Within(0.05),
                "a second hard CC inside the window lands at half");

            // a Joe without the meter has no window and does not crash
            var plain = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(plain);
            Assert.That(system.HardCcDuration(plain, victim, TimeSpan.FromSeconds(2)), Is.EqualTo(TimeSpan.FromSeconds(2)),
                "no meter, no window");
        });

        await pair.RunTicksSync(pair.SecondsToTicks((float) liturgy.CcImmunity.TotalSeconds + 0.5f));

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            Assert.That(system.IsCcImmune(joe, victim), Is.False, "the window lapsed");

            if (entities.HasComponent<KnockedDownComponent>(victim))
                entities.RemoveComponent<KnockedDownComponent>(victim);

            entities.System<EWJoeCombatSystem>().Sweep((joe, entities.GetComponent<EWJoeComponent>(joe)), victim);
            Assert.That(KnockdownLeft(entities, timing, victim).TotalSeconds, Is.EqualTo(full.TotalSeconds).Within(0.05),
                "outside the window the hard CC is full length again");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ASequenceBlocksEverySequenceForTheSharedCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid joe = default, second = default;
        EWJoeLiturgyComponent liturgy = default!;
        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            (joe, liturgy) = EWJoeLiturgyTest.SpawnJoe(entities, map, 1);
            var first = entities.SpawnEntity(Human, map.GridCoords);
            second = entities.SpawnEntity(Human, map.GridCoords);

            system.Register(joe, EWJoeCqcInput.Hit, first);
            system.Register(joe, EWJoeCqcInput.Hit, first);
            system.Register(joe, EWJoeCqcInput.Disarm, first);
            Assert.That(entities.HasComponent<KnockedDownComponent>(first), "the first Rebuke fires");

            // a fresh body (no CC window): only the shared cooldown stands in the way
            system.Register(joe, EWJoeCqcInput.Hit, second);
            system.Register(joe, EWJoeCqcInput.Hit, second);
            system.Register(joe, EWJoeCqcInput.Disarm, second);
            Assert.That(entities.HasComponent<KnockedDownComponent>(second), Is.False, "a second sequence inside the cooldown does not fire");
            Assert.That(liturgy.Buffer, Has.Count.EqualTo(3), "but its inputs are buffered");
        });

        await pair.RunTicksSync(pair.SecondsToTicks((float) liturgy.SequenceCooldown.TotalSeconds + 0.3f));

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            system.Register(joe, EWJoeCqcInput.Hit, second);
            system.Register(joe, EWJoeCqcInput.Hit, second);
            system.Register(joe, EWJoeCqcInput.Disarm, second);
            Assert.That(entities.HasComponent<KnockedDownComponent>(second), "after the cooldown the sequence fires");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MacheteHitsRestartAHeldVictimsStruggleOnlyOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid joe = default, victim = default, machete = default;
        await server.WaitAssertion(() =>
        {
            // level 0 and no meter: the hits must not build a CQC sequence that lets go of the victim
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            victim = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 1));
            machete = entities.SpawnEntity(null, map.GridCoords);
            entities.EnsureComponent<EWJoeMacheteComponent>(machete).Joe = joe;
            Assert.That(entities.System<PullingSystem>().TryStartPull(joe, victim), "pull started");
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        var delay = TimeSpan.Zero;
        var restartedAt = TimeSpan.Zero;
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), "gripped");

            var input = entities.EnsureComponent<InputMoverComponent>(victim);
            input.HeldMoveButtons = MoveButtons.Right;
            var move = new MoveInputEvent((victim, input), MoveButtons.None);
            entities.EventBus.RaiseLocalEvent(victim, ref move);
            Assert.That(entities.GetComponent<FiremanCarriableComponent>(victim).BreakingFree, "the victim struggles");
            delay = Struggle(entities, victim).Args.Delay;
        });

        // three machete hits a second apart
        for (var hit = 0; hit < 3; hit++)
        {
            await pair.RunTicksSync(pair.SecondsToTicks(1));

            var index = hit;
            await server.WaitAssertion(() =>
            {
                var cut = new MeleeHitEvent(new List<EntityUid> { victim }, joe, machete, new DamageSpecifier(), null);
                entities.EventBus.RaiseLocalEvent(machete, cut);

                var grip = entities.System<EWJoeMachineGripSystem>();
                var start = Struggle(entities, victim).StartTime;
                Assert.That(grip.StruggleRestarts(victim), Is.EqualTo(1), "one restart per hold, however many hits");
                if (index == 0)
                    restartedAt = start;
                else
                    Assert.That(start, Is.EqualTo(restartedAt), $"hit {index + 1} did not restart the struggle again");
            });
        }

        // the restarted struggle runs to the end: within Delay x 2 of the first attempt
        await pair.RunTicksSync(pair.SecondsToTicks((float) delay.TotalSeconds - 2f + 0.5f));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), Is.False,
                "the victim broke free despite the machete");
            entities.GetComponent<InputMoverComponent>(victim).HeldMoveButtons = MoveButtons.None;
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AChokeKnocksOutOncePerHold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid joe = default, victim = default;
        await server.WaitAssertion(() =>
        {
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            victim = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 1));
            Assert.That(entities.HasComponent<RMCStaminaComponent>(victim), "the victim has RMC stamina");
            Assert.That(entities.System<PullingSystem>().TryStartPull(joe, victim), "pull started");
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1));

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            Assert.That(entities.System<EWJoeCombatSystem>().StartChoke((joe, comp), victim), "the choke starts on the held body");
        });

        // 25 stamina a second: the fourth tick takes the victim to zero, the knockout tier
        await pair.RunTicksSync(pair.SecondsToTicks(6));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.TryGetComponent(joe, out EWJoeChokeComponent? choke), "still choking");
            Assert.That(choke!.KnockedOut, "the choke knocked the victim out");
            Assert.That(entities.System<EWJoeMachineGripSystem>().ChokeKnockedOut(victim), "recorded for the hold");
        });

        // RMC's regeneration lifts the victim out; the choke may not push it back below the floor
        for (var sample = 0; sample < 2; sample++)
        {
            await pair.RunTicksSync(pair.SecondsToTicks(sample == 0 ? 6 : 3));

            await server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<EWJoeChokeComponent>(joe), "the hold continues");
                var stamina = entities.GetComponent<RMCStaminaComponent>(victim);
                var floor = entities.GetComponent<EWJoeChokeComponent>(joe).StaminaFloorAfterKnockout;
                Assert.That(stamina.Current, Is.GreaterThanOrEqualTo(floor - 0.001), "no second knockout from the choke");
                Assert.That(stamina.Level, Is.LessThan(stamina.TierThresholds.Length - 1), "out of the knockout tier");
            });
        }

        await pair.CleanReturnAsync();
    }

    private static Content.Shared.DoAfter.DoAfter Struggle(IEntityManager entities, EntityUid victim)
    {
        return entities.GetComponent<DoAfterComponent>(victim).DoAfters.Values
            .Single(d => !d.Cancelled && !d.Completed && d.Args.Event is BreakFiremanCarryDoAfterEvent);
    }
}
