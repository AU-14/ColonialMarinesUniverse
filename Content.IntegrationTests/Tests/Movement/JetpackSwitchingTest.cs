using Content.Server.Movement.Systems;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Movement;

[TestFixture]
[TestOf(typeof(JetpackSystem))]
public sealed class JetpackSwitchingTest
{
    [Test]
    public async Task EnablingSecondJetpackDisablesFirst()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var system = server.System<JetpackSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(null, map.GridCoords);
            var first = entMan.SpawnEntity("JetpackBlueFilled", map.GridCoords);
            var second = entMan.SpawnEntity("JetpackBlueFilled", map.GridCoords);
            var firstComponent = entMan.GetComponent<JetpackComponent>(first);
            var secondComponent = entMan.GetComponent<JetpackComponent>(second);

            system.SetEnabled(first, firstComponent, true, user);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<ActiveJetpackComponent>(first), Is.True);
                Assert.That(firstComponent.JetpackUser, Is.EqualTo(user));
                Assert.That(
                    entMan.GetComponent<JetpackUserComponent>(user).Jetpack,
                    Is.EqualTo(first));
            });

            system.SetEnabled(second, secondComponent, true, user);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<ActiveJetpackComponent>(first), Is.False);
                Assert.That(firstComponent.JetpackUser, Is.Null);
                Assert.That(entMan.HasComponent<ActiveJetpackComponent>(second), Is.True);
                Assert.That(secondComponent.JetpackUser, Is.EqualTo(user));
                Assert.That(
                    entMan.GetComponent<JetpackUserComponent>(user).Jetpack,
                    Is.EqualTo(second));
            });
        });

        await pair.CleanReturnAsync();
    }
}
