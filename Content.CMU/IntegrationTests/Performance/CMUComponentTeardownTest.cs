using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMUComponentTeardownTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task NestedDeletionAndComponentRemovalPreserveLifecycleOrder()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var outer = SEntMan.SpawnEntity(null, map.GridCoords);
            var child = SEntMan.SpawnEntity(null, new EntityCoordinates(outer, Vector2.Zero));
            var nested = SEntMan.SpawnEntity("SheetSteel", map.GridCoords);
            SEntMan.AddComponent<AppearanceComponent>(outer);
            SEntMan.AddComponent<PointLightComponent>(outer);
            SEntMan.AddComponent<PhysicsComponent>(outer);
            var removed = new List<Type>();
            var nestedDeleted = false;

            void OnRemoved(RemovedComponentEventArgs args)
            {
                if (args.BaseArgs.Owner != outer)
                    return;

                var component = args.BaseArgs.Component;
                removed.Add(component.GetType());
                if (component is AppearanceComponent)
                {
                    SEntMan.DeleteEntity(nested);
                    nestedDeleted = true;
                    SEntMan.RemoveComponent<PointLightComponent>(outer);
                }
                else if (component is PhysicsComponent)
                {
                    Assert.That(SEntMan.GetComponent<TransformComponent>(outer).ParentUid,
                        Is.EqualTo(EntityUid.Invalid));
                    Assert.That(args.Meta.EntityDeleted, Is.False,
                        "Metadata must remain alive while physics is removed.");
                }
            }

            SEntMan.ComponentRemoved += OnRemoved;
            try
            {
                SEntMan.DeleteEntity(outer);
                Assert.That(nestedDeleted, Is.True, "The nested teardown callback must have executed.");
                Assert.That(SEntMan.Deleted(child), Is.True, "Recursive children must be removed with the parent.");
                Assert.That(removed.Count(type => type == typeof(PointLightComponent)), Is.EqualTo(1),
                    "A component removed by a callback must not be disposed twice from the retained snapshot.");
                Assert.That(removed.TakeLast(3), Is.EqualTo(new[]
                {
                    typeof(PhysicsComponent), typeof(TransformComponent), typeof(MetaDataComponent),
                }));
            }
            finally
            {
                SEntMan.ComponentRemoved -= OnRemoved;
            }
        });
    }
}
