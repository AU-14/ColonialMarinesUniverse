using System.Reflection;
using Content.Client.Graphics;
using Content.Client.Interaction;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.IntegrationTests.Tests.Interaction;

[TestFixture]
[TestOf(typeof(DragDropSystem))]
public sealed class DragDropMergeRegressionTest : InteractionTest
{
    protected override string PlayerPrototype => "DragDropMergeCultistPlayer";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DragDropMergeCultistPlayer
  parent: InteractionTestMob
  components:
  - type: Cultist

- type: entity
  id: DragDropMergeCorpse
  parent: MobHuman
  components:
  - type: DragDropReplayProbe

- type: entity
  id: DragDropMergeCultistCorpse
  parent: MobHuman
  components:
  - type: Cultist
  - type: DragDropReplayProbe
";

    [Test]
    public async Task CultistDeadTargetVetoRunsBeforeMouseCapture()
    {
        var ordinary = await SpawnTarget("DragDropMergeCorpse");
        await MakeDead(ordinary);

        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Down, cursorEntity: ordinary);
        await Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivate<object?>(CDragDropSys, "_draggedEntity"), Is.Null);
                Assert.That(GetPrivate<object?>(CDragDropSys, "_savedMouseDown"), Is.Null);
                Assert.That(GetPrivate<object?>(CDragDropSys, "_dragShadow"), Is.Null);
                Assert.That(GetPrivate<object>(CDragDropSys, "_state").ToString(), Is.EqualTo("NotDragging"));
                Assert.That(GetPrivate<HashSet<SpriteComponent>>(CDragDropSys, "_highlightedSprites"), Is.Empty);
            });
        });
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Up, cursorEntity: ordinary);

        var cultist = await SpawnTarget("DragDropMergeCultistCorpse");
        await MakeDead(cultist);

        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Down, cursorEntity: cultist);
        await Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivate<EntityUid?>(CDragDropSys, "_draggedEntity"), Is.EqualTo(ToClient(cultist)));
                Assert.That(GetPrivate<object?>(CDragDropSys, "_savedMouseDown"), Is.Not.Null);
                Assert.That(GetPrivate<object>(CDragDropSys, "_state").ToString(), Is.EqualTo("MouseDown"),
                    "dead Cultist targets bypass only the CM dead-corpse veto and reach normal drag capture");
            });

        });
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Up, cursorEntity: cultist);
        await Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivate<object?>(CDragDropSys, "_draggedEntity"), Is.Null);
                Assert.That(GetPrivate<object?>(CDragDropSys, "_savedMouseDown"), Is.Null);
                Assert.That(GetPrivate<object?>(CDragDropSys, "_dragShadow"), Is.Null);
                Assert.That(GetPrivate<HashSet<SpriteComponent>>(CDragDropSys, "_highlightedSprites"), Is.Empty);
                Assert.That(GetPrivate<HashSet<SpriteComponent>>(CDragDropSys, "_nextHighlightedSprites"), Is.Empty);
            });
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var probe = SEntMan.GetComponent<DragDropReplayProbeComponent>(ToServer(cultist));
            Assert.That(probe.Interactions, Is.EqualTo(1),
                "a quick mouse-up must replay the captured network interaction exactly once on the target");
        });
    }

    [Test]
    public async Task HighlightCleanupRemovesOnlyDragShaderAndLeavesNoStaleCandidates()
    {
        var first = ToClient(await SpawnTarget("Paper"));
        var second = ToClient(await Spawn("Paper"));

        await Client.WaitAssertion(() =>
        {
            var sprites = CEntMan.System<SpriteSystem>();
            var unrelated = Client.ProtoMan.Index<ShaderPrototype>("SelectionOutline").InstanceUnique();
            var drag = GetPrivate<ShaderInstance>(CDragDropSys, "_dropTargetInRangeShader");
            var highlighted = GetPrivate<HashSet<SpriteComponent>>(CDragDropSys, "_highlightedSprites");
            var next = GetPrivate<HashSet<SpriteComponent>>(CDragDropSys, "_nextHighlightedSprites");
            var firstSprite = CEntMan.GetComponent<SpriteComponent>(first);
            var secondSprite = CEntMan.GetComponent<SpriteComponent>(second);

            sprites.SetPostShader((first, firstSprite),
                new SpriteComponent.PostShaderArgs("merge-unrelated", unrelated));
            SetDragShader(first, firstSprite, drag);
            SetDragShader(second, secondSprite, drag);
            firstSprite.RenderOrder = 100;
            secondSprite.RenderOrder = 200;
            highlighted.Add(firstSprite);
            highlighted.Add(secondSprite);
            next.Add(firstSprite);

            Assert.Multiple(() =>
            {
                Assert.That(sprites.TryGetPostShader(
                    (first, firstSprite), ContentPostShaderIds.DragDropOutline, out _), Is.True);
                Assert.That(sprites.TryGetPostShader((first, firstSprite), "merge-unrelated", out _), Is.True);
                Assert.That(highlighted, Has.Count.EqualTo(2));
                Assert.That(next, Has.Count.EqualTo(1));
            });

            InvokePrivate(CDragDropSys, "RemoveHighlights");
            Assert.Multiple(() =>
            {
                Assert.That(sprites.TryGetPostShader(
                    (first, firstSprite), ContentPostShaderIds.DragDropOutline, out _), Is.False);
                Assert.That(sprites.TryGetPostShader(
                    (second, secondSprite), ContentPostShaderIds.DragDropOutline, out _), Is.False);
                Assert.That(sprites.TryGetPostShader((first, firstSprite), "merge-unrelated", out _), Is.True,
                    "DragDrop cleanup must not clobber unrelated keyed post shaders");
                Assert.That(firstSprite.RenderOrder, Is.Zero);
                Assert.That(secondSprite.RenderOrder, Is.Zero);
                Assert.That(highlighted, Is.Empty);
                Assert.That(next, Is.Empty,
                    "the reusable candidate highlight sets must not retain stale targets between rechecks");
            });
        });
    }

    private async Task MakeDead(NetEntity target)
    {
        await Server.WaitPost(() =>
            Server.System<MobStateSystem>().ChangeMobState(ToServer(target), MobState.Dead));
        await RunTicks(5);
    }

    private void SetDragShader(EntityUid uid, SpriteComponent sprite, ShaderInstance shader)
    {
        typeof(DragDropSystem)
            .GetMethod("SetDragDropPostShader", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(CDragDropSys, new object[] { new Entity<SpriteComponent?>(uid, sprite), shader });
    }

    private static T GetPrivate<T>(object instance, string field)
    {
        return (T) instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private static void InvokePrivate(object instance, string method)
    {
        instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, null);
    }
}

[RegisterComponent]
public sealed partial class DragDropReplayProbeComponent : Component
{
    public int Interactions;
}

public sealed partial class DragDropReplayProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DragDropReplayProbeComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(
        Entity<DragDropReplayProbeComponent> ent,
        ref InteractHandEvent args)
    {
        ent.Comp.Interactions++;
    }
}
