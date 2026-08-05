using Content.Server.Communications;
using Content.Shared.Communications;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Communications;

[TestFixture]
[TestOf(typeof(CommunicationsConsoleSystem))]
public sealed class CommunicationsConsoleInitialCooldownTest
{
    [Test]
    public async Task InitialUiStateCannotAnnounce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var console = server.EntMan.SpawnEntity("ComputerComms", map.GridCoords);
            var component = server.EntMan.GetComponent<CommunicationsConsoleComponent>(console);
            var ui = server.System<UserInterfaceSystem>();

            Assert.That(ui.TryGetUiState<CommunicationsConsoleInterfaceState>(
                console,
                CommunicationsConsoleUiKey.Key,
                out var state));

            Assert.Multiple(() =>
            {
                Assert.That(component.InitialDelay, Is.GreaterThan(0));
                Assert.That(component.AnnouncementCooldownRemaining, Is.EqualTo(component.InitialDelay));
                Assert.That(state!.CanAnnounce, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
