#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.ForTheHive;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoSpriteTest : GameTest
{
    private static readonly (EntProtoId Prototype, ResPath Sprite)[] ExpectedSprites =
    [
        ("CMXenoCarrier", new ResPath("/Textures/_CMU14/Mobs/Xenos/Carrier/carrier.rsi")),
        ("RMCXenoCarrierEggsac", new ResPath("/Textures/_CMU14/Mobs/Xenos/Carrier/eggsac_carrier.rsi")),
        ("CMXenoRunner", new ResPath("/Textures/_CMU14/Mobs/Xenos/Runner/runner.rsi")),
        ("RMCXenoRunnerAcider", new ResPath("/Textures/_CMU14/Mobs/Xenos/Runner/acider_runner.rsi")),
        ("CMXenoLesserDrone", new ResPath("/Textures/_CMU14/Mobs/Xenos/LesserDrone/lesser_drone.rsi")),
    ];

    [Test]
    public async Task XenosUseCmuSprites()
    {
        await Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (prototype, expectedSprite) in ExpectedSprites)
                {
                    var uid = CEntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
                    var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                    Assert.That(sprite.BaseRSI?.Path, Is.EqualTo(expectedSprite), $"{prototype} uses the wrong sprite set.");
                }

                var acider = CEntMan.SpawnEntity("RMCXenoRunnerAcider", MapCoordinates.Nullspace);
                var forTheHive = CEntMan.GetComponent<ForTheHiveComponent>(acider);
                Assert.That(
                    forTheHive.BaseSprite,
                    Is.EqualTo("_CMU14/Mobs/Xenos/Runner/acider_runner.rsi"));
                Assert.That(
                    forTheHive.ActiveSprite,
                    Is.EqualTo("_CMU14/Mobs/Xenos/Runner/acider_runner_primed.rsi"));

                var eggsac = CEntMan.SpawnEntity("RMCXenoCarrierEggsac", MapCoordinates.Nullspace);
                var eggsacRsi = CEntMan.GetComponent<SpriteComponent>(eggsac).BaseRSI;
                Assert.That(eggsacRsi, Is.Not.Null);
                foreach (var state in new[] { "eggsac_3_downed", "eggsac_3_downed_active", "eggsac_3_rest" })
                {
                    Assert.That(
                        eggsacRsi!.TryGetState(state, out _),
                        Is.True,
                        $"The CMU Eggsac Carrier sprite is missing state '{state}'.");
                }
            });
        });
    }
}
