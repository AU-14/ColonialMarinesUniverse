using Content.IntegrationTests.Fixtures;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat.Melee;
using Content.Shared.CombatMode;
using Content.Shared.NPC;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMUNpcTargetInvalidationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task TargetLostBeforeMeleeStartupFailsPlanAndAllowsRecovery()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var owner = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<CombatModeComponent>(owner);
            var target = SEntMan.SpawnEntity(null, map.GridCoords);
            var htn = SEntMan.EnsureComponent<HTNComponent>(owner);
            SEntMan.EnsureComponent<ActiveNPCComponent>(owner);
            htn.ConstantlyReplan = false;
            htn.PlanAccumulator = 10f;
            htn.CheckServices = false;
            var melee = new MeleeOperator { TargetKey = "Target" };
            melee.Initialize(SEntMan.EntitySysManager);
            var preceding = new HTNPrimitiveTask { Operator = new NoOperator() };
            var attack = new HTNPrimitiveTask { Operator = melee };
            htn.Blackboard.SetValue("Target", target);
            htn.Plan = new HTNPlan([preceding, attack], [], [null, null]);

            // Planning accepted this target, but it is gone before the preceding task completes.
            htn.Blackboard.Remove<EntityUid>("Target");
            var system = Server.System<HTNSystem>();
            var count = 0;
            system.UpdateNPC(ref count, 100, 1f / 30);
            Assert.That(htn.Plan, Is.Null, "The invalid plan must shut down through the normal failed-task path.");
            Assert.That(SEntMan.HasComponent<NPCMeleeCombatComponent>(owner), Is.False,
                "Failed startup must not leave a combat component with an invalid target.");

            htn.Blackboard.SetValue("Target", target);
            htn.Plan = new HTNPlan([preceding, attack], [], [null, null]);
            system.UpdateNPC(ref count, 100, 1f / 30);
            Assert.That(htn.Plan?.CurrentOperator, Is.SameAs(melee));
            Assert.That(SEntMan.GetComponent<NPCMeleeCombatComponent>(owner).Target, Is.EqualTo(target),
                "A later valid plan must still enter melee combat.");
            system.ShutdownTask(melee, htn.Blackboard, HTNOperatorStatus.Failed);
            system.ShutdownPlan(htn);
            SEntMan.DeleteEntity(owner);
            SEntMan.DeleteEntity(target);
        });
    }
}
