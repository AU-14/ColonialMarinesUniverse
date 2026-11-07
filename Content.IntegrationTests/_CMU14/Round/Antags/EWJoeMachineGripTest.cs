using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Fireman;
using Content.Shared.CMU14.Fireman;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// N5 (FLOW-02): the Early Warning Joe's one-click telegraphed Machine Grip, the escape multiplier by level, the
/// struggle restart, and the small-caste xeno grab from level 2. The victim's telegraph popup is not asserted: a
/// server-side popup to an entity without a player session is not observable from the test.
/// </summary>
[TestFixture]
public sealed class EWJoeMachineGripTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Drone = "CMXenoDrone";
    private const string Ravager = "CMXenoRavager";
    private const string Praetorian = "CMXenoPraetorian";
    private const string Crusher = "RMCXenoCrusher";
    private const string Queen = "CMXenoQueen";

    private static int Ticks(IGameTiming timing, double seconds)
    {
        return (int) Math.Ceiling(seconds / timing.TickPeriod.TotalSeconds);
    }

    [Test]
    public async Task OneClickGripCompletesAgainstAStationaryVictim()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, victim = default;
        await server.WaitAssertion(() =>
        {
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            victim = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 1));

            Assert.That(entities.System<PullingSystem>().TryStartPull(joe, victim), "pull started");
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), Is.False,
                "the grip telegraphs first, it does not land on the click");
        });

        await pair.RunTicksSync(Ticks(timing, 1));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim),
                "one click went straight to an aggressive grab after the windup");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AVictimThatBreaksThePullDuringTheWindupIsNotGrabbed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, victim = default;
        await server.WaitAssertion(() =>
        {
            var pulling = entities.System<PullingSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            victim = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 1));

            Assert.That(pulling.TryStartPull(joe, victim), "pull started");
            // moving breaks a passive pull; this is what the victim's movement key does
            Assert.That(pulling.TryStopPull(victim, entities.GetComponent<PullableComponent>(victim), victim),
                "the victim walked out of the pull");
        });

        await pair.RunTicksSync(Ticks(timing, 1));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), Is.False,
                "the windup was cancelled");
            Assert.That(entities.System<PullingSystem>().IsPulling(joe), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OtherGrabbersStillNeedTheSecondClick()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid grabber = default, victim = default;
        await server.WaitAssertion(() =>
        {
            // a colony Working Joe that is not the antag
            grabber = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            victim = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 1));
            Assert.That(entities.System<PullingSystem>().TryStartPull(grabber, victim), "pull started");

            // a click before the upgrade delay is refused (with the new "not ready" popup)
            var early = new RMCPullToggleEvent();
            entities.EventBus.RaiseLocalEvent(grabber, ref early);
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), Is.False,
                "an early second click does not upgrade the grab");
        });

        await pair.RunTicksSync(Ticks(timing, 2.5));

        await server.WaitAssertion(() =>
        {
            var fireman = entities.System<FiremanCarrySystem>();
            Assert.That(fireman.IsAggressivelyGrabbed(victim), Is.False, "no Machine Grip without the antag");

            var toggle = new RMCPullToggleEvent();
            entities.EventBus.RaiseLocalEvent(grabber, ref toggle);
            Assert.That(fireman.IsAggressivelyGrabbed(victim), "the second click upgrades the grab");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EscapeMultiplierScalesTheStruggleAndHoldsRestartIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, victim = default;
        TimeSpan firstStart = default;
        await server.WaitAssertion(() =>
        {
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            entities.System<EWJoeRuleSystem>().ApplyLevel((joe, comp), 2);

            Assert.Multiple(() =>
            {
                // review fix: the manual upgrade unlocks exactly when the telegraph would land, never sooner
                var carry = entities.GetComponent<CanFiremanCarryComponent>(joe);
                var skill = entities.System<Content.Shared._RMC14.Marines.Skills.SkillsSystem>().GetSkillDelayMultiplier(joe, carry.Skill);
                Assert.That(carry.AggressiveGrabDelay * skill, Is.EqualTo(comp.GripTelegraph),
                    "a second click cannot beat the 0.4 s telegraph");
                Assert.That(entities.GetComponent<CMUGrabEscapeMultiplierComponent>(joe).Multiplier, Is.EqualTo(1.5f),
                    "x1.5 at level 2");
            });

            victim = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 1));
            Assert.That(entities.System<PullingSystem>().TryStartPull(joe, victim), "pull started");
        });

        await pair.RunTicksSync(Ticks(timing, 1));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), "gripped");

            // a mindless test mob has no InputMoverComponent; a player's body gets one on attach (SharedMindSystem)
            var input = entities.EnsureComponent<InputMoverComponent>(victim);
            input.HeldMoveButtons = MoveButtons.Right;
            var move = new MoveInputEvent((victim, input), MoveButtons.None);
            entities.EventBus.RaiseLocalEvent(victim, ref move);

            var carriable = entities.GetComponent<FiremanCarriableComponent>(victim);
            Assert.That(carriable.BreakingFree, "the victim started to struggle");
            var struggle = Struggle(entities, victim);
            Assert.That(struggle.Args.Delay, Is.EqualTo(carriable.Delay * 1.5f), "the struggle takes x1.5 at level 2");
            firstStart = struggle.StartTime;
        });

        await pair.RunTicksSync(Ticks(timing, 1));

        await server.WaitAssertion(() =>
        {
            entities.System<EWJoeMachineGripSystem>().ExtendStruggle(victim);

            var carriable = entities.GetComponent<FiremanCarriableComponent>(victim);
            Assert.That(carriable.BreakingFree, "still struggling after the restart");
            var struggle = Struggle(entities, victim);
            Assert.That(struggle.StartTime, Is.GreaterThan(firstStart), "the struggle started over");
            Assert.That(struggle.Args.Delay, Is.EqualTo(carriable.Delay * 1.5f), "at full length");

            entities.GetComponent<InputMoverComponent>(victim).HeldMoveButtons = MoveButtons.None;
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SmallXenoGrabbedFromLevelTwoWithoutParalysisHeavyRefused()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, drone = default;
        await server.WaitAssertion(() =>
        {
            var fireman = entities.System<FiremanCarrySystem>();
            var rule = entities.System<EWJoeRuleSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            drone = entities.SpawnEntity(Drone, EWJoeTestHelpers.TileEast(entities, map, 1));
            var ravager = entities.SpawnEntity(Ravager, EWJoeTestHelpers.TileEast(entities, map, 3));
            var crusher = entities.SpawnEntity(Crusher, EWJoeTestHelpers.TileEast(entities, map, 5));
            var queen = entities.SpawnEntity(Queen, EWJoeTestHelpers.TileEast(entities, map, 8));
            var abomination = entities.SpawnEntity("CMUXenoAbomination", EWJoeTestHelpers.TileEast(entities, map, 11));

            rule.ApplyLevel((joe, comp), 0);
            Assert.That(fireman.CanAggressiveGrab(joe, drone), Is.False, "no xeno grab below level 2");

            rule.ApplyLevel((joe, comp), 2);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<CMUXenoGrabberComponent>(joe), "the xeno grip is granted at level 2");
                Assert.That(fireman.CanAggressiveGrab(joe, drone), "a drone is a small caste");
                Assert.That(fireman.CanAggressiveGrab(joe, ravager), "a ravager is a T3, not a heavy");
                Assert.That(fireman.CanAggressiveGrab(joe, crusher), Is.False, "a crusher is a heavy");
                Assert.That(fireman.CanAggressiveGrab(joe, queen), Is.False, "the queen is a heavy");
                Assert.That(fireman.CanAggressiveGrab(joe, abomination), Is.False, "the abomination is a boss, tier 1 or not");
            });

            Assert.That(entities.System<PullingSystem>().TryStartPull(joe, drone), "pulling a live drone is not refused");
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<StunnedComponent>(joe), Is.False, "the pull did not paralyze the Joe");
                Assert.That(entities.HasComponent<KnockedDownComponent>(joe), Is.False);
            });
        });

        await pair.RunTicksSync(Ticks(timing, 1));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(drone), "the drone was gripped");
        });

        // the hold cap (3 s from the grab) lets it go, then the cooldown blocks a new xeno grab
        await pair.RunTicksSync(Ticks(timing, 3));

        await server.WaitAssertion(() =>
        {
            var fireman = entities.System<FiremanCarrySystem>();
            Assert.Multiple(() =>
            {
                Assert.That(fireman.IsAggressivelyGrabbed(drone), Is.False, "the hold cap released the drone");
                Assert.That(entities.System<PullingSystem>().IsPulling(joe), Is.False);
                Assert.That(fireman.CanAggressiveGrab(joe, drone), Is.False, "the xeno grab is on cooldown");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LargeT3GrabbedWithoutParalysisAfterALongerWindup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, praetorian = default;
        TimeSpan baseWindup = default, largeWindup = default;
        await server.WaitAssertion(() =>
        {
            var rule = entities.System<EWJoeRuleSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            praetorian = entities.SpawnEntity(Praetorian, EWJoeTestHelpers.TileEast(entities, map, 1));
            rule.ApplyLevel((joe, comp), 2);

            var grip = entities.System<EWJoeMachineGripSystem>();
            baseWindup = comp.GripTelegraph;
            largeWindup = grip.GripTelegraphFor((joe, comp), praetorian);
            Assert.That(largeWindup, Is.GreaterThan(baseWindup), "a T3 takes a longer windup");
            Assert.That(entities.System<FiremanCarrySystem>().CanAggressiveGrab(joe, praetorian), "a praetorian can be grabbed");

            Assert.That(entities.System<PullingSystem>().TryStartPull(joe, praetorian), "pulling a live praetorian is not refused");
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<StunnedComponent>(joe), Is.False, "the pull did not paralyze the Joe");
                Assert.That(entities.HasComponent<KnockedDownComponent>(joe), Is.False);
            });
        });

        // past the ordinary windup, short of the large one: not gripped yet, and the manual upgrade is not ready either
        await pair.RunTicksSync(Ticks(timing, (baseWindup.TotalSeconds + largeWindup.TotalSeconds) / 2));

        await server.WaitAssertion(() =>
        {
            var fireman = entities.System<FiremanCarrySystem>();
            Assert.That(fireman.IsAggressivelyGrabbed(praetorian), Is.False, "still winding up on a T3");
            var carry = entities.GetComponent<CanFiremanCarryComponent>(joe);
            Assert.That(carry.PullTime + carry.AggressiveGrabDelay * entities.System<Content.Shared._RMC14.Marines.Skills.SkillsSystem>()
                    .GetSkillDelayMultiplier(joe, carry.Skill), Is.GreaterThan(timing.CurTime),
                "a second Ctrl-click cannot beat the longer windup");
        });

        await pair.RunTicksSync(Ticks(timing, largeWindup.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(praetorian), "the praetorian was gripped");
        });

        await pair.CleanReturnAsync();
    }

    private static Content.Shared.DoAfter.DoAfter Struggle(IEntityManager entities, EntityUid victim)
    {
        return entities.GetComponent<DoAfterComponent>(victim).DoAfters.Values
            .Single(d => !d.Cancelled && !d.Completed && d.Args.Event is BreakFiremanCarryDoAfterEvent);
    }
}
