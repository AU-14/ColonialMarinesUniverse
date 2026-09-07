using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Client.Movement.Systems;
using Content.Shared.Camera;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CameraAllocationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [Test]
    public async Task DetachedCamerasResumeUpdatingWhenTheyReturnToView()
    {
        await Client.WaitAssertion(() =>
        {
            var entMan = Client.EntMan;
            var holder = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var eye = entMan.AddComponent<EyeComponent>(holder);
            entMan.AddComponent<ContentEyeComponent>(holder);
            var metadata = entMan.System<MetaDataSystem>();
            var system = entMan.System<ContentEyeSystem>();
            var eyes = entMan.System<SharedEyeSystem>();
            var offset = new Vector2(3, 4);
            eyes.SetOffset(holder, offset, eye);
            metadata.AddFlag(holder, MetaDataFlags.Detached);
            system.Update(1f / 60);
            system.FrameUpdate(1f / 60);
            Assert.That(eye.Offset, Is.EqualTo(offset), "Detached history must not evaluate camera events");

            metadata.RemoveFlag(holder, MetaDataFlags.Detached);
            system.Update(1f / 60);
            Assert.That(eye.Offset, Is.EqualTo(Vector2.Zero), "Prediction resumes on re-entry");
            eyes.SetOffset(holder, offset, eye);
            system.FrameUpdate(1f / 60);
            Assert.That(eye.Offset, Is.EqualTo(Vector2.Zero), "Rendering also resumes on re-entry");
            entMan.DeleteEntity(holder);
        });
    }

    [Test]
    public async Task EmptyHandsDoNotAllocateForCameraRelays()
    {
        await Client.WaitAssertion(() =>
        {
            var entMan = Client.EntMan;
            var holder = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var hands = entMan.AddComponent<HandsComponent>(holder);
            var system = entMan.System<SharedHandsSystem>();
            system.AddHand((holder, hands), "left", HandLocation.Left);
            system.AddHand((holder, hands), "right", HandLocation.Right);
            var ev = new GetEyeOffsetRelayedEvent(new Vector2(1, 2));
            for (var i = 0; i < 1024; i++)
                entMan.EventBus.RaiseLocalEvent(holder, ref ev);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1024; i++)
                entMan.EventBus.RaiseLocalEvent(holder, ref ev);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Progress.WriteLine($"1,024 empty-hand camera relays allocated {allocated} bytes");

            Assert.That(ev.Offset, Is.EqualTo(new Vector2(1, 2)));
            Assert.That(allocated, Is.LessThanOrEqualTo(1024),
                "Empty hands must not allocate an iterator and relay wrapper for each camera update");
            entMan.DeleteEntity(holder);
        });
    }
}
