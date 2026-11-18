using Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Tackle;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.Damage;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// ARCH-05 / FLOW-06 / ARCH-04 (node N12): the Liturgy meter. Gains, cap and Exalted, decay after the window,
/// the break on a knockdown, the miss rule, the hit, disarm and machete hooks, the cooldown cut, the CC
/// immunity guard on empowered hotbar moves, and the per-stack buffs (ARCH-12): attack rate and slow duration.
/// </summary>
[TestFixture]
public sealed class EWJoeLiturgyTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Machete = "CMUEWJoeMachete";
    private static readonly EntProtoId SweepAction = "CMUActionEWJoeSweep";

    /// <summary>A colony Working Joe made an Early Warning Joe of <paramref name="level"/>, with the meter.</summary>
    public static (EntityUid Joe, EWJoeLiturgyComponent Liturgy) SpawnJoe(IEntityManager entities, TestMapData map, int level)
    {
        var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
        entities.EnsureComponent<EWJoeComponent>(joe).Level = level;
        return (joe, entities.EnsureComponent<EWJoeLiturgyComponent>(joe));
    }

    private static MeleeHitEvent Swing(EntityUid joe, EntityUid weapon, params EntityUid[] hits)
    {
        return new MeleeHitEvent(new List<EntityUid>(hits), joe, weapon, new DamageSpecifier(), null);
    }

    [Test]
    public async Task MeterGainsCapsAndDecays()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EWJoeLiturgyComponent liturgy = default!;
        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            // level 0: no sequence is open, so nothing spends the stacks
            var (joe, comp) = SpawnJoe(entities, map, 0);
            liturgy = comp;
            var victim = entities.SpawnEntity(Human, map.GridCoords);

            system.Register(joe, EWJoeCqcInput.Hit, victim);
            Assert.That(liturgy.Stacks, Is.EqualTo(1), "a hit is one stack");
            system.Register(joe, EWJoeCqcInput.Disarm, victim);
            Assert.That(liturgy.Stacks, Is.EqualTo(2), "a disarm is one stack");
            system.Register(joe, EWJoeCqcInput.Ability, victim);
            Assert.That(liturgy.Stacks, Is.EqualTo(4), "a landed ability is two stacks");
            Assert.That(liturgy.Exalted, Is.False);
            system.Register(joe, EWJoeCqcInput.AggressiveGrab, victim);
            Assert.That(liturgy.Stacks, Is.EqualTo(5));
            Assert.That(liturgy.Exalted, "five stacks is Exalted");
            system.Register(joe, EWJoeCqcInput.Ability, victim);
            Assert.That(liturgy.Stacks, Is.EqualTo(5), "the meter caps at five");
        });

        await pair.RunTicksSync((int) Math.Ceiling(3.6 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(liturgy.Stacks, Is.Zero, "the stacks decay once the window lapses");
            Assert.That(liturgy.Exalted, Is.False, "Exalted is not banked");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KnockdownBreaksTheMeter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default;
        EWJoeLiturgyComponent liturgy = default!;
        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            (joe, liturgy) = SpawnJoe(entities, map, 0);
            var victim = entities.SpawnEntity(Human, map.GridCoords);

            system.Register(joe, EWJoeCqcInput.Hit, victim);
            system.Register(joe, EWJoeCqcInput.Hit, victim);
            Assert.That(liturgy.Stacks, Is.EqualTo(2));

            entities.System<SharedStunSystem>().TryKnockdown(joe, TimeSpan.FromSeconds(5), force: true);
            Assert.That(entities.HasComponent<KnockedDownComponent>(joe), "the Joe is down");
        });

        await pair.RunTicksSync((int) Math.Ceiling(0.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(liturgy.Stacks, Is.Zero, "a knocked-down Joe loses the meter");
            Assert.That(liturgy.Buffer, Is.Empty, "and the sequence");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HooksRegisterAndAMissKeepsTheStacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var (joe, liturgy) = SpawnJoe(entities, map, 0);
            var victim = entities.SpawnEntity(Human, map.GridCoords);

            // a bare-handed hit is raised on the Joe as its own weapon
            var hit = Swing(joe, joe, victim);
            entities.EventBus.RaiseLocalEvent(joe, hit);
            Assert.That(liturgy.Stacks, Is.EqualTo(1), "the unarmed hit hook");
            Assert.That(liturgy.Buffer, Has.Count.EqualTo(1));

            var miss = Swing(joe, joe);
            entities.EventBus.RaiseLocalEvent(joe, miss);
            Assert.That(liturgy.Buffer, Is.Empty, "a miss breaks the sequence");
            Assert.That(liturgy.Stacks, Is.EqualTo(1), "but keeps the stacks");

            // (MobStateComponent, CMDisarmEvent): raised on the target
            var disarm = new CMDisarmEvent(joe);
            entities.EventBus.RaiseLocalEvent(victim, ref disarm);
            Assert.That(liturgy.Stacks, Is.EqualTo(2), "the disarm hook");

            // machete hits are raised on the machete and forwarded by the arsenal
            var machete = entities.SpawnEntity(null, map.GridCoords);
            entities.EnsureComponent<EWJoeMacheteComponent>(machete).Joe = joe;
            var cut = Swing(joe, machete, victim);
            entities.EventBus.RaiseLocalEvent(machete, cut);
            Assert.That(liturgy.Stacks, Is.EqualTo(3), "the machete forward");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HitTrimsCqcCooldowns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var actions = entities.System<SharedActionsSystem>();
            var (joe, liturgy) = SpawnJoe(entities, map, 0);
            var victim = entities.SpawnEntity(Human, map.GridCoords);

            EntityUid? action = null;
            Assert.That(actions.AddAction(joe, ref action, SweepAction), "the Sweep action was added");
            actions.SetCooldown(action, TimeSpan.FromSeconds(10));
            var before = entities.GetComponent<ActionComponent>(action!.Value).Cooldown!.Value.End;

            entities.EventBus.RaiseLocalEvent(joe, Swing(joe, joe, victim));

            var after = entities.GetComponent<ActionComponent>(action.Value).Cooldown!.Value.End;
            Assert.That(after, Is.EqualTo(before - liturgy.CooldownCutPerHit), "a hit takes half a second off");
            Assert.That(after, Is.GreaterThan(timing.CurTime));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExaltedIsSpentOnceAndNotOnAnImmuneTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            // level 0: no sequence is open, so the stacks only build
            var (joe, liturgy) = SpawnJoe(entities, map, 0);
            var immune = entities.SpawnEntity(Human, map.GridCoords);
            var other = entities.SpawnEntity(Human, map.GridCoords);

            system.Register(joe, EWJoeCqcInput.Ability, other);
            system.Register(joe, EWJoeCqcInput.Ability, other);
            system.Register(joe, EWJoeCqcInput.Hit, other);
            Assert.That(liturgy.Exalted, "max stacks is Exalted");

            system.RecordHardCc(joe, immune);
            Assert.That(system.IsCcImmune(joe, immune), "a hard CC opens the window");

            Assert.That(system.TryConsumeEmpowered(joe, immune, ccBonus: true, "Sweep"), Is.False,
                "no empowered CC on a target inside its window");
            Assert.That(liturgy.Exalted, "Exalted is kept for the next move");
            Assert.That(liturgy.Stacks, Is.EqualTo(liturgy.MaxStacks), "and so are the stacks");

            Assert.That(system.TryConsumeEmpowered(joe, other, ccBonus: true, "Sweep"), "empowered on a fresh target");
            Assert.That(liturgy.Exalted, Is.False, "Exalted is spent");
            Assert.That(liturgy.Stacks, Is.EqualTo(liturgy.StacksAfterEmpower), "the stacks drop to StacksAfterEmpower, not zero");
            Assert.That(system.TryConsumeEmpowered(joe, other, ccBonus: true, "Sweep"), Is.False, "and only once");

            // a Joe with fewer stacks than StacksAfterEmpower keeps what it had
            var (low, lowLiturgy) = SpawnJoe(entities, map, 0);
            system.SetExalted(low, true);
            Assert.That(system.TryConsumeEmpowered(low, other, ccBonus: false, "Desecrate"));
            Assert.That(lowLiturgy.Stacks, Is.Zero, "spending Exalted never adds stacks");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StacksSpeedUpFistsAndTheMachete()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            var melee = entities.System<SharedMeleeWeaponSystem>();
            var (joe, liturgy) = SpawnJoe(entities, map, 0);
            var victim = entities.SpawnEntity(Human, map.GridCoords);
            var machete = entities.SpawnEntity(Machete, map.GridCoords);
            entities.GetComponent<EWJoeMacheteComponent>(machete).Joe = joe;

            Assert.That(entities.HasComponent<MeleeWeaponComponent>(joe), "the Joe fights with its fists");
            var fistBase = melee.GetAttackRate(joe, joe);
            var macheteBase = melee.GetAttackRate(machete, joe);
            Assert.That(fistBase, Is.GreaterThan(0f));

            system.Register(joe, EWJoeCqcInput.Ability, victim);
            system.Register(joe, EWJoeCqcInput.Ability, victim);
            system.Register(joe, EWJoeCqcInput.Hit, victim);
            Assert.That(liturgy.Stacks, Is.EqualTo(5));

            var expected = 1f + liturgy.AttackRatePerStack * 5;
            Assert.That(melee.GetAttackRate(joe, joe), Is.EqualTo(fistBase * expected).Within(0.001f),
                "each stack adds AttackRatePerStack to the fist rate");
            Assert.That(melee.GetAttackRate(machete, joe), Is.EqualTo(macheteBase * expected).Within(0.001f),
                "and to the machete's");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StacksShortenIncomingSlows()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeLiturgySystem>();
            var slow = entities.System<RMCSlowSystem>();
            var duration = TimeSpan.FromSeconds(5);

            var (calm, _) = SpawnJoe(entities, map, 0);
            var (raging, liturgy) = SpawnJoe(entities, map, 0);
            var victim = entities.SpawnEntity(Human, map.GridCoords);
            system.Register(raging, EWJoeCqcInput.Ability, victim);
            system.Register(raging, EWJoeCqcInput.Ability, victim);
            system.Register(raging, EWJoeCqcInput.Hit, victim);
            Assert.That(liturgy.Stacks, Is.EqualTo(liturgy.MaxStacks));

            Assert.That(slow.TrySlowdown(calm, duration, ignoreDurationModifier: true, ignoreImmunity: true));
            Assert.That(slow.TrySlowdown(raging, duration, ignoreDurationModifier: true, ignoreImmunity: true));
            Assert.That(slow.TryRoot(calm, duration));
            Assert.That(slow.TryRoot(raging, duration));

            var calmSlow = entities.GetComponent<RMCSlowdownComponent>(calm).ExpiresAt - timing.CurTime;
            var ragingSlow = entities.GetComponent<RMCSlowdownComponent>(raging).ExpiresAt - timing.CurTime;
            var calmRoot = entities.GetComponent<RMCRootedComponent>(calm).ExpiresAt - timing.CurTime;
            var ragingRoot = entities.GetComponent<RMCRootedComponent>(raging).ExpiresAt - timing.CurTime;

            var expected = EWJoeLiturgyBuffsSystem.SlowDurationMultiplier(liturgy);
            Assert.That(expected, Is.LessThan(1f), "max stacks cut the slow");
            Assert.That(calmSlow.TotalSeconds, Is.EqualTo(duration.TotalSeconds).Within(0.01), "no stacks, the full slow");
            Assert.That(ragingSlow.TotalSeconds, Is.EqualTo(duration.TotalSeconds * expected).Within(0.01),
                "max stacks shorten the slow");
            Assert.That(ragingRoot.TotalSeconds, Is.EqualTo(calmRoot.TotalSeconds * expected).Within(0.01),
                "and the root");
        });

        await pair.CleanReturnAsync();
    }
}
