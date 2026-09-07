#nullable enable

using System.Linq;
using Content.Client.Players.PlayTimeTracking;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RMC14.PlayTimeTracking;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CMU14.PlayTimeTracking;
using Content.Shared._RMC14.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Players.JobWhitelist;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(PlayTimeTrackingManager))]
[TestOf(typeof(PlayTimeTrackingSystem))]
[TestOf(typeof(JobRequirementsManager))]
[TestOf(typeof(CMUThreatPlayTimeCompatibility))]
public sealed class PlayTimeMergeRegressionTest : GameTest
{
    private const string RequiredJob = "PlayTimeMergeRequiredJob";
    private const string WhitelistRoot = "PlayTimeMergeWhitelistRoot";
    private const string WhitelistChild = "PlayTimeMergeWhitelistChild";
    private const string WhitelistGrandchild = "PlayTimeMergeWhitelistGrandchild";
    private const string RequiredAntag = "PlayTimeMergeRequiredAntag";
    private const string AdminJob = "PlayTimeMergeAdminJob";
    private const string AdminJobTracker = "PlayTimeMergeAdminJobTracker";
    private const string XenoOneTracker = "PlayTimeMergeXenoOne";
    private const string XenoTwoTracker = "PlayTimeMergeXenoTwo";
    private const string NonXenoTracker = "PlayTimeMergeNonXeno";

    private const string CanonicalMilitaryPoliceTracker = "AU14JobGOVFORMilitaryPoliceMan";
    private const string LegacyMilitaryPoliceTracker = "AU14JobMilitaryPolice";
    private const string LegacyMilitaryPoliceTrackerSuffix = "AU14JobMilitaryPoliceTracker";

    [TestPrototypes]
    private const string Prototypes = @"
- type: playTimeTracker
  id: PlayTimeMergeRequiredTracker

- type: playTimeTracker
  id: PlayTimeMergeAdminJobTracker

- type: playTimeTracker
  id: PlayTimeMergeXenoOne
  isXeno: true

- type: playTimeTracker
  id: PlayTimeMergeXenoTwo
  isXeno: true

- type: playTimeTracker
  id: PlayTimeMergeNonXeno

- type: job
  parent: Passenger
  id: PlayTimeMergeRequiredJob
  playTimeTracker: PlayTimeMergeRequiredTracker
  requirements:
  - !type:OverallPlaytimeRequirement
    time: 1h

- type: job
  parent: Passenger
  id: PlayTimeMergeWhitelistRoot
  playTimeTracker: PlayTimeMergeRequiredTracker
  whitelisted: true

- type: job
  parent: PlayTimeMergeWhitelistRoot
  id: PlayTimeMergeWhitelistChild
  whitelistParent: PlayTimeMergeWhitelistRoot

- type: job
  parent: PlayTimeMergeWhitelistChild
  id: PlayTimeMergeWhitelistGrandchild
  whitelistParent: PlayTimeMergeWhitelistChild
  requirements:
  - !type:OverallPlaytimeRequirement
    time: 1h

- type: job
  parent: Passenger
  id: PlayTimeMergeAdminJob
  playTimeTracker: PlayTimeMergeAdminJobTracker

- type: antag
  id: PlayTimeMergeRequiredAntag
  name: PlayTimeMergeRequiredAntag
  objective: PlayTimeMergeRequiredAntag
  requirements:
  - !type:OverallPlaytimeRequirement
    time: 1h
";

    [SidedDependency(Side.Server)]
    private readonly IServerDbManager _database = default!;

    [SidedDependency(Side.Server)]
    private readonly UserDbDataManager _userDatabase = default!;

    [SidedDependency(Side.Server)]
    private readonly PlayTimeTrackingManager _tracking = default!;

    [SidedDependency(Side.Server)]
    private readonly RMCPlayTimeManager _rmcTracking = default!;

    [SidedDependency(Side.Server)]
    private readonly IPlayerManager _serverPlayers = default!;

    [SidedDependency(Side.Server)]
    private readonly IServerNetManager _serverNet = default!;

    [SidedDependency(Side.Server)]
    private readonly IConfigurationManager _serverConfiguration = default!;

    [SidedDependency(Side.Client)]
    private readonly IConfigurationManager _clientConfiguration = default!;

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    [Test]
    public async Task LegacyRowsCanonicalizeAggregateAndPersistWithoutDoubleCounting()
    {
        var initialSession = _serverPlayers.Sessions.Single();
        var userId = initialSession.UserId;
        var userName = initialSession.Name;
        var clientNet = Client.ResolveDependency<IClientNetManager>();

        await Client.WaitPost(() => clientNet.ClientDisconnect("Reloading seeded play-time rows."));
        await RunTicksSync(5);

        Assert.That(_serverPlayers.Sessions, Is.Empty);

        await _database.UpdatePlayTimes([
            new PlayTimeUpdate(userId, CanonicalMilitaryPoliceTracker, TimeSpan.FromMinutes(4)),
            new PlayTimeUpdate(userId, LegacyMilitaryPoliceTracker, TimeSpan.FromMinutes(2)),
            new PlayTimeUpdate(userId, LegacyMilitaryPoliceTrackerSuffix, TimeSpan.FromMinutes(3)),
        ]);

        await Task.WhenAll(Client.WaitIdleAsync(), Server.WaitIdleAsync());
        Client.SetConnectTarget(Server);
        await Client.WaitPost(() => clientNet.ClientConnect(null!, 0, userName));
        await RunTicksSync(10);

        var reconnected = _serverPlayers.Sessions.Single();
        Assert.That(reconnected.UserId, Is.EqualTo(userId));
        await _userDatabase.WaitLoadComplete(reconnected);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_tracking.TryGetTrackerTimes(reconnected, out var times), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(times![CanonicalMilitaryPoliceTracker], Is.EqualTo(TimeSpan.FromMinutes(9)));
                Assert.That(times[LegacyMilitaryPoliceTracker], Is.EqualTo(TimeSpan.Zero));
                Assert.That(times[LegacyMilitaryPoliceTrackerSuffix], Is.EqualTo(TimeSpan.Zero));
                Assert.That(_tracking.GetPlayTimeForTracker(reconnected, LegacyMilitaryPoliceTracker),
                    Is.EqualTo(TimeSpan.FromMinutes(9)));
            });

            _tracking.SaveSession(reconnected);
        });

        await RunTicksSync(5);
        var persisted = await _database.GetPlayTimes(userId);

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Single(x => x.Tracker == CanonicalMilitaryPoliceTracker).TimeSpent,
                Is.EqualTo(TimeSpan.FromMinutes(9)));
            Assert.That(persisted.Single(x => x.Tracker == LegacyMilitaryPoliceTracker).TimeSpent,
                Is.EqualTo(TimeSpan.Zero));
            Assert.That(persisted.Single(x => x.Tracker == LegacyMilitaryPoliceTrackerSuffix).TimeSpent,
                Is.EqualTo(TimeSpan.Zero));
        });
    }

    [Test]
    public async Task ThreatMemberCompatibilityAggregatesEveryPositiveXenoTrackerOnly()
    {
        await Server.WaitAssertion(() =>
        {
            var source = new Dictionary<string, TimeSpan>
            {
                [CMUThreatPlayTimeCompatibility.ThreatMemberTracker.Id] = TimeSpan.FromMinutes(5),
                [XenoOneTracker] = TimeSpan.FromMinutes(11),
                [XenoTwoTracker] = TimeSpan.FromMinutes(7),
                [NonXenoTracker] = TimeSpan.FromMinutes(13),
                ["PlayTimeMergeUnknown"] = TimeSpan.FromMinutes(17),
            };

            var compatible = CMUThreatPlayTimeCompatibility.GetCompatibleTrackerTimes(source, SProtoMan);

            Assert.Multiple(() =>
            {
                Assert.That(compatible[CMUThreatPlayTimeCompatibility.ThreatMemberTracker.Id],
                    Is.EqualTo(TimeSpan.FromMinutes(23)));
                Assert.That(compatible[XenoOneTracker], Is.EqualTo(TimeSpan.FromMinutes(11)));
                Assert.That(compatible[XenoTwoTracker], Is.EqualTo(TimeSpan.FromMinutes(7)));
                Assert.That(compatible[NonXenoTracker], Is.EqualTo(TimeSpan.FromMinutes(13)));
                Assert.That(source[CMUThreatPlayTimeCompatibility.ThreatMemberTracker.Id],
                    Is.EqualTo(TimeSpan.FromMinutes(5)), "The compatibility view must not mutate stored rows.");
            });

            source[XenoOneTracker] = TimeSpan.Zero;
            source[XenoTwoTracker] = TimeSpan.FromMinutes(-1);
            compatible = CMUThreatPlayTimeCompatibility.GetCompatibleTrackerTimes(source, SProtoMan);
            Assert.That(compatible[CMUThreatPlayTimeCompatibility.ThreatMemberTracker.Id],
                Is.EqualTo(TimeSpan.FromMinutes(5)), "Zero and negative xeno rows must not contribute.");
        });
    }

    [Test]
    public async Task RecursiveWhitelistAndRmcExclusionPreserveBanAndWhitelistOrdering()
    {
        var session = _serverPlayers.Sessions.Single();
        var clientRequirements = Client.ResolveDependency<JobRequirementsManager>();

        await Server.WaitPost(() =>
        {
            _serverConfiguration.SetCVar(CCVars.GameRoleTimers, true);
            _serverConfiguration.SetCVar(CCVars.GameRoleWhitelist, true);
        });
        await RunTicksSync(5);

        Assert.That(await _rmcTracking.Exclude(session.UserId, WhitelistGrandchild), Is.True);
        await Server.WaitPost(() =>
        {
            SendClientRoleState(session, [], []);
            SendClientWhitelist(session, []);
        });
        await RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var job = CProtoMan.Index<JobPrototype>(WhitelistGrandchild);
            Assert.Multiple(() =>
            {
                Assert.That(clientRequirements.IsWhitelisted(WhitelistGrandchild), Is.False);
                Assert.That(clientRequirements.IsAllowed(job, null, out _), Is.False,
                    "An RMC timer exclusion must not bypass a missing whitelist.");
            });
        });

        await Server.WaitPost(() => SendClientWhitelist(session, [WhitelistRoot]));
        await RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var job = CProtoMan.Index<JobPrototype>(WhitelistGrandchild);
            Assert.Multiple(() =>
            {
                Assert.That(clientRequirements.IsWhitelisted(WhitelistGrandchild), Is.True,
                    "WhitelistParent must be followed recursively through the child.");
                Assert.That(clientRequirements.IsAllowed(job, null, out _), Is.True,
                    "Once whitelisted, the RMC exclusion bypasses only the unmet timer.");
            });
        });

        await Server.WaitPost(() => SendClientRoleState(session, [WhitelistGrandchild], []));
        await RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var job = CProtoMan.Index<JobPrototype>(WhitelistGrandchild);
            Assert.That(clientRequirements.IsAllowed(job, null, out _), Is.False,
                "An RMC timer exclusion must not bypass a role ban.");
        });
    }

    [Test]
    public async Task ServerJobExclusionDoesNotAffectAntagRequirementsAndFiltersDisallowedJobs()
    {
        var session = _serverPlayers.Sessions.Single();
        var system = Server.System<PlayTimeTrackingSystem>();

        await Server.WaitPost(() => _serverConfiguration.SetCVar(CCVars.GameRoleTimers, true));

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(system.IsAllowed(session, (ProtoId<JobPrototype>) RequiredJob), Is.False);
                Assert.That(system.IsAllowed(session, (ProtoId<AntagPrototype>) RequiredAntag), Is.False);
                Assert.That(system.GetDisallowedJobs(session), Does.Contain((ProtoId<JobPrototype>) RequiredJob));
            });
        });

        Assert.That(await _rmcTracking.Exclude(session.UserId, RequiredJob), Is.True);
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(system.IsAllowed(session, (ProtoId<JobPrototype>) RequiredJob), Is.True,
                    "RMC exclusions bypass job timer requirements.");
                Assert.That(system.IsAllowed(session, (ProtoId<AntagPrototype>) RequiredAntag), Is.False,
                    "RMC job exclusions do not bypass upstream antagonist requirements.");
                Assert.That(system.GetDisallowedJobs(session), Does.Not.Contain((ProtoId<JobPrototype>) RequiredJob));
            });
        });
    }

    [Test]
    public async Task ExplicitRequirementsOverridePrototypeTimersButNotBansOrWhitelists()
    {
        var session = _serverPlayers.Sessions.Single();
        var clientRequirements = Client.ResolveDependency<JobRequirementsManager>();
        var emptyOverride = new HashSet<JobRequirement>();

        await Server.WaitPost(() =>
        {
            _serverConfiguration.SetCVar(CCVars.GameRoleTimers, true);
            _serverConfiguration.SetCVar(CCVars.GameRoleWhitelist, true);
            SendClientRoleState(session, [], []);
            SendClientWhitelist(session, []);
        });
        await RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(clientRequirements.IsAllowed(
                    null,
                    [(ProtoId<AntagPrototype>) RequiredAntag],
                    null,
                    null,
                    out _), Is.False, "A null override must retain the antagonist prototype timer fallback.");
                Assert.That(clientRequirements.IsAllowed(
                    null,
                    [(ProtoId<AntagPrototype>) RequiredAntag],
                    emptyOverride,
                    null,
                    out _), Is.True, "An explicit empty set must replace the prototype timer requirements.");
                Assert.That(clientRequirements.IsAllowed(
                    [(ProtoId<JobPrototype>) WhitelistRoot],
                    null,
                    emptyOverride,
                    null,
                    out _), Is.False, "A requirement override must not bypass job whitelists.");
            });
        });

        await Server.WaitPost(() => SendClientRoleState(
            session,
            [],
            [(ProtoId<AntagPrototype>) RequiredAntag]));
        await RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(clientRequirements.IsAllowed(
                null,
                [(ProtoId<AntagPrototype>) RequiredAntag],
                emptyOverride,
                null,
                out _), Is.False, "A requirement override must not bypass role bans.");
        });
    }

    [Test]
    public async Task AdminJobTrackingCVarControlsWhetherTheJobTrackerIsActive()
    {
        var session = _serverPlayers.Sessions.Single();
        var admin = Server.ResolveDependency<IAdminManager>();
        var mindSystem = Server.System<SharedMindSystem>();
        var roleSystem = Server.System<SharedRoleSystem>();

        await Server.WaitAssertion(() =>
        {
            var mind = mindSystem.GetMind(session.AttachedEntity!.Value);
            Assert.That(mind, Is.Not.Null);
            roleSystem.MindAddJobRole(mind!.Value, jobPrototype: AdminJob);
            admin.PromoteHost(session);
            _serverConfiguration.SetCVar(CCVars.GameAdminJobTracking, false);
            _tracking.QueueRefreshTrackers(session);
        });

        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(admin.IsAdmin(session), Is.True);
            _tracking.FlushTracker(session);
            Assert.That(_tracking.TryGetTrackerTimes(session, out var times), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(times, Does.ContainKey(PlayTimeTrackingShared.TrackerAdmin.Id));
                Assert.That(times, Does.ContainKey(PlayTimeTrackingShared.TrackerOverall.Id));
                Assert.That(times, Does.Not.ContainKey(AdminJobTracker),
                    "Admins must not accrue their job tracker while admin job tracking is disabled.");
            });

            _serverConfiguration.SetCVar(CCVars.GameAdminJobTracking, true);
            _tracking.QueueRefreshTrackers(session);
        });

        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            _tracking.FlushTracker(session);
            Assert.That(_tracking.TryGetTrackerTimes(session, out var times), Is.True);
            Assert.That(times, Does.ContainKey(AdminJobTracker));
        });
    }

    private void SendClientWhitelist(ICommonSession session, HashSet<string> whitelist)
    {
        _serverNet.ServerSendMessage(new MsgJobWhitelist
        {
            Whitelist = whitelist,
        }, session.Channel);
    }

    private void SendClientRoleState(
        ICommonSession session,
        List<ProtoId<JobPrototype>> jobBans,
        List<ProtoId<AntagPrototype>> antagBans)
    {
        _serverNet.ServerSendMessage(new MsgRoleBans
        {
            JobBans = jobBans,
            AntagBans = antagBans,
        }, session.Channel);
    }
}
