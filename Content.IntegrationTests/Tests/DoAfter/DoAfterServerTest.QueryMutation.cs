using Content.Shared.DoAfter;

namespace Content.IntegrationTests.Tests.DoAfter;

public sealed partial class DoAfterServerTest
{
    [Test]
    public async Task CallbackCanStartDoAfterOnAnotherEntity()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var first = SEntMan.SpawnEntity("DoAfterDummy", map.GridCoords);
            var second = SEntMan.SpawnEntity("DoAfterDummy", map.GridCoords);
            try
            {
                var system = Server.System<SharedDoAfterSystem>();
                var armed = false;
                var started = false;
                var secondChecks = 0;
                var secondArgs = new DoAfterArgs(SEntMan, second, TimeSpan.FromSeconds(10), new TestDoAfterEvent(), null)
                {
                    Broadcast = true,
                    AttemptFrequency = AttemptFrequency.EveryTick,
                    ExtraCheck = () =>
                    {
                        if (started)
                            secondChecks++;
                        return true;
                    },
                };
                var firstArgs = new DoAfterArgs(SEntMan, first, TimeSpan.FromSeconds(10), new TestDoAfterEvent(), null)
                {
                    Broadcast = true,
                    AttemptFrequency = AttemptFrequency.EveryTick,
                    ExtraCheck = () =>
                    {
                        if (armed && !started)
                        {
                            Assert.That(system.TryStartDoAfter(secondArgs), Is.True);
                            started = true;
                        }
                        return true;
                    },
                };
                Assert.That(system.TryStartDoAfter(firstArgs), Is.True);
                armed = true;
                Assert.DoesNotThrow(() => system.Update(0));
                Assert.That(started, Is.True);
                Assert.That(SEntMan.HasComponent<ActiveDoAfterComponent>(second), Is.True);
                Assert.That(secondChecks, Is.Zero, "New users enter the update on the following tick.");
                system.Update(0);
                Assert.That(secondChecks, Is.EqualTo(1));
            }
            finally
            {
                SEntMan.DeleteEntity(first);
                SEntMan.DeleteEntity(second);
            }
        });
        await Pair.DeleteEntityTreeLeafFirst(map.Grid);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CallbackCanRemoveAnotherPendingUser(bool deleteEntity)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var users = new[]
            {
                SEntMan.SpawnEntity("DoAfterDummy", map.GridCoords),
                SEntMan.SpawnEntity("DoAfterDummy", map.GridCoords),
            };
            try
            {
                var system = Server.System<SharedDoAfterSystem>();
                var armed = false;
                var checks = 0;
                foreach (var user in users)
                {
                    var args = new DoAfterArgs(SEntMan, user, TimeSpan.FromSeconds(10), new TestDoAfterEvent(), null)
                    {
                        Broadcast = true,
                        AttemptFrequency = AttemptFrequency.EveryTick,
                        ExtraCheck = () =>
                        {
                            if (!armed)
                                return true;

                            checks++;
                            var other = users.Single(uid => uid != user);
                            if (deleteEntity)
                                SEntMan.DeleteEntity(other);
                            else
                                SEntMan.RemoveComponent<DoAfterComponent>(other);
                            return true;
                        },
                    };
                    Assert.That(system.TryStartDoAfter(args), Is.True);
                }

                armed = true;
                Assert.DoesNotThrow(() => system.Update(0));
                Assert.That(checks, Is.EqualTo(1), "A removed user must be skipped even if it was already queued.");
            }
            finally
            {
                foreach (var user in users)
                {
                    if (SEntMan.EntityExists(user))
                        SEntMan.DeleteEntity(user);
                }
            }
        });
        await Pair.DeleteEntityTreeLeafFirst(map.Grid);
    }
}
