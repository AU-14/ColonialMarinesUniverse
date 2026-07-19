using System.Numerics;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Hands;

[TestFixture]
[TestOf(typeof(SharedHandsSystem))]
public sealed class DropEventOrderingTest
{
    private static readonly Angle DropRotation = Angle.FromDegrees(90);

    [Test]
    public async Task DroppedEventObservesFinalPositionAndRotation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var handsSystem = server.System<SharedHandsSystem>();
        var transformSystem = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(null, map.GridCoords);
            var hands = entMan.EnsureComponent<HandsComponent>(user);
            handsSystem.AddHand((user, hands), "hand", HandLocation.Left);
            entMan.EnsureComponent<InputMoverComponent>(user).TargetRelativeRotation = DropRotation;

            var item = entMan.SpawnEntity(null, map.GridCoords);
            entMan.EnsureComponent<ItemComponent>(item);
            var snapshot = entMan.EnsureComponent<DropEventSnapshotComponent>(item);
            Assert.That(
                handsSystem.TryPickup(
                    user,
                    item,
                    "hand",
                    checkActionBlocker: false,
                    animate: false,
                    handsComp: hands),
                Is.True);

            var target = map.GridCoords.Offset(Vector2.UnitX);
            var expectedCoordinates = transformSystem.ToMapCoordinates(target);
            Assert.That(
                handsSystem.TryDrop(
                    (user, hands),
                    "hand",
                    target,
                    checkActionBlocker: false),
                Is.True);

            var itemTransform = entMan.GetComponent<TransformComponent>(item);
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Count, Is.EqualTo(1));
                Assert.That(snapshot.User, Is.EqualTo(user));
                Assert.That(snapshot.Coordinates, Is.EqualTo(expectedCoordinates));
                Assert.That(snapshot.Rotation, Is.EqualTo(DropRotation));
                Assert.That(
                    transformSystem.GetMapCoordinates(item, xform: itemTransform),
                    Is.EqualTo(expectedCoordinates));
                Assert.That(itemTransform.LocalRotation, Is.EqualTo(DropRotation));
            });
        });

        await pair.CleanReturnAsync();
    }

    private sealed class DropEventSnapshotSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DropEventSnapshotComponent, DroppedEvent>(OnDropped);
        }

        private void OnDropped(Entity<DropEventSnapshotComponent> ent, ref DroppedEvent args)
        {
            var transform = Transform(ent);
            ent.Comp.Count++;
            ent.Comp.User = args.User;
            ent.Comp.Coordinates = TransformSystem.GetMapCoordinates(ent, xform: transform);
            ent.Comp.Rotation = transform.LocalRotation;
        }
    }
}

// Components must be directly in the namespace for source generation.
[RegisterComponent]
public sealed partial class DropEventSnapshotComponent : Component
{
    public int Count;
    public EntityUid? User;
    public MapCoordinates? Coordinates;
    public Angle? Rotation;
}
