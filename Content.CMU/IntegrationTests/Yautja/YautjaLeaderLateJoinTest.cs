using Content.Client.CMU14.Yautja.Lobby;
using Content.Server.CMU14.Yautja;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Players.JobWhitelist;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.Yautja;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Robust.Server.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaLeaderLateJoinTest
{
    // bug 1526: ghost join let a clan leader in, the lobby's joingame refused them because the hunter slots read 0
    [Test]
    public async Task ClanLeaderJoinsTheHuntFromTheLobbyWhenNoSlotsAreLeft()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            InLobby = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var player = pair.Player!;
        var ticker = server.System<GameTicker>();
        var round = server.System<YautjaPredatorRoundSystem>();
        var db = server.ResolveDependency<IServerDbManager>();
        var ranks = server.ResolveDependency<YautjaRankManager>();
        var prefs = server.ResolveDependency<IServerPreferencesManager>();
        var console = server.ResolveDependency<IServerConsoleHost>();
        var whitelist = server.ResolveDependency<JobWhitelistManager>();

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false);
            ticker.StartRound();
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() => Assert.That(round.TryInitializePredatorRound(out _), Is.True));
        await pair.RunTicksSync(30);

        var hunterStation = EntityUid.Invalid;
        await server.WaitAssertion(() =>
        {
            var jobs = server.System<StationJobsSystem>();
            var stations = server.EntMan.EntityQueryEnumerator<StationJobsComponent>();
            while (stations.MoveNext(out var station, out var stationJobs))
            {
                if (!jobs.TryGetJobSlot(station, "CMUYautjaHunter", out _, stationJobs))
                    continue;

                hunterStation = station;
                Assert.That(jobs.TrySetJobSlot(station, "CMUYautjaHunter", 0, stationJobs: stationJobs), Is.True);
                break;
            }

            Assert.That(hunterStation, Is.Not.EqualTo(EntityUid.Invalid));
        });

        var join = $"joingame CMUYautjaHunter {server.EntMan.GetNetEntity(hunterStation).Id}";

        // control: an ordinary hunter is still turned away at 0 slots
        await db.SetYautjaWhitelistFlagsAsync(player.UserId.UserId, (int) YautjaWhitelistFlags.Yautja);
        await whitelist.RefreshYautjaWhitelist(player.UserId);
        await server.WaitPost(() => console.ExecuteCommand(player, join));
        await pair.RunTicksSync(30);
        await server.WaitAssertion(() =>
            Assert.That(ticker.PlayerGameStatuses[player.UserId], Is.Not.EqualTo(PlayerGameStatus.JoinedGame)));

        // a clan leader doesn't take a slot
        await db.SetYautjaWhitelistFlagsAsync(player.UserId.UserId,
            (int) (YautjaWhitelistFlags.Yautja | YautjaWhitelistFlags.Leader));
        await whitelist.RefreshYautjaWhitelist(player.UserId);
        await server.WaitPost(() =>
        {
            var preferences = prefs.GetPreferences(player.UserId);
            var profile = (HumanoidCharacterProfile) preferences.SelectedCharacter;
            _ = prefs.SetProfile(player.UserId, preferences.SelectedCharacterIndex,
                profile.WithYautjaProfile(profile.YautjaProfile.WithStatus(YautjaProfileStatus.Leader)));
        });
        await pair.RunTicksSync(30);
        await server.WaitAssertion(() =>
        {
            var selected = (HumanoidCharacterProfile) prefs.GetPreferences(player.UserId).SelectedCharacter;
            var caps = ranks.ResolveProfileCapabilitiesCached(player.UserId);
            Assert.That(round.ResolveRankForSession(player), Is.EqualTo(YautjaRank.Leader),
                $"status {selected.YautjaProfile.Status}, leader allowed {caps.CanUseLeaderStatus}, base {caps.Rank}");
        });

        await server.WaitPost(() => console.ExecuteCommand(player, join));
        await pair.RunTicksSync(30);
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.PlayerGameStatuses[player.UserId], Is.EqualTo(PlayerGameStatus.JoinedGame));
            Assert.That(player.AttachedEntity, Is.Not.Null);
            Assert.That(server.EntMan.HasComponent<YautjaComponent>(player.AttachedEntity!.Value), Is.True);
        });

        await db.SetYautjaWhitelistFlagsAsync(player.UserId.UserId, 0);
        await whitelist.RefreshYautjaWhitelist(player.UserId);
        await pair.CleanReturnAsync();
    }

    // bug 1526 (round 3193): a whitelisted hunter who connects mid-hunt got no Join Hunt card. the lobby status went
    // out before their whitelist finished loading and was never sent again; the ghost join checked later and worked
    [Test]
    public async Task HunterConnectingMidHuntSeesTheJoinHuntCard()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            InLobby = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var client = pair.Client;
        var player = pair.Player!;
        var username = player.Name;
        var userId = player.UserId;
        var ticker = server.System<GameTicker>();
        var round = server.System<YautjaPredatorRoundSystem>();
        var db = server.ResolveDependency<IServerDbManager>();

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false);
            ticker.StartRound();
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() => Assert.That(round.TryInitializePredatorRound(out _), Is.True));
        await pair.RunTicksSync(30);

        await client.WaitPost(() => client.ResolveDependency<IClientNetManager>().ClientDisconnect("relog"));
        await pair.RunTicksSync(20);

        // whitelisted while they were away, so it only comes in with their player data on connect
        await db.SetYautjaWhitelistFlagsAsync(userId.UserId, (int) YautjaWhitelistFlags.Yautja);

        client.SetConnectTarget(server);
        await client.WaitPost(() => client.ResolveDependency<IClientNetManager>().ClientConnect(null!, 0, username));
        for (var i = 0; i < 20; i++)
        {
            await pair.RunTicksSync(10);
            await Task.Delay(50);
        }

        await client.WaitAssertion(() =>
        {
            var lobby = client.System<YautjaHuntLobbySystem>();
            Assert.That(lobby.HuntActive, Is.True);
            Assert.That(lobby.HuntWhitelisted, Is.True, "the Join Hunt card needs the whitelist the hunter actually holds");
            Assert.That(lobby.CanSeeHuntCard, Is.True);
        });

        await db.SetYautjaWhitelistFlagsAsync(userId.UserId, 0);
        await pair.CleanReturnAsync();
    }
}
