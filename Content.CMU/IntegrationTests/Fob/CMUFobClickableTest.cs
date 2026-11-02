using System.Linq;
using System.Numerics;
using Content.Client.Clickable;
using Content.Client.CMU14.Fob;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Fob;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Fob;

[TestFixture, TestOf(typeof(CMUFobPresentationSystem))]
public sealed class CMUFobClickableTest : GameTest
{
    [TestCase("CMUFobMonitoringCrate")]
    [TestCase("CMUFobSupportGun")]
    public async Task AnimatedArtworkRetainsPixelHitsAfterTranslationRotationAndScaling(string prototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        await Server.WaitPost(() =>
        {
            target = SEntMan.SpawnEntity(prototype, map.GridCoords);
            var appearance = Server.System<SharedAppearanceSystem>();
            appearance.SetData(target, CMUFobPresentationVisuals.Status, CMUFobPresentationStatus.Off);
            appearance.SetData(target, CMUFobGunVisuals.Installed, true);
            appearance.SetData(target, CMUFobPresentationVisuals.Open, true);
        });
        await RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var uid = CEntMan.GetEntity(SEntMan.GetNetEntity(target));
            var eye = Client.ResolveDependency<IEyeManager>().CurrentEye;
            eye.Rotation = 0;
            Client.System<CMUFobPresentationSystem>().FrameUpdate(0);
            var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
            var animation = CEntMan.GetComponent<CMUFobAnimationComponent>(uid);
            var sprites = Client.System<SpriteSystem>();
            var clicks = Client.System<ClickableSystem>();
            var pos = Client.System<SharedTransformSystem>().GetWorldPosition(uid);
            bool Hit(Vector2 offset) => clicks.CheckClick((uid, null, sprite, null), pos + offset,
                eye, false, out _, out _, out _);

            if (prototype == "CMUFobMonitoringCrate")
            {
                Assert.That(Hit(new Vector2(0, 3f / 32)), Is.True,
                    "The dark interior of an open crate must remain clickable between the lid and rim.");
                Assert.That(Hit(new Vector2(0.9f, 0.9f)), Is.False,
                    "Stretching a solid pixel must not create an oversized hit area outside the crate.");
            }

            // Exercise a real generated lid/barrel crop, with no inherited layer masking a missed hit.
            var part = animation.Parts.First(p => p.MovingPart == 0);
            var layer = animation.Layers.Single(l => l.Texture == part.Texture);
            foreach (var other in sprite.AllLayers.OfType<SpriteComponent.Layer>())
                sprites.LayerSetVisible(other, other == layer);

            var offset = new Vector2(1.5f, 1);
            sprites.LayerSetOffset(layer, offset);
            sprites.LayerSetRotation(layer, Angle.FromDegrees(90));
            sprites.LayerSetScale(layer, new Vector2(0.4f, 2));
            Assert.That(Hit(offset), Is.True, "Hover/click must follow the visible moving part.");
            Assert.That(Hit(offset - new Vector2(12f / 32, 0)), Is.True,
                "Rotation and stretching must move the far end of the part's hit area with its artwork.");
            Assert.That(Hit(offset + new Vector2(0, 12f / 32)), Is.False,
                "The unrotated end must not retain a phantom hit area.");
            Assert.That(Hit(Vector2.Zero), Is.False, "The old position must not retain a phantom hit area.");
            Assert.That(Hit(offset + new Vector2(0, 1)), Is.False,
                "Pixels elsewhere in the packed RSI atlas must not extend the cropped part's hit area.");
        });
        await Server.WaitPost(() => SEntMan.DeleteEntity(target));
    }
}
