using Content.Server.CMU14.BalanceRating;
using Content.Shared.CMU14.BalanceRating;

namespace Content.IntegrationTests.CMU14.BalanceRating;

[TestFixture]
public sealed class CMUBalanceRatingEuiTest
{
    [Test]
    public async Task ConstructorResolvesServerDependencies()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            var eui = new CMUBalanceRatingEui();
            var state = eui.GetNewState();

            Assert.That(state, Is.TypeOf<CMUBalanceRatingEuiState>());

            var dashboard = ((CMUBalanceRatingEuiState) state).Dashboard;
            Assert.Multiple(() =>
            {
                Assert.That(dashboard.Entries, Is.Empty);
                Assert.That(dashboard.TotalPolls, Is.Zero);
                Assert.That(dashboard.TotalResponses, Is.Zero);
            });
        });

        await pair.CleanReturnAsync();
    }
}
