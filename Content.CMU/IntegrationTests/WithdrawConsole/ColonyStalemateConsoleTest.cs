using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.WithdrawConsole;
using Content.Shared.CMU14.WithdrawConsole;

namespace Content.IntegrationTests.CMU14.WithdrawConsole;

public sealed class ColonyStalemateConsoleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Destructive = true };

    [Test]
    public async Task ExpiredColonyWithdrawalCannotEndTheRound()
    {
        var map = await Pair.CreateTestMap();
        EntityUid colony = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<WithdrawConsoleSystem>().IsProtectedWithdrawalAllowed(), Is.True);
            colony = SSpawnAtPosition("AU14WithdrawConsoleColony", map.GridCoords);
            var console = SComp<WithdrawConsoleComponent>(colony);
            // Old saves may restore a colony countdown that has already expired.
            console.WithdrawActive = true;
            console.WithdrawStartTime = SGameTiming.CurTime - console.WithdrawDuration;
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var console = SComp<WithdrawConsoleComponent>(colony);
            Assert.That(console.RoundEndTriggered, Is.False);
        });
    }
}
