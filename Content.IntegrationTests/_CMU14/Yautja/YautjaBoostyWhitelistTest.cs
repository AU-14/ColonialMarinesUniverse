using Content.Client.Players.PlayTimeTracking;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Players.JobWhitelist;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server._CMU14.Yautja;
using Content.Server._RMC14.LinkAccount;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared._CMU14.Yautja;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaBoostyWhitelistTest
{
    [Test]
    public async Task BoostyPrioritiesOneThroughFourAllowHunterAndPriorityFiveDoesNot()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            InLobby = true,
            Dirty = true,
            DummyTicker = false,
        });
        var player = pair.Player;
        Assert.That(player, Is.Not.Null);

        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var linkAccount = pair.Server.ResolveDependency<LinkAccountManager>();
        var jobWhitelist = pair.Server.ResolveDependency<JobWhitelistManager>();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();

        await pair.Server.WaitPost(() => pair.Server.CfgMan.SetCVar(CCVars.GameRoleWhitelist, true));
        var userDb = pair.Server.ResolveDependency<UserDbDataManager>();
        await pair.Server.WaitAssertion(() => Assert.That(userDb.IsLoadComplete(player!), Is.True));
        for (var priority = 1; priority <= 4; priority++)
        {
            await db.UpsertPatronTier($"Test Boosty Priority {priority}",
                9912833966292471860UL + (ulong) priority,
                priority,
                false,
                false,
                false,
                false,
                false,
                false);
        }

        await db.UpsertPatronTier("Test Boosty Priority Five", 9912833966292471869UL, 5, false, false, false, false, false, false);

        player = player!;
        for (var priority = 1; priority <= 4; priority++)
        {
            await pair.Server.ExecuteCommand($"rmcboosty grant {player.UserId.UserId} \"Test Boosty Priority {priority}\"");
            await pair.Server.WaitPost(() => jobWhitelist.SendJobWhitelist(player));
            await pair.RunTicksSync(20);

            await pair.Server.WaitAssertion(() =>
            {
                var hunter = prototypes.Index<JobPrototype>("CMUYautjaHunter");
                Assert.That(linkAccount.GetConnectedPatron(player.UserId)?.Tier?.Priority, Is.EqualTo(priority));
                Assert.That(hunter.Whitelisted, Is.True);
                Assert.That(jobWhitelist.IsAllowed(player, hunter.ID), Is.True);
                Assert.That(jobWhitelist.IsWhitelisted(player.UserId, hunter.ID), Is.True);
            });

            await pair.Client.WaitAssertion(() =>
            {
                var requirements = pair.Client.ResolveDependency<JobRequirementsManager>();
                Assert.That(requirements.IsWhitelisted("CMUYautjaHunter"), Is.True);
                Assert.That(requirements.CanCustomizeWhitelistedJob("CMUYautjaHunter"), Is.True);
            });
        }

        var ticker = pair.Server.System<GameTicker>();
        var hunterStation = EntityUid.Invalid;
        await pair.Server.WaitPost(() =>
        {
            pair.Server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false);
            pair.Server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomMinimumRounds, 1);
            pair.Server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomMaximumRounds, 1);
            pair.Server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, true);
            ticker.RestartRound();
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await pair.RunTicksSync(30);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            var stations = pair.Server.EntMan.EntityQueryEnumerator<StationJobsComponent>();
            while (stations.MoveNext(out var station, out var jobs))
            {
                if (!jobs.JobList.TryGetValue("CMUYautjaHunter", out var slots) || slots == 0)
                    continue;

                hunterStation = station;
                break;
            }

            Assert.That(hunterStation, Is.Not.EqualTo(EntityUid.Invalid));
            Assert.That(jobWhitelist.IsAllowed(player, "CMUYautjaHunter"), Is.True);
            ticker.MakeJoinGame(player, hunterStation, "CMUYautjaHunter", silent: true);
        });
        await pair.RunTicksSync(30);

        await pair.Server.WaitAssertion(() =>
        {
            var mindSystem = pair.Server.System<MindSystem>();
            Assert.That(ticker.PlayerGameStatuses[player.UserId], Is.EqualTo(PlayerGameStatus.JoinedGame));
            Assert.That(player.AttachedEntity, Is.Not.Null);
            var mind = mindSystem.GetMind(player.AttachedEntity!.Value);
            Assert.That(mind, Is.Not.Null);
            Assert.That(pair.Server.System<SharedJobSystem>().MindTryGetJobId(mind!.Value, out var job), Is.True);
            Assert.That(job, Is.EqualTo("CMUYautjaHunter"));
            Assert.That(pair.Server.EntMan.GetComponent<HumanoidAppearanceComponent>(player.AttachedEntity.Value).Species,
                Is.EqualTo("Yautja"));
        });

        await pair.Server.ExecuteCommand($"rmcboosty grant {player.UserId.UserId} \"Test Boosty Priority Five\"");
        await pair.Server.WaitPost(() => jobWhitelist.SendJobWhitelist(player));
        await pair.RunTicksSync(20);

        await pair.Server.WaitAssertion(() =>
        {
            var hunter = prototypes.Index<JobPrototype>("CMUYautjaHunter");
            Assert.That(jobWhitelist.IsAllowed(player, hunter.ID), Is.False);
        });

        await pair.Client.WaitAssertion(() =>
        {
            var requirements = pair.Client.ResolveDependency<JobRequirementsManager>();
            Assert.That(requirements.IsWhitelisted("CMUYautjaHunter"), Is.False);
            Assert.That(requirements.CanCustomizeWhitelistedJob("CMUYautjaHunter"), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
