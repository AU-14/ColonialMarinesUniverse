using Content.Shared.DoAfter;

namespace Content.IntegrationTests.Tests.DoAfter;

public sealed partial class DoAfterServerTest
{
    [Test]
    public async Task InstantDoAfterCanBeCancelledByItsCompletionHandler()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("DoAfterDummy", map.GridCoords);
            var system = SEntMan.System<SharedDoAfterSystem>();
            var ev = new TestDoAfterEvent();
            Assert.That(system.TryStartDoAfter(new DoAfterArgs(SEntMan, user, TimeSpan.Zero, ev, null)
                { Broadcast = true }), Is.True);
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(user).DoAfters, Is.Empty);
            Assert.DoesNotThrow(() => system.Cancel(ev.DoAfter, force: true));
            Assert.That(ev.Cancelled, Is.True);
            SEntMan.DeleteEntity(user);
        });
    }

    [Test]
    public async Task DeletedMovementEntityCancelsDoAfter()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        var completed = new TestDoAfterEvent();
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("DoAfterDummy", map.GridCoords);
            var movement = SEntMan.SpawnEntity(null, map.GridCoords);
            var system = SEntMan.System<SharedDoAfterSystem>();
            var args = new DoAfterArgs(SEntMan, user, TimeSpan.FromSeconds(10), completed, null)
            {
                Broadcast = true,
                BreakOnMove = true,
            };
            Assert.That(system.TryStartDoAfter(args), Is.True);
            var doAfter = SEntMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single();
#pragma warning disable RA0002 // Reproduce a stored effective mover being deleted independently of the user.
            doAfter.MovementEntity = movement;
#pragma warning restore RA0002
            SEntMan.DeleteEntity(movement);
        });

        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(completed.Cancelled, Is.True);
            SEntMan.DeleteEntity(user);
        });
    }
}
