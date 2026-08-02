using System.Linq;
using Content.Client.Lobby;
using Content.Client.Players.PlayTimeTracking;
using Content.IntegrationTests.Pair;
using Content.Server.Database;
using Content.Server.Players.JobWhitelist;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using Content.Shared.CCVar;
using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaWhitelistAccessTest
{
    [Test]
    public async Task EveryYautjaWhitelistTypeAllowsHunterJobAndClientSelection()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
        var jobWhitelist = pair.Server.ResolveDependency<JobWhitelistManager>();
        var serverPlayers = pair.Server.ResolveDependency<IPlayerManager>();
        var clientNet = pair.Client.ResolveDependency<IClientNetManager>();
        var requirements = pair.Client.ResolveDependency<JobRequirementsManager>();
        var preferences = pair.Client.ResolveDependency<IClientPreferencesManager>();
        var hunter = prototypes.Index<JobPrototype>("CMUYautjaHunter");
        var playerId = pair.Player!.UserId.UserId;
        var username = pair.Player.Name;

        await pair.Server.WaitPost(() => pair.Server.CfgMan.SetCVar(CCVars.GameRoleWhitelist, true));

        var flags = new[]
        {
            YautjaWhitelistFlags.Yautja,
            YautjaWhitelistFlags.Legacy,
            YautjaWhitelistFlags.Council,
            YautjaWhitelistFlags.CouncilLegacy,
            YautjaWhitelistFlags.Leader,
        };

        foreach (var flag in flags)
        {
            await db.SetYautjaWhitelistFlagsAsync(playerId, (int) flag);
            await jobWhitelist.RefreshYautjaWhitelist(new NetUserId(playerId));
            await Reconnect(pair, clientNet, serverPlayers, username);

            await pair.Server.WaitAssertion(() =>
                Assert.That(jobWhitelist.IsAllowed(serverPlayers.Sessions.Single(), hunter.ID), Is.True));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(requirements.IsWhitelisted(hunter.ID), Is.True);
                Assert.That(requirements.CanCustomizeWhitelistedJob(hunter.ID), Is.True);
                Assert.That(
                    preferences.YautjaCapabilities.CanUseLegacy,
                    Is.EqualTo(flag is YautjaWhitelistFlags.Legacy or YautjaWhitelistFlags.CouncilLegacy));
            });
        }

        await db.SetYautjaWhitelistFlagsAsync(playerId, (int) YautjaWhitelistFlags.None);
        await jobWhitelist.RefreshYautjaWhitelist(new NetUserId(playerId));
        await Reconnect(pair, clientNet, serverPlayers, username);

        await pair.Server.WaitAssertion(() =>
            Assert.That(jobWhitelist.IsAllowed(serverPlayers.Sessions.Single(), hunter.ID), Is.False));
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(requirements.IsWhitelisted(hunter.ID), Is.False);
            Assert.That(requirements.CanCustomizeWhitelistedJob(hunter.ID), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClanAdminMenuWhitelistUpdateShowsClanlessTargetAndReachesClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
        var jobWhitelist = pair.Server.ResolveDependency<JobWhitelistManager>();
        var rankManager = pair.Server.ResolveDependency<YautjaRankManager>();
        var serverPlayers = pair.Server.ResolveDependency<IPlayerManager>();
        var clientNet = pair.Client.ResolveDependency<IClientNetManager>();
        var requirements = pair.Client.ResolveDependency<JobRequirementsManager>();
        var preferences = pair.Client.ResolveDependency<IClientPreferencesManager>();
        var hunter = prototypes.Index<JobPrototype>("CMUYautjaHunter");
        var session = serverPlayers.Sessions.Single();
        var playerId = session.UserId.UserId;
        var username = session.Name;
        var euiManager = pair.Server.ResolveDependency<Content.Server.EUI.EuiManager>();
        YautjaClanAdminEui? editor = null;

        await pair.Server.WaitPost(() => pair.Server.CfgMan.SetCVar(CCVars.GameRoleWhitelist, true));
        await pair.Server.WaitPost(() =>
        {
            editor = new YautjaClanAdminEui();
            euiManager.OpenEui(editor, session);
        });

        try
        {
            foreach (var flag in new[]
            {
                YautjaWhitelistFlags.Yautja,
                YautjaWhitelistFlags.Legacy,
                YautjaWhitelistFlags.Council,
                YautjaWhitelistFlags.CouncilLegacy,
                YautjaWhitelistFlags.Leader,
            })
            {
                await pair.Server.WaitPost(() =>
                    editor!.HandleMessage(new YautjaClanAdminSetWhitelistMessage(username, flag)));
                await pair.RunTicksSync(30);

                await pair.Server.WaitAssertion(() =>
                {
                    Assert.That(db.GetYautjaWhitelistFlagsAsync(playerId).GetAwaiter().GetResult(), Is.EqualTo((int) flag));
                    Assert.That(jobWhitelist.IsAllowed(session, hunter.ID), Is.True);
                    var capabilities = rankManager.ResolveProfileCapabilitiesCached(session.UserId);
                    Assert.That(
                        capabilities.CanUseLegacy,
                        Is.EqualTo(flag is YautjaWhitelistFlags.Legacy or YautjaWhitelistFlags.CouncilLegacy));
                    Assert.That(
                        capabilities.CanUseCouncilStatus,
                        Is.EqualTo(flag is YautjaWhitelistFlags.Council or YautjaWhitelistFlags.CouncilLegacy));
                    Assert.That(capabilities.CanUseLeaderStatus, Is.EqualTo(flag == YautjaWhitelistFlags.Leader));
                    var state = (YautjaClanAdminEuiState) editor!.GetNewState();
                    var displayedPlayer = state.ClanlessPlayers.SingleOrDefault(player =>
                        player.PlayerId == session.UserId);
                    Assert.That(displayedPlayer, Is.Not.Null);
                    Assert.That(displayedPlayer!.WhitelistFlags, Is.EqualTo(flag));
                });
                await pair.Client.WaitAssertion(() =>
                {
                    Assert.That(requirements.IsWhitelisted(hunter.ID), Is.True);
                    Assert.That(requirements.CanCustomizeWhitelistedJob(hunter.ID), Is.True);
                    Assert.That(
                        preferences.YautjaCapabilities.CanUseLegacy,
                        Is.EqualTo(flag is YautjaWhitelistFlags.Legacy or YautjaWhitelistFlags.CouncilLegacy));
                    Assert.That(
                        preferences.YautjaCapabilities.CanUseCouncilStatus,
                        Is.EqualTo(flag is YautjaWhitelistFlags.Council or YautjaWhitelistFlags.CouncilLegacy));
                    Assert.That(
                        preferences.YautjaCapabilities.CanUseLeaderStatus,
                        Is.EqualTo(flag == YautjaWhitelistFlags.Leader));
                });
            }

            await pair.Server.WaitPost(() =>
                editor!.HandleMessage(new YautjaClanAdminClearWhitelistMessage(session.UserId)));
            await pair.RunTicksSync(30);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(db.GetYautjaWhitelistFlagsAsync(playerId).GetAwaiter().GetResult(), Is.EqualTo(0));
                Assert.That(jobWhitelist.IsAllowed(session, hunter.ID), Is.False);
                var capabilities = rankManager.ResolveProfileCapabilitiesCached(session.UserId);
                Assert.That(capabilities.CanUseLegacy, Is.False);
                Assert.That(capabilities.CanUseCouncilStatus, Is.False);
                Assert.That(capabilities.CanUseLeaderStatus, Is.False);
                var state = (YautjaClanAdminEuiState) editor!.GetNewState();
                Assert.That(state.ClanlessPlayers.Any(player => player.PlayerId == session.UserId), Is.False);
            });
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(requirements.IsWhitelisted(hunter.ID), Is.False);
                Assert.That(requirements.CanCustomizeWhitelistedJob(hunter.ID), Is.False);
                Assert.That(preferences.YautjaCapabilities.CanUseLegacy, Is.False);
                Assert.That(preferences.YautjaCapabilities.CanUseCouncilStatus, Is.False);
                Assert.That(preferences.YautjaCapabilities.CanUseLeaderStatus, Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => editor?.Close());
            await pair.Client.WaitPost(() => clientNet.ClientDisconnect("Yautja clan admin menu test"));
        }

        await pair.CleanReturnAsync();
    }

    private static async Task Reconnect(
        TestPair pair,
        IClientNetManager clientNet,
        IPlayerManager serverPlayers,
        string username)
    {
        await pair.Client.WaitPost(() => clientNet.ClientDisconnect("Yautja whitelist refresh test"));
        await pair.RunTicksSync(20);
        await pair.Server.WaitAssertion(() => Assert.That(serverPlayers.PlayerCount, Is.EqualTo(0)));

        pair.Client.SetConnectTarget(pair.Server);
        await pair.Client.WaitPost(() => clientNet.ClientConnect(null!, 0, username));
        await pair.RunTicksSync(20);
        await pair.Server.WaitAssertion(() => Assert.That(serverPlayers.PlayerCount, Is.EqualTo(1)));
    }
}
