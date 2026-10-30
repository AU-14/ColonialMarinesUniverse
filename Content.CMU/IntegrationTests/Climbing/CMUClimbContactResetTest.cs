using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Contacts;
using Robust.Shared.Physics.Systems;
using PhysicsSystem = Robust.Client.Physics.PhysicsSystem;

namespace Content.IntegrationTests.CMU14.Climbing;

public sealed class CMUClimbContactResetTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task LeavingTableCornerPreservesPendingContactsAndRechecksSupport(bool newSupport)
    {
        await Client.WaitAssertion(() =>
        {
            var map = CEntMan.System<SharedMapSystem>().CreateMap(runMapInit: true);
            var coordinates = new EntityCoordinates(map, Vector2.Zero);
            var table = CEntMan.SpawnEntity("Table", coordinates);
            var obstacle = CEntMan.SpawnEntity("Table", coordinates.Offset(new Vector2(0.85f, 0)));
            CEntMan.RemoveComponent<ClimbableComponent>(obstacle);
            var climber = CEntMan.SpawnEntity("MobHuman", coordinates);
            var fixtures = CComp<FixturesComponent>(climber);
            var masks = fixtures.Fixtures.ToDictionary(pair => pair.Key, pair => pair.Value.CollisionMask);
            var climbingSystem = CEntMan.System<ClimbSystem>();
            climbingSystem.Climb(climber, climber, table, silent: true);
            CEntMan.System<SharedPhysicsSystem>().WakeBody(climber);

            // Exercise the real prediction-reset contact batch. Its internal entry
            // point is needed to reproduce mutation between two pending contacts.
            var broadphase = CEntMan.System<SharedBroadphaseSystem>();
            var find = typeof(SharedBroadphaseSystem).GetMethod("FindNewContacts", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var reset = typeof(PhysicsSystem).GetMethod("UpdateIsTouching", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var physics = CEntMan.System<PhysicsSystem>();
            find.Invoke(broadphase, null);
            var contacts = (LinkedList<Contact>) typeof(PhysicsComponent)
                .GetField("Contacts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(CComp<PhysicsComponent>(climber))!;
            reset.Invoke(physics, new object[] { contacts.Where(c => c.FixtureAId == "climb" || c.FixtureBId == "climb").ToList() });

            // AABBs still overlap at this corner, but the circle no longer touches the table.
            CEntMan.System<SharedTransformSystem>().SetLocalPosition(climber, new Vector2(0.72f, 0.72f));
            find.Invoke(broadphase, null);
            var pending = contacts.Where(c => c.FixtureAId == "climb" || c.FixtureBId == "climb")
                .OrderBy(c => c.OtherEnt(climber) == table ? 0 : 1).ToList();
            Assert.That(pending.Count, Is.GreaterThanOrEqualTo(2));
            reset.Invoke(physics, new object[] { pending });
            Assert.That(pending.All(c => c.EntityA.IsValid() && c.EntityB.IsValid()), Is.True,
                "Ending one contact must not invalidate the rest of prediction reset's batch");

            // State can change between dispatch and deferred cleanup.
            if (newSupport)
                CEntMan.EnsureComponent<ClimbableComponent>(obstacle);
            climbingSystem.UpdateBeforeSolve(true, 0);
            Assert.That(CComp<ClimbingComponent>(climber).IsClimbing, Is.EqualTo(newSupport));
            Assert.That(fixtures.Fixtures.ContainsKey("climb"), Is.EqualTo(newSupport));
            if (!newSupport)
                foreach (var (key, mask) in masks)
                    Assert.That(fixtures.Fixtures[key].CollisionMask, Is.EqualTo(mask));
        });
    }
}
