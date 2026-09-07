using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.Players.JobWhitelist;
using Content.Shared.CCVar;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(JobWhitelistManager))]
public sealed class JobWhitelistMergeRegressionTest : GameTest
{
    private static readonly ProtoId<JobPrototype> Root = "JobWhitelistMergeRoot";
    private static readonly ProtoId<JobPrototype> Child = "JobWhitelistMergeChild";
    private static readonly ProtoId<JobPrototype> Grandchild = "JobWhitelistMergeGrandchild";
    private static readonly ProtoId<JobPrototype> Unrelated = "JobWhitelistMergeUnrelated";
    private static readonly ProtoId<JobPrototype> CycleA = "JobWhitelistMergeCycleA";
    private static readonly ProtoId<JobPrototype> CycleB = "JobWhitelistMergeCycleB";
    private static readonly ProtoId<JobPrototype> PublicJob = "JobWhitelistMergePublic";

    private static readonly ProtoId<JobPrototype>[] MutableWhitelists =
    [
        Root,
        Child,
        Grandchild,
        Unrelated,
        CycleA,
        CycleB,
    ];

    [TestPrototypes]
    private const string Prototypes = @"
- type: job
  parent: Passenger
  id: JobWhitelistMergeRoot
  whitelisted: true

- type: job
  parent: JobWhitelistMergeRoot
  id: JobWhitelistMergeChild
  whitelistParent: JobWhitelistMergeRoot

- type: job
  parent: JobWhitelistMergeChild
  id: JobWhitelistMergeGrandchild
  whitelistParent: JobWhitelistMergeChild

- type: job
  parent: Passenger
  id: JobWhitelistMergeUnrelated
  whitelisted: true

- type: job
  parent: Passenger
  id: JobWhitelistMergeCycleA
  whitelisted: true
  whitelistParent: JobWhitelistMergeCycleB

- type: job
  parent: Passenger
  id: JobWhitelistMergeCycleB
  whitelisted: true
  whitelistParent: JobWhitelistMergeCycleA

- type: job
  parent: Passenger
  id: JobWhitelistMergePublic
  whitelisted: false
";

    [SidedDependency(Side.Server)]
    private readonly IServerDbManager _database = default!;

    [SidedDependency(Side.Server)]
    private readonly UserDbDataManager _userDatabase = default!;

    [SidedDependency(Side.Server)]
    private readonly JobWhitelistManager _whitelist = default!;

    [SidedDependency(Side.Server)]
    private readonly IPlayerManager _players = default!;

    [SidedDependency(Side.Server)]
    private readonly IConfigurationManager _configuration = default!;

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GameRoleWhitelist), true)]
    public async Task RecursiveParentGrantsUseLoadedDatabaseStateAndCyclesTerminate()
    {
        var session = _players.Sessions.Single();
        ProtoId<JobPrototype> missingJob = "JobWhitelistMergeMissing";
        await _userDatabase.WaitLoadComplete(session);

        try
        {
            foreach (var job in MutableWhitelists)
                await EnsureRemoved(session.UserId, job);

            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(_whitelist.IsAllowed(session, Root), Is.False);
                    Assert.That(_whitelist.IsAllowed(session, Child), Is.False);
                    Assert.That(_whitelist.IsAllowed(session, Grandchild), Is.False,
                        "a whitelisted grandchild must be denied when neither it nor an ancestor is granted");
                    Assert.That(_whitelist.IsAllowed(session, Unrelated), Is.False);
                    Assert.That(_whitelist.IsAllowed(session, CycleA), Is.False,
                        "a no-grant whitelist-parent cycle must terminate and deny");
                    Assert.That(_whitelist.IsAllowed(session, CycleB), Is.False);
                    Assert.That(_whitelist.IsAllowed(session, PublicJob), Is.True,
                        "a resolved non-whitelisted job remains public");
                    Assert.That(_whitelist.IsAllowed(session, missingJob), Is.True,
                        "a missing prototype must retain the permissive compatibility behavior");
                });
            });

            await Add(session.UserId, Child);
            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(_whitelist.IsAllowed(session, Root), Is.False,
                        "a descendant grant must not grant its ancestor");
                    Assert.That(_whitelist.IsAllowed(session, Child), Is.True,
                        "a concrete grant must authorize that job");
                    Assert.That(_whitelist.IsAllowed(session, Grandchild), Is.True,
                        "the grandchild must inherit its granted direct parent");
                    Assert.That(_whitelist.IsAllowed(session, Unrelated), Is.False);
                });
            });

            await Remove(session.UserId, Child);
            await Server.WaitAssertion(() =>
            {
                Assert.That(_whitelist.IsAllowed(session, Child), Is.False);
                Assert.That(_whitelist.IsAllowed(session, Grandchild), Is.False,
                    "removing the concrete grant must immediately deny its descendants again");
            });

            await Add(session.UserId, Root);
            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(_whitelist.IsAllowed(session, Root), Is.True);
                    Assert.That(_whitelist.IsAllowed(session, Child), Is.True);
                    Assert.That(_whitelist.IsAllowed(session, Grandchild), Is.True,
                        "a root grant must traverse the full three-level WhitelistParent chain");
                    Assert.That(_whitelist.IsAllowed(session, Unrelated), Is.False,
                        "the recursive grant must not leak to an unrelated whitelisted job");
                    Assert.That(_whitelist.IsAllowed(session, CycleA), Is.False);
                });
            });

            await Remove(session.UserId, Root);
            await Server.WaitAssertion(() =>
            {
                Assert.That(_whitelist.IsAllowed(session, Root), Is.False);
                Assert.That(_whitelist.IsAllowed(session, Grandchild), Is.False,
                    "removing the root grant must deny the complete chain again");
            });

            await Server.WaitPost(() => _configuration.SetCVar(CCVars.GameRoleWhitelist, false));
            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(_whitelist.IsAllowed(session, Grandchild), Is.True);
                    Assert.That(_whitelist.IsAllowed(session, Unrelated), Is.True);
                    Assert.That(_whitelist.IsAllowed(session, CycleA), Is.True,
                        "the disabled CVar must bypass traversal before entering a cycle");
                });
            });
            await Server.WaitPost(() => _configuration.SetCVar(CCVars.GameRoleWhitelist, true));
        }
        finally
        {
            await Server.WaitPost(() => _configuration.SetCVar(CCVars.GameRoleWhitelist, true));
            foreach (var job in MutableWhitelists)
                await EnsureRemoved(session.UserId, job);
        }
    }

    private async Task Add(NetUserId user, ProtoId<JobPrototype> job)
    {
        await Server.WaitPost(() => _whitelist.AddWhitelist(user, job));
        await WaitForDatabase(user, job, true);
        await Server.WaitAssertion(() => Assert.That(_whitelist.IsWhitelisted(user, job), Is.True));
    }

    private async Task Remove(NetUserId user, ProtoId<JobPrototype> job)
    {
        await Server.WaitPost(() => _whitelist.RemoveWhitelist(user, job));
        await WaitForDatabase(user, job, false);
        await Server.WaitAssertion(() => Assert.That(_whitelist.IsWhitelisted(user, job), Is.False));
    }

    private async Task EnsureRemoved(NetUserId user, ProtoId<JobPrototype> job)
    {
        // Always call the manager so a reused server cannot retain a stale in-memory grant
        // even when the persistent row was already removed by a previous dirty fixture.
        await Remove(user, job);
    }

    private async Task WaitForDatabase(NetUserId user, ProtoId<JobPrototype> job, bool expected)
    {
        await PoolManager.WaitUntil(
            Server,
            async () => (await _database.IsJobWhitelisted(user, job)) == expected,
            maxTicks: 120);
    }
}
