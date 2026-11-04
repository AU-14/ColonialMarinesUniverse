using Content.Client.CMU14.Ghost;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Ghost;
using Content.Shared.Ghost.Components;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests.CMU14.Ghost;

public sealed class CMUGhostAppearanceClientTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task CopiedBodySurvivesLaterGhostDamageAppearanceUpdates()
    {
        await Client.WaitAssertion(() =>
        {
            var map = CEntMan.System<SharedMapSystem>().CreateMap(runMapInit: true);
            var coordinates = new EntityCoordinates(map, Vector2.Zero);
            var body = CEntMan.SpawnEntity("CMXenoDrone", coordinates);
            var ghost = CEntMan.SpawnEntity("MobObserver", coordinates);
            var fallback = CEntMan.SpawnEntity("MobObserver", coordinates);
            CEntMan.System<CMUGhostAppearanceSystem>().SetSource(ghost, body);
            CEntMan.System<CMUGhostAppearanceClientSystem>().FrameUpdate(0);

            var sprites = CEntMan.System<SpriteSystem>();
            var sprite = CEntMan.GetComponent<SpriteComponent>(ghost);
            var baseLayer = sprites.LayerMapGet((ghost, sprite), XenoVisualLayers.Base);
            var state = sprites.LayerGetRsiState((ghost, sprite), baseLayer);
            var layers = sprite.LayerData.Count;
            var appearance = CEntMan.System<AppearanceSystem>();
            appearance.SetData(ghost, GhostVisuals.Damage, "ghost_Piercing2");
            appearance.SetData(fallback, GhostVisuals.Damage, "ghost_Piercing2");
            appearance.FrameUpdate(0);

            Assert.That(sprite.LayerData.Count, Is.EqualTo(layers),
                "An old ghost visualizer must not create a ghost damage layer on the copied body.");
            Assert.That(sprites.LayerGetRsiState((ghost, sprite), baseLayer), Is.EqualTo(state),
                "The copied body must retain its valid body state after appearance updates.");
            var fallbackSprite = CEntMan.GetComponent<SpriteComponent>(fallback);
            var fallbackLayer = sprites.LayerMapGet((fallback, fallbackSprite), "ghostVariant");
            Assert.That(sprites.LayerGetRsiState((fallback, fallbackSprite), fallbackLayer).ToString(),
                Is.EqualTo("ghost_Piercing2"), "Ghosts without a copied body keep their original death appearance.");
        });
    }
}
