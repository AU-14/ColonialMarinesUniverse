using Content.Client.Light.Components;
using Content.Client.Light.EntitySystems;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests.Tests.Light;

[TestFixture]
[TestOf(typeof(ExpendableLightSystem))]
public sealed class ExpendableLightVisualOwnerRegressionTest : GameTest
{
    [Test]
    public async Task AirFlaresApplyLightBehaviorBeforeSkippingMissingVisualLayers()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        var serverEntities = new List<EntityUid>();
        var netEntities = new Dictionary<string, NetEntity>();
        string[] prototypes =
        [
            "RMCAirFlare",
            "RMCAirFlareCAS",
            "RMCAirFlareL96",
        ];

        try
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, map.Grid.Owner);
                foreach (var prototype in prototypes)
                {
                    var uid = SEntMan.SpawnEntity(prototype, map.GridCoords);
                    serverEntities.Add(uid);
                    netEntities.Add(prototype, SEntMan.GetNetEntity(uid));
                    Assert.That(SEntMan.GetComponent<ExpendableLightComponent>(uid).CurrentState,
                        Is.EqualTo(ExpendableLightState.Lit), prototype);
                }
            });
            await Pair.RunUntilSynced();

            await Client.WaitAssertion(() =>
            {
            var behavior = Client.System<LightBehaviorSystem>();
            var sprite = Client.System<SpriteSystem>();

            AssertAirFlare("RMCAirFlare", hasSprite: false, hasAppearance: true, behaviorRunning: true, lightEnabled: true);
            AssertAirFlare("RMCAirFlareCAS", hasSprite: true, hasAppearance: true, behaviorRunning: true, lightEnabled: true);
            AssertAirFlare("RMCAirFlareL96", hasSprite: true, hasAppearance: false, behaviorRunning: false, lightEnabled: true);

            void AssertAirFlare(
                string prototype,
                bool hasSprite,
                bool hasAppearance,
                bool behaviorRunning,
                bool lightEnabled)
            {
                var uid = CEntMan.GetEntity(netEntities[prototype]);
                var lightBehavior = CEntMan.GetComponent<LightBehaviourComponent>(uid);
                var pointLight = CEntMan.GetComponent<PointLightComponent>(uid);
                Assert.Multiple(() =>
                {
                    Assert.That(CEntMan.HasComponent<SpriteComponent>(uid), Is.EqualTo(hasSprite), prototype);
                    Assert.That(CEntMan.HasComponent<AppearanceComponent>(uid), Is.EqualTo(hasAppearance),
                        $"{prototype} appearance");
                    Assert.That(behavior.HasRunningBehaviours((uid, lightBehavior)), Is.EqualTo(behaviorRunning),
                        $"{prototype} behavior");
                    Assert.That(pointLight.Enabled, Is.EqualTo(lightEnabled), $"{prototype} point light");
                });

                if (!hasSprite)
                    return;

                var spriteComponent = CEntMan.GetComponent<SpriteComponent>(uid);
                Assert.Multiple(() =>
                {
                    Assert.That(sprite.LayerMapTryGet(
                        (uid, spriteComponent),
                        ExpendableLightVisualLayers.Glow,
                        out _,
                        false), Is.True, $"{prototype} glow");
                    Assert.That(sprite.LayerMapTryGet(
                        (uid, spriteComponent),
                        ExpendableLightVisualLayers.Overlay,
                        out _,
                        false), Is.False, $"{prototype} overlay");
                });
            }
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
                foreach (var uid in serverEntities)
                {
                    if (SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
        }
    }
}
