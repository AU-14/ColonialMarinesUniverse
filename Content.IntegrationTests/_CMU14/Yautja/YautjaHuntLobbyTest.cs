using Content.Client.CMU14.Yautja.Lobby;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.Yautja;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Players.JobWhitelist;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Yautja;
using Content.Shared.GameTicking;
using Content.Shared.Roles.Jobs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntLobbyTest
{
    [Test]
    public async Task WhitelistedPlayerCanReadyForQueuedHuntAndStartsOnHunterShip()
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
        var jobWhitelist = server.ResolveDependency<JobWhitelistManager>();

        await server.WaitPost(() => server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false));

        // the hunt gets queued while the lobby is still open, not once the round is running
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
            Assert.That(round.TryInitializePredatorRound(out _), Is.True);
            Assert.That(round.HuntActive, Is.True);

            // no whitelist yet, so no hunt for you
            Assert.That(round.TrySetHuntReady(player, true, out _), Is.False);
        });
        await pair.RunTicksSync(10);

        // the hunt is on, but the card/toggle stay hidden from non-holders
        await pair.Client.WaitAssertion(() =>
        {
            var lobby = pair.Client.System<YautjaHuntLobbySystem>();
            Assert.That(lobby.HuntActive, Is.True);
            Assert.That(lobby.HuntWhitelisted, Is.False);
        });

        var db = server.ResolveDependency<IServerDbManager>();
        await db.SetYautjaWhitelistFlagsAsync(player.UserId.UserId, (int) YautjaWhitelistFlags.Yautja);
        await jobWhitelist.RefreshYautjaWhitelist(player.UserId);
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(round.TrySetHuntReady(player, true, out _), Is.True);
            Assert.That(ticker.PlayerGameStatuses[player.UserId], Is.EqualTo(PlayerGameStatus.ReadyToPlay));
        });
        await pair.RunTicksSync(10);

        await pair.Client.WaitAssertion(() =>
        {
            var lobby = pair.Client.System<YautjaHuntLobbySystem>();
            Assert.That(lobby.HuntActive, Is.True);
            Assert.That(lobby.HuntWhitelisted, Is.True);
            Assert.That(lobby.HuntReady, Is.True);
        });

        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(ticker.PlayerGameStatuses[player.UserId], Is.EqualTo(PlayerGameStatus.JoinedGame));
            Assert.That(player.AttachedEntity, Is.Not.Null);

            var mob = player.AttachedEntity!.Value;
            Assert.That(server.EntMan.HasComponent<YautjaComponent>(mob), Is.True);

            var mind = server.System<MindSystem>().GetMind(mob);
            Assert.That(mind, Is.Not.Null);
            Assert.That(server.System<SharedJobSystem>().MindTryGetJobId(mind!.Value, out var job), Is.True);
            Assert.That(job?.Id, Is.EqualTo("CMUYautjaHunter"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AncientSlotIsStaffOrAncientOnlyAndSpawnsAtAncientRank()
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
        var admin = (AdminManager) server.ResolveDependency<IAdminManager>();

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false);
            ticker.StartRound();
        });
        await pair.RunTicksSync(10);

        // the test client logs in as a local admin
        await server.WaitAssertion(() =>
        {
            Assert.That(admin.GetAdminData(player, includeDeAdmin: true), Is.Not.Null);
            Assert.That(round.IsAncientSlotEligible(player), Is.True);
            Assert.That(round.TryInitializePredatorRound(out _), Is.True);
        });
        await pair.RunTicksSync(30);

        var ancientStation = EntityUid.Invalid;
        await server.WaitAssertion(() =>
        {
            var jobs = server.System<StationJobsSystem>();
            var stations = server.EntMan.EntityQueryEnumerator<StationJobsComponent>();
            while (stations.MoveNext(out var station, out var stationJobs))
            {
                if (!jobs.TryGetJobSlot(station, "CMUYautjaAncient", out var slots, stationJobs) || slots == 0)
                    continue;

                Assert.That(slots, Is.EqualTo(1));
                ancientStation = station;
                break;
            }

            Assert.That(ancientStation, Is.Not.EqualTo(EntityUid.Invalid));
            ticker.MakeJoinGame(player, ancientStation, "CMUYautjaAncient", silent: true);
        });
        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.Not.Null);
            var mob = player.AttachedEntity!.Value;
            Assert.That(server.EntMan.GetComponent<YautjaComponent>(mob).ClanRank, Is.EqualTo(YautjaRank.Ancient));

            // the overseer reaches a living pred on the hunting party net
            var sent = server.System<YautjaOverseerSystem>()
                .SendOverseerMessage("test", YautjaOverseerNetwork.HuntingParty, "test");
            Assert.That(sent, Is.EqualTo(1));
            Assert.That(server.System<YautjaOverseerSystem>()
                .SendOverseerMessage("test", YautjaOverseerNetwork.Stranded, "test"), Is.Zero);
        });

        // drop admin: no longer eligible
        await server.WaitPost(() => server.CfgMan.SetCVar(CCVars.ConsoleLoginLocal, false));
        await server.WaitPost(() => admin.ReloadAdmin(player));
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() => Assert.That(round.IsAncientSlotEligible(player), Is.False));

        await pair.CleanReturnAsync();
    }
}
