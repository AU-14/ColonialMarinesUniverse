using System.Reflection;
#pragma warning disable RA0002 // Integration regression intentionally seeds completed DoAfter state.

using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Verbs;
using Content.Shared._RMC14.CCVar;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Execution;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(SharedExecutionSystem))]
public sealed class ExecutionMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExecutionPolicyWeapon
  name: execution policy weapon
  components:
  - type: Item
  - type: MeleeWeapon
    damage:
      types:
        Blunt: 10
  - type: Execution
    doAfterDuration: 0
  - type: ExecutionMergeProbe
";

    [Test]
    [EnsureCVar(Side.Server, typeof(RMCCVars), nameof(RMCCVars.RMCEnableSuicide), false)]
    public Task DisabledSuicideCVarDoesNotEnableExecution()
    {
        return AssertExecutionDisabled(false);
    }

    [Test]
    [EnsureCVar(Side.Server, typeof(RMCCVars), nameof(RMCCVars.RMCEnableSuicide), true)]
    public Task EnabledSuicideCVarStillDoesNotEnableExecution()
    {
        return AssertExecutionDisabled(true);
    }

    private async Task AssertExecutionDisabled(bool expectedSuicideCVar)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<ExecutionMergeProbeSystem>();
            var execution = Server.System<SharedExecutionSystem>();
            var actionBlocker = Server.System<ActionBlockerSystem>();
            var damageable = Server.System<DamageableSystem>();
            var hands = Server.System<SharedHandsSystem>();
            var mobState = Server.System<MobStateSystem>();
            var verbs = Server.System<VerbSystem>();
            var localization = Server.ResolveDependency<ILocalizationManager>();

            var attacker = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var weapon = SEntMan.SpawnEntity("ExecutionPolicyWeapon", map.GridCoords);

            try
            {
                Assert.That(hands.TryPickup(attacker, weapon), Is.True);
                mobState.ChangeMobState(victim, MobState.Critical);

                Assert.Multiple(() =>
                {
                    Assert.That(Server.CfgMan.GetCVar(RMCCVars.RMCEnableSuicide),
                        Is.EqualTo(expectedSuicideCVar));
                    Assert.That(SEntMan.HasComponent<DamageableComponent>(victim), Is.True);
                    Assert.That(mobState.IsCritical(victim), Is.True);
                    Assert.That(actionBlocker.CanInteract(victim, null), Is.False,
                        "the victim satisfies the upstream incapacitation prerequisite");
                    Assert.That(actionBlocker.CanAttack(attacker, victim), Is.True,
                        "the attacker satisfies the upstream attack prerequisite");
                    Assert.That(execution.CanBeExecuted(victim, attacker), Is.False,
                        "the execution policy must stay disabled independently of the suicide CVar");
                });

                var executionText = localization.GetString("execution-verb-name");
                var localVerbs = verbs.GetLocalVerbs(victim, attacker, typeof(UtilityVerb));
                Assert.That(localVerbs.Any(verb => verb.Text == executionText), Is.False,
                    "an otherwise eligible incapacitated victim must expose no execution utility verb");

                var beforeDoAfters = ActiveDoAfterCount(attacker);
                var component = SEntMan.GetComponent<ExecutionComponent>(weapon);
                typeof(SharedExecutionSystem)
                    .GetMethod("TryStartExecutionDoAfter", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(execution, new object[] { weapon, victim, attacker, component });
                Assert.That(ActiveDoAfterCount(attacker), Is.EqualTo(beforeDoAfters),
                    "the policy gate must return before starting an execution do-after");

                var damageBefore = damageable.GetTotalDamage(victim);
                var completion = Completion(attacker, victim, weapon);
            SEntMan.EventBus.RaiseLocalEvent(weapon, completion);
                var probe = SEntMan.GetComponent<ExecutionMergeProbeComponent>(weapon);

                Assert.Multiple(() =>
                {
                    Assert.That(completion.Handled, Is.False,
                        "the rejected completion must return before marking an execution handled");
                    Assert.That(component.Executing, Is.False);
                    Assert.That(probe.MeleeHits, Is.Zero,
                        "the rejected completion must not reach the melee attack path");
                    Assert.That(damageable.GetTotalDamage(victim), Is.EqualTo(damageBefore));
                    Assert.That(mobState.IsCritical(victim), Is.True);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(weapon);
                SEntMan.DeleteEntity(victim);
                SEntMan.DeleteEntity(attacker);
            }
        });
    }

    private int ActiveDoAfterCount(EntityUid user)
    {
        if (!SEntMan.TryGetComponent<DoAfterComponent>(user, out var component))
            return 0;

        return component.DoAfters.Values.Count(doAfter => !doAfter.Cancelled && !doAfter.Completed);
    }

    private ExecutionDoAfterEvent Completion(EntityUid user, EntityUid victim, EntityUid weapon)
    {
        var ev = new ExecutionDoAfterEvent();
        var args = new DoAfterArgs(SEntMan, user, TimeSpan.Zero, ev, weapon, victim, weapon);
        ev.DoAfter = new Content.Shared.DoAfter.DoAfter(0, args, TimeSpan.Zero)
        {
            Completed = true,
        };
        return ev;
    }
}

[RegisterComponent]
public sealed partial class ExecutionMergeProbeComponent : Component
{
    public int MeleeHits;
}

public sealed class ExecutionMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExecutionMergeProbeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private static void OnMeleeHit(Entity<ExecutionMergeProbeComponent> entity, ref MeleeHitEvent args)
    {
        entity.Comp.MeleeHits++;
    }
}

#pragma warning restore RA0002
