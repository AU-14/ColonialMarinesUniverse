using System.Collections.Generic;
using System.Linq;
using System.Net;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Yautja;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Eui;
using Robust.Shared.Network;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanHallTest
{
    [Test]
    public async Task BloodedSeesOwnClanButNotEliteNotes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var session = pair.Player!;
        var db = server.ResolveDependency<IServerDbManager>();

        try
        {
            // localhost sessions are full admins otherwise, and staff see everything
            await AsPlainHunter(pair, session);
            var clanId = await db.CreateYautjaClanAsync("Hall Blooded", "Blooded view test", 40, "#aa3322");
            await db.UpdateYautjaClanExtrasAsync(clanId, "pred_mask4_bronze", "Hunt at dusk.", "Elite eyes only.");
            await Join(pair, session.UserId, clanId, YautjaRank.Blooded);

            var hall = await Open(pair, session);
            var state = await WaitForState(pair, hall, s => s.Clan != null);

            Assert.Multiple(() =>
            {
                Assert.That(state.Viewer.Staff, Is.False);
                Assert.That(state.Viewer.Rank, Is.EqualTo(YautjaRank.Blooded));
                Assert.That(state.Clan!.Id, Is.EqualTo(clanId));
                Assert.That(state.Clan.Notice, Is.EqualTo("Hunt at dusk."));
                Assert.That(state.Clan.Sigil, Is.EqualTo("pred_mask4_bronze"));
                Assert.That(state.Clan.EliteNotes, Is.Null);
                Assert.That(state.Clan.CanEditNotice, Is.False);
                Assert.That(state.Clan.Members.Select(m => m.PlayerId), Does.Contain(session.UserId));
                Assert.That(state.Whitelist, Is.Null);
                Assert.That(state.Round, Is.Null);
                Assert.That(state.Clans.Single(c => c.Id == clanId).Honor, Is.EqualTo(40));
            });

            await server.WaitPost(() => hall.Close());
        }
        finally
        {
            await server.WaitPost(() =>
                server.CfgMan.SetCVar(CCVars.ConsoleLoginLocal, CCVars.ConsoleLoginLocal.DefaultValue));
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LeaderRunsClanThroughTheHall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var session = pair.Player!;
        var db = server.ResolveDependency<IServerDbManager>();
        var clans = server.ResolveDependency<YautjaClanManager>();

        try
        {
            await AsPlainHunter(pair, session);
            var clanId = await db.CreateYautjaClanAsync("Hall Leader", "Leader test", 0, "#3355aa");
            await Join(pair, session.UserId, clanId, YautjaRank.Leader);

            var hunter = await MakePlayer(db, "HallHunter");
            await Join(pair, hunter, clanId, YautjaRank.Blooded);
            var recruit = await MakePlayer(db, "HallRecruit");
            await db.SetYautjaWhitelistFlagsAsync(recruit.UserId, (int) YautjaWhitelistFlags.Yautja);
            clans.InvalidateCache(recruit);
            var recruitActor = await clans.ResolveActor(recruit, false, false);
            Assert.That((await clans.HallPetition(recruitActor, clanId)).Succeeded, Is.True);

            var hall = await Open(pair, session);
            var state = await WaitForState(pair, hall, s => s.Clan?.Petitions?.Count == 1);
            Assert.That(state.Clan!.Petitions!.Single().PlayerId, Is.EqualTo(recruit));
            Assert.That(state.Clan.CanEditPresentation, Is.True);
            Assert.That(state.Clan.CanRename, Is.False);

            // promote within the band, then try past it
            state = await Send(pair, hall, state, new YautjaClanHallSetRankMessage(hunter, YautjaRank.Elder));
            Assert.That(state.StatusIsError, Is.False, state.Status);
            Assert.That((await db.GetYautjaClanMemberAsync(hunter.UserId))!.Rank, Is.EqualTo((int) YautjaRank.Elder));

            state = await Send(pair, hall, state, new YautjaClanHallSetRankMessage(hunter, YautjaRank.Leader));
            Assert.That(state.StatusIsError, Is.True);
            Assert.That((await db.GetYautjaClanMemberAsync(hunter.UserId))!.Rank, Is.EqualTo((int) YautjaRank.Elder));

            // the recruit joins as Blooded and the petition is gone
            state = await Send(pair, hall, state, new YautjaClanHallAnswerPetitionMessage(recruit, true));
            Assert.That(state.StatusIsError, Is.False, state.Status);
            var joined = await db.GetYautjaClanMemberAsync(recruit.UserId);
            var petitionsLeft = await db.GetYautjaClanPetitionsAsync(clanId);
            Assert.Multiple(() =>
            {
                Assert.That(joined!.ClanId, Is.EqualTo(clanId));
                Assert.That(joined.Rank, Is.EqualTo((int) YautjaRank.Blooded));
                Assert.That(petitionsLeft, Is.Empty);
            });

            // a leader's rename is ignored, the rest of the look applies
            state = await Send(pair, hall, state, new YautjaClanHallEditLookMessage(clanId, "Stolen Name", "#11aa44", "pred_mask9_crimson"));
            var clan = await db.GetYautjaClanAsync(clanId);
            Assert.Multiple(() =>
            {
                Assert.That(clan!.Name, Is.EqualTo("Hall Leader"));
                Assert.That(clan.Color, Is.EqualTo("#11aa44"));
                Assert.That(clan.Sigil, Is.EqualTo("pred_mask9_crimson"));
            });

            state = await Send(pair, hall, state, new YautjaClanHallExileMessage(hunter));
            var exiled = await db.GetYautjaClanMemberAsync(hunter.UserId);
            Assert.Multiple(() =>
            {
                Assert.That(exiled!.ClanId, Is.Null);
                Assert.That(exiled.Rank, Is.EqualTo((int) YautjaRank.Blooded));
            });

            // and a leader is no council
            state = await Send(pair, hall, state, new YautjaClanHallFoundClanMessage("Not Allowed", "#ffffff"));
            Assert.That(state.StatusIsError, Is.True);
            Assert.That((await db.GetYautjaClansAsync()).Any(c => c.Name == "Not Allowed"), Is.False);

            await server.WaitPost(() => hall.Close());
        }
        finally
        {
            await server.WaitPost(() =>
                server.CfgMan.SetCVar(CCVars.ConsoleLoginLocal, CCVars.ConsoleLoginLocal.DefaultValue));
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundHonorBanksIntoHunterAndClan()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var session = pair.Player!;
        var db = server.ResolveDependency<IServerDbManager>();
        var bank = server.System<YautjaHonorBankSystem>();

        var clanId = await db.CreateYautjaClanAsync("Hall Honor", "Honor test", 100, "#ccaa33");
        await Join(pair, session.UserId, clanId, YautjaRank.Blooded, honor: 10);
        var clanless = await MakePlayer(db, "HallClanless");
        await db.SetYautjaWhitelistFlagsAsync(clanless.UserId, (int) YautjaWhitelistFlags.Yautja);
        var outsider = await MakePlayer(db, "HallOutsider");

        await bank.BankAll(new Dictionary<NetUserId, int>
        {
            [session.UserId] = 25,
            [clanless] = 7,
            [outsider] = 50,
        });

        var member = await db.GetYautjaClanMemberAsync(session.UserId.UserId);
        var clan = await db.GetYautjaClanAsync(clanId);
        var clanlessMember = await db.GetYautjaClanMemberAsync(clanless.UserId);
        var outsiderMember = await db.GetYautjaClanMemberAsync(outsider.UserId);
        Assert.Multiple(() =>
        {
            Assert.That(member!.Honor, Is.EqualTo(35));
            Assert.That(clan!.Honor, Is.EqualTo(125));
            // whitelisted but clanless: gets a profile to hold the honor
            Assert.That(clanlessMember!.Honor, Is.EqualTo(7));
            Assert.That(clanlessMember.ClanId, Is.Null);
            // not a hunter: a youngblood round doesn't make them one
            Assert.That(outsiderMember, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    private static async Task AsPlainHunter(TestPair pair, Robust.Shared.Player.ICommonSession session)
    {
        var server = pair.Server;
        var admin = server.ResolveDependency<Content.Server.Administration.Managers.IAdminManager>();
        await server.WaitPost(() => server.CfgMan.SetCVar(CCVars.ConsoleLoginLocal, false));
        await server.WaitPost(() => ((Content.Server.Administration.Managers.AdminManager) admin).ReloadAdmin(session));
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() => Assert.That(admin.GetAdminData(session), Is.Null));

        var db = server.ResolveDependency<IServerDbManager>();
        await db.SetYautjaWhitelistFlagsAsync(session.UserId.UserId, (int) YautjaWhitelistFlags.Yautja);
        server.ResolveDependency<YautjaClanManager>().InvalidateCache(session.UserId);
    }

    private static async Task<NetUserId> MakePlayer(IServerDbManager db, string name)
    {
        var id = new NetUserId(Guid.NewGuid());
        await db.UpdatePlayerRecordAsync(id, name, IPAddress.Loopback, null);
        return id;
    }

    private static async Task Join(TestPair pair, NetUserId player, int clanId, YautjaRank rank, int honor = 0)
    {
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        await db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            player.UserId,
            clanId,
            (int) rank,
            (int) YautjaClanManager.PermissionsForRank(rank),
            honor,
            false));
        pair.Server.ResolveDependency<YautjaClanManager>().InvalidateCache(player);
    }

    private static async Task<YautjaClanHallEui> Open(TestPair pair, Robust.Shared.Player.ICommonSession session)
    {
        YautjaClanHallEui? hall = null;
        await pair.Server.WaitPost(() =>
        {
            hall = new YautjaClanHallEui();
            pair.Server.ResolveDependency<EuiManager>().OpenEui(hall, session);
        });
        return hall!;
    }

    private static async Task<YautjaClanHallEuiState> WaitForState(
        TestPair pair,
        YautjaClanHallEui hall,
        Func<YautjaClanHallEuiState, bool> ready,
        int afterRevision = 0)
    {
        YautjaClanHallEuiState? state = null;
        await PoolManager.WaitUntil(pair.Server, () =>
        {
            state = (YautjaClanHallEuiState) hall.GetNewState();
            return state.Revision > afterRevision && ready(state);
        }, maxTicks: 600);
        Assert.That(hall.IsShutDown, Is.False);
        return state!;
    }

    private static async Task<YautjaClanHallEuiState> Send(
        TestPair pair,
        YautjaClanHallEui hall,
        YautjaClanHallEuiState before,
        EuiMessageBase message)
    {
        await pair.Server.WaitPost(() => hall.HandleMessage(message));
        return await WaitForState(pair, hall, _ => true, before.Revision);
    }
}
