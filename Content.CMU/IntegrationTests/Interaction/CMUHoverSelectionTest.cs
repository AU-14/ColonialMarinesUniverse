using System.Numerics;
using System.Reflection;
using Content.Client.Clickable;
using Content.Client.Gameplay;
using Content.Client.Sprite;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Interaction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Interaction;

[TestFixture]
public sealed class CMUHoverSelectionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task HoverPreservesDrawOrderAndLiveClickFiltering()
    {
        var map = await Pair.CreateTestMap();
        var targets = new List<NetEntity>();
        await Server.WaitPost(() =>
        {
            var player = SEntMan.SpawnEntity(null, map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, player);
            for (var i = 0; i < 3; i++)
                targets.Add(SEntMan.GetNetEntity(SEntMan.SpawnEntity("ClickTestRotatingCornerVisible", map.GridCoords)));
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var state = new GameplayStateBase();
            IoCManager.InjectDependencies(state);
            var comparer = typeof(GameplayStateBase).GetNestedType("ClickableEntityComparer", BindingFlags.NonPublic)!;
            typeof(GameplayStateBase).GetField("_comparer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(state, Activator.CreateInstance(comparer, nonPublic: true));

            var ids = targets.Select(CEntMan.GetEntity).ToArray();
            var sprites = ids.Select(CEntMan.GetComponent<SpriteComponent>).ToArray();
            var transform = Client.System<SharedTransformSystem>();
            var spriteSystem = Client.System<SpriteSystem>();
            var coordinates = transform.GetMapCoordinates(ids[0]);
            var eye = Client.ResolveDependency<IEyeManager>().CurrentEye;
            eye.Rotation = Angle.Zero;

            for (var i = 0; i < ids.Length; i++)
            {
                CEntMan.GetComponent<ClickableComponent>(ids[i]).Bounds =
                    new ClickableComponent.DirBoundData { All = new Box2(-1, -1, 1, 1) };
                spriteSystem.SetDrawDepth((ids[i], sprites[i]), 1);
                sprites[i].RenderOrder = 0;
            }

            void Check(EntityUid expected)
            {
                Assert.That(state.GetClickedEntity(coordinates, eye), Is.EqualTo(expected));
                Assert.That(state.GetClickedEntity(coordinates, eye),
                    Is.EqualTo(state.GetClickableEntities(coordinates, eye).First()));
            }

            Check(ids.Max()); // UID breaks an otherwise exact tie.
            spriteSystem.SetDrawDepth((ids[0], sprites[0]), 2);
            Check(ids[0]);
            spriteSystem.SetDrawDepth((ids[1], sprites[1]), 2);
            sprites[1].RenderOrder = 10;
            Check(ids[1]);

            // Y-sorting must still be evaluated in the active eye's orientation.
            for (var i = 0; i < ids.Length; i++)
            {
                spriteSystem.SetDrawDepth((ids[i], sprites[i]), 1);
                sprites[i].RenderOrder = 0;
            }
            var firstTransform = CEntMan.GetComponent<TransformComponent>(ids[0]);
            var secondTransform = CEntMan.GetComponent<TransformComponent>(ids[1]);
            var original = firstTransform.LocalPosition;
            transform.SetLocalPosition(firstTransform, original - new Vector2(0, 0.1f));
            transform.SetLocalPosition(secondTransform, original + new Vector2(0, 0.1f));
            Check(ids[0]);
            eye.Rotation = Angle.FromDegrees(180);
            Check(ids[1]);
            eye.Rotation = Angle.Zero;

            // All filters operate on live state, even when the cursor hasn't moved.
            spriteSystem.SetDrawDepth((ids[0], sprites[0]), 3);
            spriteSystem.SetDrawDepth((ids[1], sprites[1]), 2);
            CEntMan.AddComponent<FadingSpriteComponent>(ids[0]);
            Check(ids[1]);
            Assert.That(state.GetClickableEntities(coordinates, eye, excludeFaded: false).First(), Is.EqualTo(ids[0]));
            CEntMan.RemoveComponent<FadingSpriteComponent>(ids[0]);
            spriteSystem.SetVisible((ids[0], sprites[0]), false);
            Check(ids[1]);
            spriteSystem.SetVisible((ids[0], sprites[0]), true);
            CEntMan.AddComponent<InteractionTransparencyComponent>(ids[0]);
            Check(ids[1]);
            Assert.That(state.GetClickableEntities(coordinates, eye, ignoreInteractionTransparency: true).First(),
                Is.EqualTo(ids[0]));
            CEntMan.RemoveComponent<InteractionTransparencyComponent>(ids[0]);
            Check(ids[0]);
            CEntMan.RemoveComponent<ClickableComponent>(ids[0]);
            Check(ids[1]);

            Assert.That(state.GetClickedEntity(coordinates, null), Is.Null);
            Assert.That(state.GetClickedEntity(new MapCoordinates(coordinates.Position + new Vector2(100), coordinates.MapId), eye), Is.Null);
        });
    }
}
