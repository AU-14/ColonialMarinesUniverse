using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Stun;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// ARCH-06 (node N13): CQC sequences. The starter table loads, a sequence fires only on the exact order, on one
/// target and in time, levels gate it, it spends no stacks (ARCH-12), the CC-immunity window blocks a second
/// sequence Sweep, a sequence lands the same effect as the hotbar, an Exalted Joe's sequence spends the empower,
/// and Unmaking's wide swing needs a blade. Inputs are registered directly.
/// </summary>
[TestFixture]
public sealed class EWJoeCqcSequenceTest
{
    private const string Human = "CMMobHuman";
    private const string Machete = "CMUEWJoeMachete";

    private static readonly string[] Starters = ["Rebuke", "Seize", "Profane", "Penance", "Silence", "Unmaking"];

    [Test]
    public async Task StarterSequencesAreWellFormed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            var effects = server.EntMan.System<EWJoeCqcEffectsSystem>();
            Assert.Multiple(() =>
            {
                foreach (var id in Starters)
                {
                    Assert.That(protos.TryIndex<EWJoeCqcSequencePrototype>(id, out var sequence), $"{id} exists");
                    if (sequence == null)
                        continue;

                    Assert.That(sequence.Steps, Has.Count.InRange(1, 4), $"{id} fits the buffer");
                    Assert.That(effects.Has(sequence.Effect), $"{id}'s effect {sequence.Effect} is registered");
                    Assert.That(loc.HasString(sequence.Name), $"{id} has a name");
                    if (sequence.Steps.Contains(EWJoeCqcInput.Wide))
                        Assert.That(sequence.RequiresBlade, $"{id} has a wide swing, so it needs a blade");
                }

                // ARCH-12: free sequences would hand out the empower for nothing; max stacks does that now
                Assert.That(protos.EnumeratePrototypes<EWJoeCqcSequencePrototype>().Any(s => s.ID == "Exaltation"), Is.False, "Exaltation is retired");
                Assert.That(effects.Has("Empower"), Is.False, "and so is its effect");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RebukeFiresOnlyOnTheExactOrderOnOneTargetInTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid slowJoe = default, slowVictim = default;
        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();

            // out of order
            var (joe, _) = EWJoeLiturgyTest.SpawnJoe(entities, map, 1);
            var victim = entities.SpawnEntity(Human, map.GridCoords);
            system.Register(joe, EWJoeCqcInput.Disarm, victim);
            system.Register(joe, EWJoeCqcInput.Hit, victim);
            system.Register(joe, EWJoeCqcInput.Hit, victim);
            Assert.That(entities.HasComponent<KnockedDownComponent>(victim), Is.False, "Disarm, Hit, Hit is not Rebuke");

            // the last step on another body
            var (joe2, _) = EWJoeLiturgyTest.SpawnJoe(entities, map, 1);
            var first = entities.SpawnEntity(Human, map.GridCoords);
            var second = entities.SpawnEntity(Human, map.GridCoords);
            system.Register(joe2, EWJoeCqcInput.Hit, first);
            system.Register(joe2, EWJoeCqcInput.Hit, first);
            system.Register(joe2, EWJoeCqcInput.Disarm, second);
            Assert.That(entities.HasComponent<KnockedDownComponent>(second), Is.False, "steps on two bodies are not a sequence");
            Assert.That(entities.HasComponent<KnockedDownComponent>(first), Is.False);

            // the exact order: Sweep, and the three stacks built are kept (ARCH-12: sequences are free)
            var (joe3, liturgy3) = EWJoeLiturgyTest.SpawnJoe(entities, map, 1);
            var target = entities.SpawnEntity(Human, map.GridCoords);
            system.Register(joe3, EWJoeCqcInput.Hit, target);
            system.Register(joe3, EWJoeCqcInput.Hit, target);
            system.Register(joe3, EWJoeCqcInput.Disarm, target);
            Assert.That(entities.HasComponent<KnockedDownComponent>(target), "Rebuke sweeps");
            Assert.That(liturgy3.Stacks, Is.EqualTo(3), "Rebuke spends no stacks");
            Assert.That(liturgy3.Buffer, Is.Empty, "a fired sequence clears the buffer");

            // too slow: the last step comes after the gap
            (slowJoe, _) = EWJoeLiturgyTest.SpawnJoe(entities, map, 1);
            slowVictim = entities.SpawnEntity(Human, map.GridCoords);
            system.Register(slowJoe, EWJoeCqcInput.Hit, slowVictim);
            system.Register(slowJoe, EWJoeCqcInput.Hit, slowVictim);
        });

        await pair.RunTicksSync((int) Math.Ceiling(1.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            entities.System<EWJoeLiturgySystem>().Register(slowJoe, EWJoeCqcInput.Disarm, slowVictim);
            Assert.That(entities.HasComponent<KnockedDownComponent>(slowVictim), Is.False, "a step past the max gap starts over");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MinLevelGatesAndCcImmunityBlocksASecondSweep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();

            var (low, lowLiturgy) = EWJoeLiturgyTest.SpawnJoe(entities, map, 0);
            var spared = entities.SpawnEntity(Human, map.GridCoords);
            system.Register(low, EWJoeCqcInput.Hit, spared);
            system.Register(low, EWJoeCqcInput.Hit, spared);
            system.Register(low, EWJoeCqcInput.Disarm, spared);
            Assert.That(entities.HasComponent<KnockedDownComponent>(spared), Is.False, "Rebuke is closed at level 0");
            Assert.That(lowLiturgy.Stacks, Is.EqualTo(3), "nothing spent");

            var (joe, liturgy) = EWJoeLiturgyTest.SpawnJoe(entities, map, 1);
            var victim = entities.SpawnEntity(Human, map.GridCoords);
            system.Register(joe, EWJoeCqcInput.Hit, victim);
            system.Register(joe, EWJoeCqcInput.Hit, victim);
            system.Register(joe, EWJoeCqcInput.Disarm, victim);
            Assert.That(liturgy.Buffer, Is.Empty, "the first Rebuke fires");
            Assert.That(liturgy.Stacks, Is.EqualTo(3), "and spends nothing");
            Assert.That(system.IsCcImmune(joe, victim), "and opens the CC-immunity window");

            // the same sequence within 6 s: the steps stay in the buffer and the Exalted reached is kept
            system.Register(joe, EWJoeCqcInput.Hit, victim);
            system.Register(joe, EWJoeCqcInput.Hit, victim);
            system.Register(joe, EWJoeCqcInput.Disarm, victim);
            Assert.That(liturgy.Buffer, Has.Count.EqualTo(3), "the second Rebuke is refused inside the window");
            Assert.That(liturgy.Exalted, "a refused sequence does not spend the empower");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProfaneLandsTheSameDesecrateAsTheHotbar()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            var combat = entities.System<EWJoeCombatSystem>();

            var (joe, liturgy) = EWJoeLiturgyTest.SpawnJoe(entities, map, 2);
            entities.EnsureComponent<EWJoeDesecrateComponent>(joe);
            var bySequence = entities.SpawnEntity(Human, map.GridCoords);
            var byHotbar = entities.SpawnEntity(Human, map.GridCoords);
            var other = entities.SpawnEntity(Human, map.GridCoords);

            // two stacks banked on another body, then Grab, Hit, Hit (three more)
            system.Register(joe, EWJoeCqcInput.Ability, other);
            system.Register(joe, EWJoeCqcInput.Grab, bySequence);
            system.Register(joe, EWJoeCqcInput.Hit, bySequence);
            system.Register(joe, EWJoeCqcInput.Hit, bySequence);
            Assert.That(liturgy.Stacks, Is.EqualTo(4), "Profane spends nothing");

            combat.Desecrate((joe, entities.GetComponent<EWJoeComponent>(joe)), byHotbar);

            var sequenceDamage = entities.System<DamageableSystem>().GetTotalDamage(bySequence);
            var hotbarDamage = entities.System<DamageableSystem>().GetTotalDamage(byHotbar);
            Assert.That(sequenceDamage, Is.GreaterThan(FixedPoint2.Zero), "Profane desecrated");
            Assert.That(sequenceDamage, Is.EqualTo(hotbarDamage), "the sequence and the hotbar share one Desecrate");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnmakingNeedsABlade()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();

            // Hit, Wide, Hit with fists: no Unmaking (fists cannot wide-swing)
            var (fists, fistsLiturgy) = EWJoeLiturgyTest.SpawnJoe(entities, map, 5);
            var spared = entities.SpawnEntity(Human, map.GridCoords);
            system.Register(fists, EWJoeCqcInput.Hit, spared);
            system.Register(fists, EWJoeCqcInput.Wide, spared);
            system.Register(fists, EWJoeCqcInput.Hit, spared);
            Assert.That(entities.HasComponent<KnockedDownComponent>(spared), Is.False, "a fist Wide step does not count");
            Assert.That(fistsLiturgy.Buffer, Has.Count.EqualTo(3), "nothing fired");

            // the same steps with the blade: Unmaking dazes and floors the target, and spends nothing
            var (joe5, liturgy5) = EWJoeLiturgyTest.SpawnJoe(entities, map, 5);
            var target = entities.SpawnEntity(Human, map.GridCoords);
            system.Register(joe5, EWJoeCqcInput.Hit, target, fromBlade: true);
            system.Register(joe5, EWJoeCqcInput.Wide, target, fromBlade: true);
            system.Register(joe5, EWJoeCqcInput.Hit, target, fromBlade: true);
            Assert.That(entities.HasComponent<KnockedDownComponent>(target), "Unmaking floors the target");
            Assert.That(entities.System<NewStatusEffectsSystem>().HasStatusEffect(target, RMCDazedSystem.StatusEffectDazed),
                "and dazes it");
            Assert.That(system.IsCcImmune(joe5, target), "Unmaking is hard CC");
            Assert.That(liturgy5.Stacks, Is.EqualTo(3), "Unmaking spends no stacks");

            // the blade rule itself: fists are not a blade, the machete is
            var machete = entities.SpawnEntity(Machete, map.GridCoords);
            Assert.That(system.IsBlade(joe5, joe5), Is.False, "fists are not a blade");
            Assert.That(system.IsBlade(joe5, machete), "the machete is a blade");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnExaltedSequenceSpendsTheEmpowerOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            var (joe, liturgy) = EWJoeLiturgyTest.SpawnJoe(entities, map, 1);
            var other = entities.SpawnEntity(Human, map.GridCoords);
            var victim = entities.SpawnEntity(Human, map.GridCoords);

            // four stacks on another body, then Hit (five: Exalted), Hit, Disarm: an empowered Rebuke
            system.Register(joe, EWJoeCqcInput.Ability, other);
            system.Register(joe, EWJoeCqcInput.Ability, other);
            system.Register(joe, EWJoeCqcInput.Hit, victim);
            Assert.That(liturgy.Exalted, "max stacks is Exalted");
            system.Register(joe, EWJoeCqcInput.Hit, victim);
            system.Register(joe, EWJoeCqcInput.Disarm, victim);

            Assert.That(entities.HasComponent<KnockedDownComponent>(victim), "Rebuke sweeps");
            Assert.That(liturgy.Exalted, Is.False, "the sequence spent the empower");
            Assert.That(liturgy.Stacks, Is.EqualTo(liturgy.StacksAfterEmpower + EWJoeLiturgySystem.GainFor(liturgy, EWJoeCqcInput.Disarm)),
                "the stacks drop, not to zero, and the closing input still counts afterwards");

            // the input that fills the meter does not empower its own sequence: max stacks empower the NEXT move
            var second = entities.SpawnEntity(Human, map.GridCoords);
            system.Break((joe, liturgy));
            system.Register(joe, EWJoeCqcInput.Ability, other); // two stacks: Hit, Hit brings four, Disarm the fifth
            system.Register(joe, EWJoeCqcInput.Hit, second);
            system.Register(joe, EWJoeCqcInput.Hit, second);
            Assert.That(liturgy.Exalted, Is.False, "one short of max before the closing input");
            system.Register(joe, EWJoeCqcInput.Disarm, second);
            Assert.That(entities.HasComponent<KnockedDownComponent>(second), "Rebuke still sweeps");
            Assert.That(liturgy.Exalted, "the closing input filled the meter and its Exalted waits for the next move");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SeizeSkipsTheGripWindup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            var (joe, liturgy) = EWJoeLiturgyTest.SpawnJoe(entities, map, 2);
            var other = entities.SpawnEntity(Human, map.GridCoords);
            var victim = entities.SpawnEntity(Human, map.GridCoords);

            system.Register(joe, EWJoeCqcInput.Ability, other);
            system.Register(joe, EWJoeCqcInput.Disarm, victim);

            // the real pull raises the Grab step through the PullStartedMessage hook
            Assert.That(entities.System<PullingSystem>().TryStartPull(joe, victim), "pull started");
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), "Seize grips at once");
            Assert.That(liturgy.Stacks, Is.EqualTo(4), "nothing spent, the grip's own stack gained");
        });

        await pair.CleanReturnAsync();
    }
}
