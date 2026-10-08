#nullable enable
using System.Collections;
using System.Reflection;
using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Client.Chat.UI;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chat;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Mobs.Components;
using Moq;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DLiveBillboardTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task TurningAroundAMobKeepsItsSpriteFacingTheViewer()
    {
        await Client.WaitAssertion(() =>
        {
            var maps = Client.System<SharedMapSystem>();
            maps.CreateMap(out var map, runMapInit: true);
            var mob = CEntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map));
            using var view = new CMU3DSceneControl(true) { SceneMap = map };
            view.SceneMaps.Add(map);
            view.SetBillboards([mob]);
            view.SetCameraOverride(CMU3DFirstPersonCamera.Frame(new Vector2(4, 0), MathF.PI / 2, 0, new Vector2(800, 600)));
            var candidate = Collect(view).Single(x => Value<EntityUid>(x, "Uid") == mob);
            var yaw = Value<float>(candidate, "PlaneYaw");
            Assert.That(MathF.Abs(MathF.Cos(yaw)), Is.LessThan(.001f),
                "A mob viewed from the east must not become an edge-on plane.");
            view.SetCameraOverride(CMU3DFirstPersonCamera.Frame(new Vector2(0, -4), 0, 0, new Vector2(800, 600)));
            candidate = Collect(view).Single(x => Value<EntityUid>(x, "Uid") == mob);
            Assert.That(MathF.Abs(MathF.Sin(Value<float>(candidate, "PlaneYaw"))), Is.LessThan(.001f));
        });
    }

    [Test]
    public async Task LookingAroundInPlaceDoesNotSpinTheMobArtwork()
    {
        await Client.WaitAssertion(() =>
        {
            Client.System<SharedMapSystem>().CreateMap(out var map, runMapInit: true);
            var mob = CEntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map));
            var sprite = CComp<SpriteComponent>(mob);
            using var view = new CMU3DSceneControl(true) { SceneMap = map };
            view.SceneMaps.Add(map);
            var first = Render(0);
            var turned = Render(.35f);
            Assert.Multiple(() =>
            {
                Assert.That(turned.Plane, Is.EqualTo(first.Plane).Within(.0001),
                    "Turning in place must not rotate a stationary sprite plane in the world.");
                Assert.That(turned.Up.Length, Is.EqualTo(first.Up.Length));
                for (var i = 0; i < Math.Min(first.Up.Length, turned.Up.Length); i++)
                    Assert.That(Vector2.Distance(turned.Up[i], first.Up[i]), Is.LessThan(.0001),
                        "Camera yaw must not spin the artwork inside its billboard.");
                Assert.That(turned.Textures, Is.EqualTo(first.Textures),
                    "Looking around from the same position must not cycle the mob's directional frames.");
            });

            Client.System<SharedTransformSystem>().SetWorldRotation(mob, MathF.PI / 2);
            var facingEast = Render(.35f);
            Assert.That(facingEast.Textures, Is.Not.EqualTo(turned.Textures),
                "The mob's actual facing must still select a different directional frame.");
            Assert.That(facingEast.Up, Is.EqualTo(turned.Up), "Changing facing must keep portrait artwork upright.");

            (float Plane, Vector2[] Up, object[] Textures) Render(float look)
            {
                view.SetCameraOverride(CMU3DFirstPersonCamera.Frame(new Vector2(0, -4), look, 0, new Vector2(800, 600)));
                var candidate = Collect(view).Single(x => Value<EntityUid>(x, "Uid") == mob);
                var eye = Value<Angle>(candidate, "EyeRotation");
                var handle = new Mock<DrawingHandleWorld>(MockBehavior.Loose, Texture.White);
                Client.System<SpriteSystem>().RenderSprite((mob, sprite), handle.Object, eye,
                    Value<Angle>(candidate, "Yaw"), Vector2.Zero, overrideDirection: Value<Direction?>(candidate, "Direction"));
                // Clyde.DrawEntity applies this view rotation after the real per-layer sprite matrices.
                var viewMatrix = Matrix3Helpers.CreateRotation(-eye);
                var up = handle.Invocations.Where(i => i.Method.Name == "SetTransform")
                    .Select(i => Vector2.TransformNormal(Vector2.UnitY, (Matrix3x2) i.Arguments[0] * viewMatrix)).ToArray();
                var textures = handle.Invocations.Where(i => i.Method.Name is "DrawTextureRect" or "DrawTextureRectRegion")
                    .Select(i => i.Arguments[0]).ToArray();
                Assert.That(up, Is.Not.Empty, "Exercise actual sprite layer rendering, not only the candidate metadata.");
                Assert.That(textures, Is.Not.Empty);
                return (Value<float>(candidate, "PlaneYaw"), up, textures);
            }
        });
    }

    [TestCase("ClickTestRotatingCornerVisibleNoRot")]
    [TestCase("ClickTestRotatingCornerInvisibleNoRot")]
    public async Task PortraitPickingUsesTheFrameSeenByTheViewer(string prototype)
    {
        await Client.WaitAssertion(() =>
        {
            Client.System<SharedMapSystem>().CreateMap(out var map, runMapInit: true);
            var target = CEntMan.SpawnEntity(prototype, new MapCoordinates(Vector2.Zero, map));
            CEntMan.AddComponent<MobStateComponent>(target);
            using var view = new CMU3DSceneControl(true) { SceneMap = map };
            view.SceneMaps.Add(map);
            // From the east, a south-facing sprite presents its west frame: the lower right corner.
            var camera = CMU3DFirstPersonCamera.Frame(new Vector2(4, 0), MathF.PI / 2, 0, new Vector2(800, 600));
            view.SetCameraOverride(camera);
            var candidate = Collect(view).Single(x => Value<EntityUid>(x, "Uid") == target);
            var published = (IList) typeof(CMU3DSceneControl)
                .GetField("_billboards", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
            published.Add(candidate);
            var center = Value<Vector3>(candidate, "Center");
            Assert.That(camera.TryProject(center + new Vector3(0, .4f, -.4f), out var visible), Is.True);
            view.Aim(visible, out var hit);
            Assert.That(hit, Is.EqualTo(target), "Aiming at the displayed frame must hit its pixels or explicit bounds.");
            Assert.That(camera.TryProject(center + new Vector3(0, .4f, .4f), out var empty), Is.True);
            view.Aim(empty, out hit);
            Assert.That(hit, Is.Null, "The original south frame's occupied corner is empty in this view.");
        });
    }

    [Test]
    public async Task ProjectileAppearsAndDisappearsWithoutPublishingAnotherMapSnapshot()
    {
        await Client.WaitAssertion(() =>
        {
            Client.System<SharedMapSystem>().CreateMap(out var map, runMapInit: true);
            using var view = new CMU3DSceneControl(true) { SceneMap = map };
            view.SceneMaps.Add(map);
            view.SetCameraOverride(CMU3DFirstPersonCamera.Frame(new Vector2(0, -4), 0, 0, new Vector2(800, 600)));
            view.SetBillboards([]);
            var bullet = CEntMan.SpawnEntity("CMBulletPistol9mm", new MapCoordinates(Vector2.Zero, map));
            Assert.That(Collect(view).Select(x => Value<EntityUid>(x, "Uid")), Does.Contain(bullet),
                "Transient combat visuals must not wait for the static geometry snapshot.");
            CEntMan.DeleteEntity(bullet);
            Assert.That(Collect(view).Select(x => Value<EntityUid>(x, "Uid")), Does.Not.Contain(bullet));
        });
    }

    private object[] Collect(CMU3DSceneControl view)
    {
        typeof(CMU3DSceneControl).GetMethod("CollectBillboards", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(view, [Client.System<SpriteSystem>(), Client.System<SharedTransformSystem>()]);
        return ((IEnumerable) typeof(CMU3DSceneControl)
            .GetField("_billboardCandidates", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!).Cast<object>().ToArray();
    }

    private static T Value<T>(object candidate, string property) =>
        (T) candidate.GetType().GetProperty(property)!.GetValue(candidate)!;

    [Test]
    public async Task SpeechTracksTheHeadAcrossFloorsAndHidesBehindGeometry()
    {
        await Client.WaitAssertion(() =>
        {
            var maps = Client.System<SharedMapSystem>();
            var mapUid = maps.CreateMap(out var map, runMapInit: true);
            var mob = CEntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map));
            using var view = new CMU3DSceneControl(true) { SceneMap = map };
            view.SceneMaps.Add(map);
            view.SetCameraOverride(CMU3DFirstPersonCamera.Frame(new Vector2(0, -8), 0, 0, new Vector2(800, 600)));
            Collect(view);
            var eyes = Client.ResolveDependency<IEyeManager>();
            var previous = eyes.MainViewport;
            try
            {
                eyes.MainViewport = view;
                var message = new ChatMessage(ChatChannel.Local, "hello", "hello", CEntMan.GetNetEntity(mob), null);
                using var bubble = new TextSpeechBubble(message, mob, "sayBox");
                var update = typeof(SpeechBubble).GetMethod("FrameUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!;
                void Frame() => update.Invoke(bubble, [new FrameEventArgs(.1f)]);
                Frame();
                var groundY = bubble.Position.Y;
                Assert.That(view.TryProjectHead(mob, 0, out var head), Is.True);
                Assert.That(groundY, Is.LessThan(head.Y / bubble.UIScale));
                CEntMan.AddComponent<CMUZLevelMapComponent>(mapUid).Depth = 1;
                Frame();
                var upstairsY = bubble.Position.Y;
                Assert.That(upstairsY, Is.LessThan(groundY - 30),
                    "The real speech control must rise with the sender's floor, without a 2D portal offset.");
                view.SetScene([new CMU3DSceneBox(new Vector3(0, -4, 3), new Vector3(2, .2f, 4), 0, Color.White, null)]);
                Frame();
                Assert.That(bubble.Modulate.A, Is.Zero, "Overhead speech must not reveal a sender through a solid wall.");
                view.SetScene([]);
                Frame();
                Assert.That(bubble.Modulate.A, Is.GreaterThan(0), "Removing the obstruction must restore the live bubble.");
                view.SetCameraOverride(CMU3DFirstPersonCamera.Frame(new Vector2(0, -8), MathF.PI, 0, new Vector2(800, 600)));
                Frame();
                Assert.That(bubble.Modulate.A, Is.Zero, "A sender behind the perspective camera must not produce a stray screen label.");
            }
            finally
            {
                eyes.MainViewport = previous;
            }
        });
    }
}
