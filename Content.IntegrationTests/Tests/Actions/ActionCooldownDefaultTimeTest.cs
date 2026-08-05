#nullable enable

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Actions;

[TestFixture]
[TestOf(typeof(SharedActionsSystem))]
public sealed class ActionCooldownDefaultTimeTest
{
    [Test]
    public async Task OmittedTimeUsesCurrentGameTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var actions = server.System<SharedActionsSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var action = entMan.EnsureComponent<ActionComponent>(uid);
            var start = timing.CurTime;
            var end = start + TimeSpan.FromHours(1);

            actions.SetCooldown((uid, action), start, end);

            Assert.Multiple(() =>
            {
                Assert.That(actions.IsCooldownActive(action), Is.True);
                Assert.That(actions.IsCooldownActive(action, end - timing.TickPeriod), Is.True);
                Assert.That(actions.IsCooldownActive(action, end), Is.False);
            });

            actions.RemoveCooldown((uid, action));
            Assert.That(actions.IsCooldownActive(action), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
