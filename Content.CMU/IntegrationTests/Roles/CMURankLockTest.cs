using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.CMU14.Roles.Ranks;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Roles;

[TestFixture]
[TestOf(typeof(CMURankLockSystem))]
public sealed class CMURankLockTest
{
    private const string Console = "AU14CorporateConsole";
    private const string Dummy = "CMURankLockTestDummy";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMURankLockTestDummy
  components:
  - type: CMURankLock
    allowedRanks:
    - RMCRankWeYaExecutiveSpecialist
";

    // The corporate console opens for Executive Specialist (WYC5) and up.
    [TestCase("RMCRankWeYaTrainee", false)]
    [TestCase("RMCRankWeYaSeniorExecutive", false)]
    [TestCase("CMURankWeYuLegalSupervisor", false)]
    [TestCase("RMCRankWeYaExecutiveSpecialist", true)]
    [TestCase("RMCRankWeYaExecutiveSupervisor", true)]
    [TestCase("RMCRankWeYaDivisionManager", true)]
    [TestCase("RMCRankWeYaChiefExecutive", true)]
    public async Task CorporateConsoleOpensForWYC5AndAbove(string rank, bool allowed)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var console = entities.SpawnEntity(Console, map.GridCoords);
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                server.System<SharedRankSystem>().SetRank(user, rank);

                Assert.That(server.System<CMURankLockSystem>().HasAccess(console, user), Is.EqualTo(allowed));
            }
            finally
            {
                entities.DeleteEntity(console);
                entities.DeleteEntity(user);
            }
        });
        await pair.CleanReturnAsync();
    }

    // The console has other open checks (power, ID access), so the lock's own cancel is tested in isolation.
    [TestCase("RMCRankWeYaSeniorExecutive", true)]
    [TestCase("RMCRankWeYaExecutiveSpecialist", false)]
    public async Task OpenAttemptIsCancelledBelowRank(string rank, bool cancelled)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var dummy = entities.SpawnEntity(Dummy, map.GridCoords);
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                server.System<SharedRankSystem>().SetRank(user, rank);
                var attempt = new ActivatableUIOpenAttemptEvent(user, true);
                entities.EventBus.RaiseLocalEvent(dummy, attempt);
                Assert.That(attempt.Cancelled, Is.EqualTo(cancelled));
            }
            finally
            {
                entities.DeleteEntity(dummy);
                entities.DeleteEntity(user);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UserWithoutRankIsDenied()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var console = entities.SpawnEntity(Console, map.GridCoords);
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                server.System<SharedRankSystem>().ClearRank(user);
                Assert.That(server.System<CMURankLockSystem>().HasAccess(console, user), Is.False);
            }
            finally
            {
                entities.DeleteEntity(console);
                entities.DeleteEntity(user);
            }
        });
        await pair.CleanReturnAsync();
    }
}
