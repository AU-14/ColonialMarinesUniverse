#pragma warning disable RA0002 // Regression inspects deadline and rate state owned by SatiationSystem.
using Content.IntegrationTests.Fixtures;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.IntegrationTests.CMU14.Nutrition;

[TestFixture]
public sealed class CMUSatiationDeadlineTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: satiation
  id: CMUDeadlineTestSatiation
  baseChangeRate: -1
  maximumValue: 100
  startingValueMinimum: 50
  startingValueMaximum: 50
  thresholds:
    Low: 0
    Middle: 20
    High: 100
  changeModifiers:
    Middle: 0.5
    High: 1
  alertCategory: Hunger

- type: entity
  id: CMUDeadlineTestDummy
  components:
  - type: Satiation
    satiations:
      Hunger:
        prototype: CMUDeadlineTestSatiation
""";

    [Test]
    public async Task FutureAndNullDeadlinesStayIdleAndDueThresholdChangesStillApply()
    {
        EntityUid uid = default;
        Robust.Shared.Timing.GameTick version = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                uid = SEntMan.SpawnEntity("CMUDeadlineTestDummy", MapCoordinates.Nullspace);
                var component = SEntMan.GetComponent<SatiationComponent>(uid);
                SEntMan.System<SatiationSystem>().SetValue((uid, component), SatiationSystem.Hunger, 20.5f);
                var hunger = component.GetOrNull(SatiationSystem.Hunger)!;
                Assert.That(hunger.ActualChangeRate, Is.EqualTo(-1));
                Assert.That(hunger.NextChangeRateModUpdateTime,
                    Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromSeconds(0.5)));
                Assert.That(hunger.NextAlertUpdateTime, Is.Null);
                version = component.LastModifiedTick;
            });
            await Pair.RunTicksSync(Pair.SecondsToTicks(0.3f));
            await Server.WaitAssertion(() =>
            {
                var component = SEntMan.GetComponent<SatiationComponent>(uid);
                Assert.That(component.LastModifiedTick, Is.EqualTo(version));
                Assert.That(component.GetOrNull(SatiationSystem.Hunger)!.ActualChangeRate, Is.EqualTo(-1));
            });
            await Pair.RunTicksSync(Pair.SecondsToTicks(0.4f));
            await Server.WaitAssertion(() =>
            {
                var component = SEntMan.GetComponent<SatiationComponent>(uid);
                Assert.That(component.LastModifiedTick, Is.GreaterThan(version));
                Assert.That(component.GetOrNull(SatiationSystem.Hunger)!.ActualChangeRate, Is.EqualTo(-0.5f));
                SEntMan.System<SatiationSystem>().SetValue((uid, component), SatiationSystem.Hunger, 0f);
                var hunger = component.GetOrNull(SatiationSystem.Hunger)!;
                Assert.That(hunger.NextChangeRateModUpdateTime, Is.Null);
                Assert.That(hunger.NextAlertUpdateTime, Is.Null);
                version = component.LastModifiedTick;
            });
            await Pair.RunTicksSync(Pair.SecondsToTicks(1));
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.GetComponent<SatiationComponent>(uid).LastModifiedTick, Is.EqualTo(version)));
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(uid));
        }
    }
}
