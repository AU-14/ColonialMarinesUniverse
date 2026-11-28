using Content.IntegrationTests._CMU14.Round.Antags;
using Content.Server.CMU14.Combat.CQC;
using Content.Server.CMU14.Combat.Drown;
using Content.Shared._RMC14.Fireman;
using Content.Shared.CMU14.Combat.CQC;
using Content.Shared.CMU14.Combat.Drown;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Combat;

/// <summary>
/// The generic CQC system and the shared takedowns work on a body that is not an Early Warning Joe: a body whose
/// CMUCqc lists Seize completes Disarm, Grab (the Grab from the real pull hook) and the generic Grip effect grabs, a
/// body that does not list it only pulls, and a body with CMUDrown drowns an organic it holds.
/// </summary>
[TestFixture]
public sealed class CMUCqcSystemTest
{
    // a colony Working Joe that is not the antag: it can pull and grab, and it has no EWJoe component
    private const string Body = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private static readonly ProtoId<CMUCqcSequencePrototype> Seize = "Seize";

    [Test]
    public async Task ANonEarlyWarningBodyCompletesSeizeAndTheGripLands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var fireman = entities.System<FiremanCarrySystem>();
            var pulling = entities.System<PullingSystem>();
            var system = entities.System<CMUCqcSystem>();
            Assert.That(system.HasEffect("Grip"), "Grip is a generic effect");

            var fighter = entities.SpawnEntity(Body, map.GridCoords);
            Assert.That(entities.HasComponent<EWJoeComponent>(fighter), Is.False, "the body is not an Early Warning Joe");
            var cqc = entities.EnsureComponent<CMUCqcComponent>(fighter);
            cqc.Sequences = new List<ProtoId<CMUCqcSequencePrototype>> { Seize };
            var victim = entities.SpawnEntity(Human, map.GridCoords);

            // the Disarm step is registered directly (a raised disarm also runs RMC's tackle), then the real pull raises
            // the Grab step through the (MobStateComponent, PullStartedMessage) hook
            Assert.That(system.Register(fighter, CMUCqcInput.Disarm, victim), "a CQC body records the input");
            Assert.That(cqc.Buffer, Has.Count.EqualTo(1), "the disarm is buffered");
            Assert.That(fireman.IsAggressivelyGrabbed(victim), Is.False, "no grab before the sequence completes");

            Assert.That(pulling.TryStartPull(fighter, victim), "pull started");
            Assert.That(fireman.IsAggressivelyGrabbed(victim), "Seize's generic Grip goes straight to an aggressive grab");
            Assert.That(cqc.NextSequence, Is.GreaterThan(timing.CurTime), "the sequence fired and started the shared cooldown");

            // the same inputs from a body that does not list Seize are only a pull
            var plain = entities.SpawnEntity(Body, map.GridCoords);
            entities.EnsureComponent<CMUCqcComponent>(plain);
            var other = entities.SpawnEntity(Human, map.GridCoords);
            system.Register(plain, CMUCqcInput.Disarm, other);
            Assert.That(pulling.TryStartPull(plain, other), "pull started");
            Assert.That(entities.GetComponent<CMUCqcComponent>(plain).Buffer, Has.Count.EqualTo(2), "both steps are buffered");
            Assert.That(fireman.IsAggressivelyGrabbed(other), Is.False, "a sequence the body does not know never fires");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ANonEarlyWarningBodyDrownsAHeldOrganic()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid drowner = default, victim = default;
        await server.WaitAssertion(() =>
        {
            drowner = entities.SpawnEntity(Body, map.GridCoords);
            entities.EnsureComponent<CMUDrownComponent>(drowner);
            victim = entities.SpawnEntity(Human, map.GridCoords);
            Assert.That(entities.HasComponent<EWJoeComponent>(drowner), Is.False, "the body is not an Early Warning Joe");
            Assert.That(entities.System<CMUDrownSystem>().CanDrown(drowner, victim, out var reason), Is.False, "no grab yet");
            Assert.That(reason, Is.EqualTo("cmu-ew-joe-needs-grab"));
        });

        Assert.That(await EWJoeTestHelpers.AggressiveGrab(pair, drowner, victim), "aggressive grab established");

        await server.WaitAssertion(() =>
        {
            var drown = entities.System<CMUDrownSystem>();
            Assert.That(drown.CanDrown(drowner, victim, out _), "a held organic can be drowned");

            // the Drown action starts the hold on a body without EWJoe
            var action = new EWJoeDrownActionEvent { Performer = drowner, Target = victim };
            entities.EventBus.RaiseLocalEvent(drowner, action);
            Assert.That(action.Handled, "the Drown action is taken");
            Assert.That(entities.HasComponent<CMUDrowningComponent>(victim), "the hold started");

            // the hold's end, without waiting out the do-after
            drown.Drown((drowner, entities.GetComponent<CMUDrownComponent>(drowner)), victim);
            Assert.Multiple(() =>
            {
                Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(victim, "StatusEffectForcedSleeping"),
                    "the victim is forced asleep");
                Assert.That(entities.HasComponent<IsDeadICComponent>(victim), "the victim counts as dead for objectives");
                Assert.That(entities.HasComponent<CMUDrownedComponent>(victim), "and is marked drowned until it wakes");
            });
        });

        await pair.CleanReturnAsync();
    }
}
