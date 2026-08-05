using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Client.Administration.Systems;
using Content.Client.Administration.UI.CustomControls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Administration;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.Administration;

[TestFixture]
public sealed class PlayerListControlTest : GameTest
{
    [Test]
    public async Task DisposedControlDoesNotReceivePlayerListUpdates()
    {
        await Client.WaitAssertion(() =>
        {
            var admin = CEntMan.System<AdminSystem>();
            var control = new PlayerListControl();
            control.PopulateList([]);
            control.Dispose();

            var eventField = typeof(AdminSystem).GetField(
                nameof(AdminSystem.PlayerListChanged),
                BindingFlags.Instance | BindingFlags.NonPublic);
            var subscribers = eventField?.GetValue(admin) as Action<List<PlayerInfo>>;
            var player = new PlayerInfo(
                "TestUser",
                "Test Character",
                "Test Character",
                "TestJob",
                false,
                null,
                null,
                0,
                null,
                new NetUserId(Guid.NewGuid()),
                true,
                true,
                TimeSpan.Zero);

            Assert.That(eventField, Is.Not.Null);
            Assert.That(
                subscribers?.GetInvocationList().Any(handler => ReferenceEquals(handler.Target, control)) ?? false,
                Is.False);
            Assert.DoesNotThrow(() => subscribers?.Invoke([player]));
        });
    }
}
