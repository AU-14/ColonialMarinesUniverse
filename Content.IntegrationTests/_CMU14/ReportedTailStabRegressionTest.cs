using System.Numerics;
using Content.IntegrationTests.Utility;
using Content.Server.Movement.Components;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Xenonids.Stab;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class ReportedTailStabRegressionTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: CMXenoDrone
          id: CMUTestReportedTailDrone
          components:
          - type: XenoTailStab
            tailDamage:
              types:
                Piercing: 30
        """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task StabUsesMovingTargetsViewedPositionAndRespectsWalls(bool blocked)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        var sessions = await pair.Server.AddDummySessions(1);
        await pair.RunTicksSync(15);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var attacker = entities.SpawnEntity("CMUTestReportedTailDrone", map.GridCoords);
            entities.EnsureComponent<XenoTailStabComponent>(attacker);
            pair.Server.PlayerMan.SetAttachedEntity(sessions[0], attacker);
            var viewed = map.GridCoords.Offset(new Vector2(1.5f, 0));
            var target = entities.SpawnEntity("CMMobHuman", viewed.Offset(new Vector2(0, 1.5f)));
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            var history = entities.EnsureComponent<LagCompensationComponent>(target);
            history.Positions.Clear();
            history.Positions.Enqueue((timing.CurTime - TimeSpan.FromMilliseconds(50), viewed, Angle.Zero));
            history.Positions.Enqueue((timing.CurTime, entities.GetComponent<TransformComponent>(target).Coordinates, Angle.Zero));
            entities.System<SharedRMCLagCompensationSystem>().SetLastRealTick(sessions[0].UserId, timing.CurTick - 10);
            if (blocked)
                entities.SpawnEntity("CMWallMetal", map.GridCoords.Offset(new Vector2(1, 0)));
            var damage = entities.GetComponent<DamageableComponent>(target);
            var damageSystem = entities.System<DamageableSystem>();
            var before = damageSystem.GetTotalDamage((target, damage));
            var ev = new XenoTailStabEvent { Performer = attacker, Target = viewed };
            entities.EventBus.RaiseLocalEvent(attacker, ev);
            if (blocked)
                Assert.That(damageSystem.GetTotalDamage((target, damage)), Is.EqualTo(before));
            else
                Assert.That(damageSystem.GetTotalDamage((target, damage)), Is.GreaterThan(before), "The target moved away after the attacker saw it inside the stab.");
        });
        await pair.CleanReturnAsync();
    }
}
