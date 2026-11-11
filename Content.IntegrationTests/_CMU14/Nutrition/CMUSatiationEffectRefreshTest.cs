#pragma warning disable RA0002 // Regression observes effect projections and their modification ticks.
using Content.IntegrationTests.Fixtures;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Nutrition;

[TestFixture]
public sealed class CMUSatiationEffectRefreshTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: satiation
  id: CMUIdleEffectTestSatiation
  baseChangeRate: 0
  maximumValue: 100
  startingValueMinimum: 75
  startingValueMaximum: 75
  thresholds:
    Low: 0
    Middle: 50
    High: 100
  alertCategory: Hunger

- type: satiation
  id: CMUDecayingEffectTestSatiation
  parent: CMUIdleEffectTestSatiation
  baseChangeRate: -1

- type: entity
  id: CMUIdleEffectTestDummy
  components:
  - type: CMUSatiationRefreshProbe
  - type: MovementSpeedModifier
  - type: Satiation
    satiations:
      Hunger:
        prototype: CMUIdleEffectTestSatiation
  - type: SatiationSpeedModifier
    satiations:
      Hunger:
        thresholds:
          50: 0.5
          100: 1

- type: entity
  id: CMUDecayingEffectTestDummy
  parent: CMUIdleEffectTestDummy
  components:
  - type: Satiation
    satiations:
      Hunger:
        prototype: CMUDecayingEffectTestSatiation

""";

    [Test]
    public async Task DeadlineChangesStillDirtyEffectsAndTimedThresholdCrossingRefreshesMovement()
    {
        EntityUid uid = default;
        GameTick version = default;
        TimeSpan? deadline = null;
        try
        {
            await Server.WaitAssertion(() =>
            {
                uid = SEntMan.SpawnEntity("CMUDecayingEffectTestDummy", MapCoordinates.Nullspace);
                var effect = SEntMan.GetComponent<SatiationSpeedModifierComponent>(uid);
                version = effect.LastModifiedTick;
                deadline = effect.Satiations[SatiationSystem.Hunger].ProjectedThresholdChangeTime;
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                var probe = SEntMan.GetComponent<CMUSatiationRefreshProbeComponent>(uid);
                probe.Refreshes = 0;
                SEntMan.System<SatiationSystem>().SetValue((uid, SEntMan.GetComponent<SatiationComponent>(uid)),
                    SatiationSystem.Hunger, 50.1f);
                var effect = SEntMan.GetComponent<SatiationSpeedModifierComponent>(uid);
                Assert.That(effect.Satiations[SatiationSystem.Hunger].ProjectedThresholdChangeTime, Is.LessThan(deadline));
                Assert.That(effect.LastModifiedTick, Is.GreaterThan(version), "changed projected time must remain synchronized");
                Assert.That(probe.Refreshes, Is.Zero, "deadline alone does not alter movement");
            });
            await Pair.RunTicksSync(Pair.SecondsToTicks(0.3f));
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<CMUSatiationRefreshProbeComponent>(uid).Refreshes, Is.EqualTo(1));
                Assert.That(SEntMan.GetComponent<MovementSpeedModifierComponent>(uid).WalkSpeedModifier, Is.EqualTo(0.5f));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (uid.IsValid())
                    SEntMan.DeleteEntity(uid);
            });
        }
    }
}

[RegisterComponent]
public sealed partial class CMUSatiationRefreshProbeComponent : Component
{
    public int Refreshes;
}

public sealed class CMUSatiationRefreshProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUSatiationRefreshProbeComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
    }

    private void OnRefresh(Entity<CMUSatiationRefreshProbeComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
        => ent.Comp.Refreshes++;
}
