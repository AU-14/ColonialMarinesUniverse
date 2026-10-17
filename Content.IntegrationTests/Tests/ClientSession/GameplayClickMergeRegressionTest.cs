using System.Linq;
using System.Reflection;
using Content.Client.Gameplay;
using Content.IntegrationTests.Fixtures;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.IntegrationTests.Tests.ClientSession;

[TestFixture]
public sealed class GameplayClickMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: GameplayClickTransparentTarget
  parent: ClickTestRotatingCornerVisible
  components:
  - type: Clickable
    bounds:
      south: "-0.5,-0.5,0.5,0.5"
      north: "-0.5,-0.5,0.5,0.5"
      east: "-0.5,-0.5,0.5,0.5"
      west: "-0.5,-0.5,0.5,0.5"
  - type: InteractionTransparency
""";

    [Test]
    public async Task IgnoreTransparencyReturnsTargetThatNormalGameplayHitTestingSkips()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid player = default;
        EntityUid target = default;
        NetEntity targetNet = default;
        await Server.WaitAssertion(() =>
        {
            player = SEntMan.SpawnEntity(null, map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(session, player);
            var transform = Server.System<SharedTransformSystem>();
            target = SEntMan.SpawnEntity("GameplayClickTransparentTarget", transform.GetMapCoordinates(player));
            targetNet = SEntMan.GetNetEntity(target);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var state = new GameplayStateBase();
            IoCManager.InjectDependencies(state);
            var comparerType = typeof(GameplayStateBase)
                .GetNestedType("ClickableEntityComparer", BindingFlags.NonPublic)!;
            typeof(GameplayStateBase)
                .GetField("_comparer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(state, Activator.CreateInstance(comparerType, nonPublic: true));

            var clientTarget = CEntMan.GetEntity(targetNet);
            var coordinates = CEntMan.System<SharedTransformSystem>().GetMapCoordinates(clientTarget);
            var eye = Client.ResolveDependency<IEyeManager>().CurrentEye;
            Assert.That(eye, Is.Not.Null);

            var ordinary = state.GetClickableEntities(coordinates, eye, excludeFaded: false,
                ignoreInteractionTransparency: false).ToArray();
            var ignored = state.GetClickableEntities(coordinates, eye, excludeFaded: false,
                ignoreInteractionTransparency: true).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(ordinary, Does.Not.Contain(clientTarget),
                    "normal gameplay selection must skip a transparent target overlapping the local player");
                Assert.That(ignored, Does.Contain(clientTarget),
                    "explicit overlay callers must be able to recover the same clickable target");
            });

            var sprite = CEntMan.GetComponent<Robust.Client.GameObjects.SpriteComponent>(clientTarget);
            Assert.That(CEntMan.System<Content.Client.Clickable.ClickableSystem>().CheckClick(
                    (clientTarget, null, sprite, CEntMan.GetComponent<TransformComponent>(clientTarget)),
                    coordinates.Position,
                    eye,
                    excludeFaded: false,
                    out _,
                    out _,
                    out _),
                Is.True, "the upstream nullable ClickableComponent path must remain independently clickable");
        });

        await Server.WaitPost(() =>
        {
            Server.PlayerMan.SetAttachedEntity(session, originalAttached);
            SEntMan.DeleteEntity(target);
            SEntMan.DeleteEntity(player);
        });
    }
}
