#nullable enable
#pragma warning disable RA0002 // Regression tests arrange authoritative and predicted component state.

using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.Client.Actions;
using Content.IntegrationTests.Fixtures;
using Content.Server.Movement.Components;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Movement;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Timing;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class CmuActionPredictionRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestHeavyAttacker
          components:
          - type: CombatMode
            isInCombatMode: true
            toggleMouseRotator: false
          - type: MeleeWeapon
            damage:
              types:
                Blunt: 10
            maxTargets: 2
            range: 2

        - type: entity
          id: CMUTestHeavyVictim
          components:
          - type: Damageable
          - type: Injurable
            damageContainer: Inorganic

        - type: entity
          parent: ActionCombatModeToggle
          id: CMUTestQueuedAction
          components:
          - type: Action
            inSimulationOnly: true
            checkCanInteract: false
            checkConsciousness: false
            raiseOnUser: true
          - type: RMCCooldownOnMiss
            missCooldown: 0.7

        - type: entity
          parent: CMUTestQueuedAction
          id: CMUTestQueuedProbeAction
          components:
          - type: Action
            raiseOnAction: true
          - type: InstantAction
            event: !type:CmuQueuedProbeActionEvent
          - type: CmuActionPredictionProbe
        """;

    [Test]
    public async Task HeavyAttackDeduplicatesVictimsBeforeApplyingTargetLimitAndDamage()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var attacker = SEntMan.SpawnEntity("CMUTestHeavyAttacker", map.GridCoords);
            var first = SEntMan.SpawnEntity("CMUTestHeavyVictim", map.GridCoords.Offset(new Vector2(1, 0)));
            var second = SEntMan.SpawnEntity("CMUTestHeavyVictim", map.GridCoords.Offset(new Vector2(1, 0.5f)));
            Server.PlayerMan.SetAttachedEntity(ServerSession!, attacker);
            var request = new HeavyAttackEvent(SEntMan.GetNetEntity(attacker),
                [SEntMan.GetNetEntity(first), SEntMan.GetNetEntity(first), SEntMan.GetNetEntity(second)],
                SEntMan.GetNetCoordinates(map.GridCoords.Offset(new Vector2(2, 0))));

            Invoke(typeof(SharedMeleeWeaponSystem), SEntMan.System<SharedMeleeWeaponSystem>(), "OnHeavyAttack",
                request, new EntitySessionEventArgs(ServerSession!));

            var firstDamage = SEntMan.GetComponent<DamageableComponent>(first).Damage.DamageDict.GetValueOrDefault("Blunt");
            var secondDamage = SEntMan.GetComponent<DamageableComponent>(second).Damage.DamageDict.GetValueOrDefault("Blunt");
            Assert.Multiple(() =>
            {
                Assert.That(firstDamage, Is.GreaterThan(Content.Shared.FixedPoint.FixedPoint2.Zero));
                Assert.That(secondDamage, Is.EqualTo(firstDamage), "Duplicates must not consume the second victim's slot or multiply the first victim's damage.");
            });
        });
    }

    [TestCase("owner", true)]
    [TestCase("other", false)]
    [TestCase("detached", false)]
    [TestCase("disabled", false)]
    public async Task MissCooldownOnlyAcceptsAnAvailableActionOwnedByTheAttachedSender(string sender, bool accepted)
    {
        var map = await Pair.CreateTestMap();
        var action = EntityUid.Invalid;
        ActionCooldown? original = null;
        await Server.WaitAssertion(() =>
        {
            var owner = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var other = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(Vector2.UnitY));
            action = SEntMan.SpawnEntity("CMUTestQueuedAction", map.GridCoords);
            var actionComp = SEntMan.GetComponent<ActionComponent>(action);
            var actions = SEntMan.System<SharedActionsSystem>();
            actions.AddActionDirect(owner, (action, actionComp));
            actions.SetEnabled((action, actionComp), sender != "disabled");
            Server.PlayerMan.SetAttachedEntity(ServerSession!, sender == "detached" ? null : sender == "other" ? other : owner);

            Invoke(typeof(SharedRMCActionsSystem), SEntMan.System<SharedRMCActionsSystem>(), "OnMissedTargetAction",
                new RMCMissedTargetActionEvent(SEntMan.GetNetEntity(action)), new EntitySessionEventArgs(ServerSession!));

            Assert.That(actionComp.Cooldown.HasValue, Is.EqualTo(accepted));
            original = actionComp.Cooldown;
        });

        if (!accepted)
            return;

        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            var firstCooldown = original!.Value;
            var tickTime = CmuPredictionTiming.GetSimulationTime(SGameTiming);
            Assert.That(tickTime, Is.GreaterThan(firstCooldown.Start));
            Assert.That(tickTime, Is.LessThan(firstCooldown.End), "The repeated miss must arrive while the first penalty is still active.");

            Invoke(typeof(SharedRMCActionsSystem), SEntMan.System<SharedRMCActionsSystem>(), "OnMissedTargetAction",
                new RMCMissedTargetActionEvent(SEntMan.GetNetEntity(action)), new EntitySessionEventArgs(ServerSession!));

            var actionComp = SEntMan.GetComponent<ActionComponent>(action);
            Assert.That(actionComp.Cooldown, Is.EqualTo(original), "A later-tick miss request must not extend the pending cooldown.");
        });
    }

    [Test]
    public async Task ActionValidationAndInstantExecutionUseTheSameTickBoundary()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var action = SEntMan.SpawnEntity("CMUTestQueuedAction", map.GridCoords);
            var comp = SEntMan.GetComponent<ActionComponent>(action);
            var actions = SEntMan.System<SharedActionsSystem>();
            actions.AddActionDirect(user, (action, comp));
            var mode = SEntMan.GetComponent<CombatModeComponent>(user);
            var before = mode.IsInCombatMode;
            var simulation = SGameTiming.InSimulation;
            var remainder = SGameTiming.TickRemainder;
            try
            {
                SGameTiming.InSimulation = true;
                var tickTime = SGameTiming.CurTime;
                actions.SetCooldown((action, comp), tickTime, tickTime + SGameTiming.TickPeriod * 0.5);
                SGameTiming.TickRemainder = SGameTiming.TickPeriod * 0.75;
                SGameTiming.InSimulation = false;
                Assert.That(SGameTiming.CurTime, Is.GreaterThan(comp.Cooldown!.Value.End));
                Assert.That(actions.ValidAction((action, comp)), Is.False, "Target selection must not accept a render-frame-only expiry.");
                var request = new RequestPerformActionEvent(SEntMan.GetNetEntity(action), SGameTiming.CurTick);
                Assert.That(Invoke(typeof(SharedActionsSystem), actions, "TryPerformAction", request, user, false, true), Is.False);
                Assert.That(mode.IsInCombatMode, Is.EqualTo(before));

                SGameTiming.InSimulation = true;
                Assert.That(actions.ValidAction((action, comp)), Is.False);
                actions.SetCooldown((action, comp), tickTime, tickTime);
                Assert.That(actions.ValidAction((action, comp)), Is.True, "Actions become available at their end tick.");
                Assert.That(Invoke(typeof(SharedActionsSystem), actions, "TryPerformAction", request, user, false, true), Is.True);
                Assert.That(mode.IsInCombatMode, Is.Not.EqualTo(before), "The accepted instant action must actually execute.");
            }
            finally
            {
                SGameTiming.InSimulation = simulation;
                SGameTiming.TickRemainder = remainder;
            }
        });
    }

    [Test]
    public async Task InHandWieldAndUseDelayRejectRenderOnlyExpiryAndStartOnTickTime()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var item = SEntMan.SpawnEntity("RMCWeaponRifleM54C", map.GridCoords);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, item), Is.True);
            var delays = SEntMan.System<UseDelaySystem>();
            delays.SetLength(item, TimeSpan.FromSeconds(1));
            var delay = SEntMan.GetComponent<UseDelayComponent>(item);
            var entry = delay.Delays[UseDelaySystem.DefaultId];
            var wieldable = SEntMan.GetComponent<WieldableComponent>(item);
            var simulation = SGameTiming.InSimulation;
            var remainder = SGameTiming.TickRemainder;
            try
            {
                SGameTiming.InSimulation = true;
                var tickTime = SGameTiming.CurTime;
                entry.EndTime = tickTime + SGameTiming.TickPeriod * 0.5;
                SGameTiming.TickRemainder = SGameTiming.TickPeriod * 0.75;
                SGameTiming.InSimulation = false;
                Assert.That(delays.IsDelayed((item, delay)), Is.True);
                SEntMan.System<SharedInteractionSystem>().UseInHandInteraction(user, item);
                Assert.That(wieldable.Wielded, Is.False, "A hand UI request cannot wield during a render-only cooldown expiry.");

                entry.EndTime = tickTime - TimeSpan.FromTicks(1);
                Assert.That(SEntMan.System<SharedWieldableSystem>().TryWield((item, wieldable), user), Is.True);
                Assert.That(wieldable.Wielded, Is.True);
                Assert.That(delays.TryResetDelay((item, delay)), Is.True);
                Assert.That(entry.StartTime, Is.EqualTo(tickTime));
                Assert.That(entry.EndTime, Is.EqualTo(tickTime + entry.Length));
                Assert.That(CmuPredictionTiming.GetSimulationTime(SGameTiming), Is.EqualTo(tickTime));
                Assert.That(SGameTiming.InSimulation, Is.False, "Reading tick time must not mutate the timing service.");
            }
            finally
            {
                SGameTiming.InSimulation = simulation;
                SGameTiming.TickRemainder = remainder;
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task InteractionAndDoAfterHonorHistoricalRangeOptIn(bool historical)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("CMUTestHeavyVictim", map.GridCoords.Offset(new Vector2(8, 0)));
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            var history = SEntMan.EnsureComponent<LagCompensationComponent>(target);
            history.Positions.Clear();
            history.Positions.Enqueue((SGameTiming.CurTime, map.GridCoords.Offset(Vector2.UnitX), Angle.Zero));
            SEntMan.System<SharedRMCLagCompensationSystem>().SetLastRealTick(ServerSession!.UserId, SGameTiming.CurTick);
            var interaction = SEntMan.System<SharedInteractionSystem>();
            Assert.That(interaction.InRangeAndAccessible(user, target, 1.5f, lagCompensated: historical), Is.EqualTo(historical));

            var args = new DoAfterArgs(SEntMan, user, 1, new AwaitedDoAfterEvent(), null, target)
            {
                LagCompensated = historical,
                BreakOnMove = false,
                NeedHand = false,
                RequireCanInteract = false,
            };
            var doAfter = new DoAfter(0, args, SGameTiming.CurTime);
            Assert.That(Invoke(typeof(SharedDoAfterSystem), SEntMan.System<SharedDoAfterSystem>(), "ShouldCancel", doAfter), Is.EqualTo(!historical));
        });
    }

    [TestCase("dispatch")]
    [TestCase("cleanup")]
    [TestCase("removed")]
    [TestCase("detached")]
    [TestCase("clientOnly")]
    public async Task QueuedActionDispatchRespectsLifecycleAndClientOnlyBehavior(string scenario)
    {
        var map = await Pair.CreateTestMap();
        NetEntity actionNet = default;
        await Server.WaitPost(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            EntityUid? action = null;
            var prototype = "CMUTestQueuedProbeAction";
            SEntMan.System<SharedActionsSystem>().AddAction(user, ref action, prototype);
            actionNet = SEntMan.GetNetEntity(action!.Value);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var action = CEntMan.GetEntity(actionNet);
            var comp = CEntMan.GetComponent<ActionComponent>(action);
            var user = comp.AttachedEntity!.Value;
            var probe = CEntMan.EnsureComponent<CmuActionPredictionProbeComponent>(action);
            Assert.That(probe.Executions, Is.Zero);
            var actions = CEntMan.System<ActionsSystem>();
            var simulation = CGameTiming.InSimulation;
            try
            {
                CGameTiming.InSimulation = false;
                comp.ClientExclusive = scenario == "clientOnly";
                actions.TriggerAction((action, comp));
                Assert.That(probe.Executions, Is.EqualTo(scenario == "clientOnly" ? 1 : 0));
                if (scenario == "cleanup")
                    CEntMan.EventBus.RaiseEvent(EventSource.Network, new RoundRestartCleanupEvent());
                else if (scenario == "detached")
                    CEntMan.EventBus.RaiseLocalEvent(user, new LocalPlayerDetachedEvent(user));
                else if (scenario == "removed")
                    actions.RemoveAction(user, (action, comp));

                CGameTiming.InSimulation = true;
                actions.Update(0);
                Assert.That(probe.Executions, Is.EqualTo(scenario is "dispatch" or "clientOnly" ? 1 : 0));
                actions.Update(0);
                Assert.That(probe.Executions, Is.EqualTo(scenario is "dispatch" or "clientOnly" ? 1 : 0), "Input is drained once; a second update must not execute it again.");
            }
            finally
            {
                CGameTiming.InSimulation = simulation;
            }
        });
    }

    private static object? Invoke(Type declaringType, object instance, string method, params object?[] arguments)
    {
        return declaringType.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, arguments);
    }
}

[RegisterComponent]
public sealed partial class CmuActionPredictionProbeComponent : Component
{
    public int Executions;
}

public sealed partial class CmuQueuedProbeActionEvent : InstantActionEvent;

public sealed class CmuActionPredictionProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CmuActionPredictionProbeComponent, CmuQueuedProbeActionEvent>(OnAction);
    }

    private static void OnAction(Entity<CmuActionPredictionProbeComponent> action, ref CmuQueuedProbeActionEvent args)
    {
        action.Comp.Executions++;
        args.Handled = true;
    }
}

#pragma warning restore RA0002
