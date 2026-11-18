using System.Net;
using Content.IntegrationTests.CMU14.Yautja.BadBlood;
using Content.IntegrationTests.Pair;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.Yautja;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.IntegrationTests.CMU14.Yautja;

// STATE-604: clan join requests are for hunters the clans know (a Yautja whitelist or a clan profile).
// a Bad Blood body opens the hall but must never reach an honourable clan, even when a whitelisted player wears it
[TestFixture]
public sealed class YautjaClanJoinRequestEligibilityTest
{
    private const string NotHunterError = "cmu-yautja-hall-denied-petition-not-hunter";

    [Test]
    public async Task OnlyKnownHuntersRequestAndGetAccepted()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var db = server.ResolveDependency<IServerDbManager>();
        var clans = server.ResolveDependency<YautjaClanManager>();

        var clanId = await db.CreateYautjaClanAsync("Join Gate", "STATE-604", 0, "#335577");
        var leader = await MakePlayer(db, "GateLeader");
        await db.SetYautjaWhitelistFlagsAsync(leader.UserId,
            (int) (YautjaWhitelistFlags.Yautja | YautjaWhitelistFlags.Leader));
        await db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            leader.UserId,
            clanId,
            (int) YautjaRank.Blooded,
            (int) YautjaClanManager.PermissionsForRank(YautjaRank.Blooded),
            0,
            false));
        clans.InvalidateCache(leader);
        var leaderActor = await clans.ResolveActor(leader, false, false);

        // no whitelist and no clan profile: the hall resolves them to a clanless Blooded, which used to be enough
        var outsider = await MakePlayer(db, "GateOutsider");
        var outsiderActor = await clans.ResolveActor(outsider, false, false);
        var refused = await clans.HallPetition(outsiderActor, clanId);
        var outsiderPetitions = (await db.GetYautjaClanPetitionsAsync())
            .Where(entry => entry.PlayerUserId == outsider.UserId)
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(refused.Succeeded, Is.False);
            Assert.That(refused.Error, Is.EqualTo(NotHunterError));
            Assert.That(outsiderPetitions, Is.Empty);
        });

        // a whitelisted honourable hunter still asks and gets in
        var hunter = await MakePlayer(db, "GateHunter");
        await db.SetYautjaWhitelistFlagsAsync(hunter.UserId, (int) YautjaWhitelistFlags.Yautja);
        clans.InvalidateCache(hunter);
        var hunterActor = await clans.ResolveActor(hunter, false, false);
        Assert.That((await clans.HallPetition(hunterActor, clanId)).Succeeded, Is.True);
        var hunterAccepted = await clans.HallAnswerPetition(leaderActor, hunter, true);
        Assert.That(hunterAccepted.Succeeded, Is.True);
        Assert.That((await db.GetYautjaClanMemberAsync(hunter.UserId))?.ClanId, Is.EqualTo(clanId));

        // a request stored before the gate existed: Accept re-checks and the outsider gets no clan profile
        Assert.That(await db.SetYautjaClanPetitionAsync(outsider.UserId, clanId), Is.True);
        var outsiderAccepted = await clans.HallAnswerPetition(leaderActor, outsider, true);
        var outsiderProfile = await db.GetYautjaClanMemberAsync(outsider.UserId);
        Assert.Multiple(() =>
        {
            Assert.That(outsiderAccepted.Succeeded, Is.False);
            Assert.That(outsiderAccepted.Error, Is.EqualTo(NotHunterError));
            Assert.That(outsiderProfile, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WhitelistedHunterPlayingBadBloodCannotRequest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var session = pair.Player!;
        var db = server.ResolveDependency<IServerDbManager>();

        try
        {
            // whitelisted and clanless: an honourable hunter who could otherwise ask
            await AsPlainHunter(pair, session);
            var clanId = await db.CreateYautjaClanAsync("Outcast Gate", "STATE-604", 0, "#553311");

            var map = await pair.CreateTestMap();
            var party = await BadBloodTestHelpers.SpawnParty(pair, map, grunts: 0, attachMinds: false, startRule: false);
            await server.WaitPost(() =>
            {
                var minds = server.System<SharedMindSystem>();
                minds.TransferTo(minds.GetOrCreateMind(session.UserId), party.Leader);
            });
            await pair.RunTicksSync(5);
            await server.WaitAssertion(() =>
            {
                Assert.That(session.AttachedEntity, Is.EqualTo(party.Leader));
                Assert.That(server.EntMan.HasComponent<YautjaBadBloodComponent>(party.Leader), Is.True);
            });

            var hall = await Open(pair, session);
            var state = await WaitForState(pair, hall, _ => true);
            Assert.That(state.Viewer.CanPetition, Is.False);

            // the window hides the button, the server refuses the message anyway
            state = await Send(pair, hall, state, new YautjaClanHallPetitionMessage(clanId));
            Assert.That(state.StatusIsError, Is.True, state.Status);
            Assert.That(await PetitionOf(db, session.UserId), Is.Null);

            // the refusal is about the body: out of it, the same whitelisted player can ask again
            await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(session, null));
            await pair.RunTicksSync(5);
            state = await Send(pair, hall, state, new YautjaClanHallPetitionMessage(clanId));
            Assert.That(state.StatusIsError, Is.False, state.Status);
            Assert.That(await PetitionOf(db, session.UserId), Is.EqualTo(clanId));

            await server.WaitPost(() => hall.Close());
        }
        finally
        {
            await server.WaitPost(() =>
                server.CfgMan.SetCVar(CCVars.ConsoleLoginLocal, CCVars.ConsoleLoginLocal.DefaultValue));
        }

        await pair.CleanReturnAsync();
    }

    private static async Task<int?> PetitionOf(IServerDbManager db, NetUserId player)
    {
        return (await db.GetYautjaClanPetitionsAsync())
            .FirstOrDefault(entry => entry.PlayerUserId == player.UserId)?.ClanId;
    }

    private static async Task AsPlainHunter(TestPair pair, ICommonSession session)
    {
        var server = pair.Server;
        var admin = server.ResolveDependency<IAdminManager>();
        await server.WaitPost(() => server.CfgMan.SetCVar(CCVars.ConsoleLoginLocal, false));
        await server.WaitPost(() => ((AdminManager) admin).ReloadAdmin(session));
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

    private static async Task<YautjaClanHallEui> Open(TestPair pair, ICommonSession session)
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
