using Content.IntegrationTests.Fixtures;
using Content.Server.Stunnable;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Stunnable;

[TestFixture]
[TestOf(typeof(SharedStunSystem))]
public sealed class StunXenoImmunityMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: MobHuman
          id: StunXenoImmunityBulwarkActive
          components:
          - type: XenoBulwark
            encased: true

        - type: entity
          parent: MobHuman
          id: StunXenoImmunityBulwarkInactive
          components:
          - type: XenoBulwark
            encased: false

        - type: entity
          parent: MobHuman
          id: StunXenoImmunityBurrowActive
          components:
          - type: XenoBurrow
            active: true

        - type: entity
          parent: MobHuman
          id: StunXenoImmunityBurrowInactive
          components:
          - type: XenoBurrow
            active: false

        - type: entity
          parent: MobHuman
          id: StunXenoImmunityCrestActive
          components:
          - type: XenoCrest
            lowered: true

        - type: entity
          parent: MobHuman
          id: StunXenoImmunityCrestInactive
          components:
          - type: XenoCrest
            lowered: false

        - type: entity
          parent: MobHuman
          id: StunXenoImmunityFortifyActive
          components:
          - type: XenoFortify
            fortified: true

        - type: entity
          parent: MobHuman
          id: StunXenoImmunityFortifyInactive
          components:
          - type: XenoFortify
            fortified: false

        - type: entity
          parent: MobHuman
          id: StunXenoImmunityCarrionActive
          components:
          - type: XenoCarrionMantle

        - type: entity
          parent: MobHuman
          id: StunXenoImmunityCarrionControl
        """;

    [Test]
    public async Task MigratedXenoImmunitiesGateNormalParalysisButForceBypassesAttempts()
    {
        await Server.WaitAssertion(() =>
        {
            var stun = Server.System<StunSystem>();
            var pairs = new[]
            {
                (Active: "StunXenoImmunityBulwarkActive", Inactive: "StunXenoImmunityBulwarkInactive"),
                (Active: "StunXenoImmunityBurrowActive", Inactive: "StunXenoImmunityBurrowInactive"),
                (Active: "StunXenoImmunityCrestActive", Inactive: "StunXenoImmunityCrestInactive"),
                (Active: "StunXenoImmunityFortifyActive", Inactive: "StunXenoImmunityFortifyInactive"),
                (Active: "StunXenoImmunityCarrionActive", Inactive: "StunXenoImmunityCarrionControl"),
            };

            foreach (var pair in pairs)
            {
                var active = SSpawn(pair.Active);
                var inactive = SSpawn(pair.Inactive);

                Assert.That(stun.TryParalyze(active, TimeSpan.FromSeconds(1), refresh: true), Is.False,
                    $"{pair.Active} must cancel normal paralysis while its defensive state is active");
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<StunnedComponent>(active), Is.False);
                    Assert.That(SEntMan.HasComponent<KnockedDownComponent>(active), Is.False);
                });

                Assert.That(stun.TryParalyze(inactive, TimeSpan.FromSeconds(1), refresh: true), Is.True,
                    $"{pair.Inactive} must not grant unconditional immunity");
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<StunnedComponent>(inactive), Is.True);
                    Assert.That(SEntMan.HasComponent<KnockedDownComponent>(inactive), Is.True);
                });

                Assert.That(stun.TryParalyze(active, TimeSpan.FromSeconds(1), refresh: true, force: true), Is.True,
                    "force bypasses conditional attempt cancellation while retaining status prototype filters");
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<StunnedComponent>(active), Is.True);
                    Assert.That(SEntMan.HasComponent<KnockedDownComponent>(active), Is.True);
                });
            }
        });
    }
}
