using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Gravity;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Stun;
using Content.Shared.Stunnable;
using Content.Shared.Gravity;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Stunnable;

[TestFixture]
[TestOf(typeof(RMCSizeStunSystem))]
public sealed class StunTriggerMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: StunTriggerMergeSource
          components:
          - type: RMCSize
            size: SmallXeno
          - type: RMCStunOnHit
            stuns:
            - stunArea: 3
              stunTime: 5
              slowTime: 0
              superSlowTime: 0
              knockBackPowerMin: 0
              knockBackPowerMax: 0
              knockBackSpeed: 0

        - type: entity
          id: StunTriggerMergeDecoy
          components:
          - type: RMCSize
            size: SmallXeno

        - type: entity
          parent: MobHuman
          id: StunTriggerMergeTarget
          components:
          - type: RMCSize
            size: SmallXeno
        """;

    [Test]
    public async Task AreaTriggerSelectsMobStateInsteadOfSourceOrSizeOnlyDecoy()
    {
        var map = await Pair.CreateTestMap();
        EntityUid source = default;
        EntityUid decoy = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<GravitySystem>().EnableGravity(map.MapUid,
                SEntMan.EnsureComponent<GravityComponent>(map.MapUid));
            source = SSpawnAtPosition("StunTriggerMergeSource", map.GridCoords);
            decoy = SSpawnAtPosition("StunTriggerMergeDecoy", map.GridCoords.Offset(new Vector2(0.1f, 0)));
            target = SSpawnAtPosition("StunTriggerMergeTarget", map.GridCoords.Offset(new Vector2(0.2f, 0)));
        });
        await Pair.RunTicksSync(2); // Populate the spatial lookup before the area query.
        await Server.WaitAssertion(() =>
        {
            var trigger = new RMCTriggerEvent(null, false);
            SEntMan.EventBus.RaiseLocalEvent(source, ref trigger);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True,
                    "the only MobState candidate must receive the area paralysis");
                Assert.That(SEntMan.HasComponent<StunnedComponent>(source), Is.False,
                    "the size-bearing trigger itself is not an eligible mob target");
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(source), Is.False);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(decoy), Is.False,
                    "an unrelated size-only entity must not consume the single-target break");
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(decoy), Is.False);
            });
        });
    }
}
