using System.Numerics;
using System.Linq;
using Content.Client.Popups;
using Content.Server._CMU14.Yautja;
using Content.Server.Examine;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._RMC14.Areas;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHonorScoringTest
{
    [Test]
    public async Task YautjaKillAwardsDefaultCmss13HonorButSelfAndEnvironmentDoNot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid selfTarget = default;
        EntityUid environmentTarget = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                selfTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                environmentTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);

                mobState.ChangeMobState(prey, MobState.Dead, origin: hunter);
                mobState.ChangeMobState(selfTarget, MobState.Dead, origin: selfTarget);
                mobState.ChangeMobState(environmentTarget, MobState.Dead);
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var record = entMan.GetComponent<YautjaTrophyRecordComponent>(hunter);

                Assert.Multiple(() =>
                {
                    Assert.That(record.Score, Is.EqualTo(1),
                        "CMSS13 /mob/proc/handle_death_cause() awards add_honor(max(life_kills_total, default_honor_value)) for non-self Yautja kills.");
                    Assert.That(record.RankName, Is.EqualTo("cmu-yautja-rank-hunter"),
                        "One default honor point should not cross the local Blooded rank threshold.");
                    Assert.That(entMan.HasComponent<YautjaTrophyRecordComponent>(selfTarget), Is.False,
                        "CMSS13 skips honor when cause_mob == src.");
                    Assert.That(entMan.HasComponent<YautjaTrophyRecordComponent>(environmentTarget), Is.False,
                        "CMSS13 only calls add_honor when the death cause resolves to a Yautja mob.");
                });
            });
        }
        finally
        {
            await Delete(server, hunter, prey, selfTarget, environmentTarget);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaKillAwardsCmss13TargetLifeKillsWhenAboveDefaultHonor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid dangerousPrey = default;
        EntityUid ordinaryPrey = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                dangerousPrey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                ordinaryPrey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaHonorWorthComponent>(dangerousPrey).LifeKillsTotal = 7;

                mobState.ChangeMobState(dangerousPrey, MobState.Dead, origin: hunter);
                mobState.ChangeMobState(ordinaryPrey, MobState.Dead, origin: hunter);
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var record = server.EntMan.GetComponent<YautjaTrophyRecordComponent>(hunter);

                Assert.That(record.Score, Is.EqualTo(8),
                    "CMSS13 awards max(life_kills_total, default_honor_value), so a 7-kill target plus a default target should grant 8 honor total.");
                Assert.That(record.RankName, Is.EqualTo("cmu-yautja-rank-blooded"),
                    "The local rank record should advance when CMSS13 honor awards cross the existing score threshold.");
            });
        }
        finally
        {
            await Delete(server, hunter, dangerousPrey, ordinaryPrey);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaExamineShowsCmss13HonorWorthOnlyToYautja()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var ordinaryViewer = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var dangerousPrey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var defaultPrey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaHonorWorthComponent>(dangerousPrey).LifeKillsTotal = 7;

                var dangerousText = examine.GetExamineText(dangerousPrey, hunter).ToMarkup();
                var defaultText = examine.GetExamineText(defaultPrey, hunter).ToMarkup();
                var ordinaryViewerText = examine.GetExamineText(dangerousPrey, ordinaryViewer).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(dangerousText, Does.Contain("is worth 7 honor."),
                        "CMSS13 Yautja examine text uses max(life_kills_total, default_honor_value).");
                    Assert.That(defaultText, Does.Contain("is worth 1 honor."),
                        "CMSS13 carbon defaults default_honor_value to 1.");
                    Assert.That(ordinaryViewerText, Does.Not.Contain("honor"),
                        "CMSS13 only appends the honor-worth line when isyautja(user).");
                });
            }
            finally
            {
                DeleteNow(entMan, hunter, ordinaryViewer, dangerousPrey, defaultPrey);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaSelfExamineUsesWhitelistRankInsteadOfTrophyScore()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);

            try
            {
                var yautja = entMan.EnsureComponent<YautjaComponent>(hunter);
                yautja.ClanRank = YautjaRank.Ancient;

                var record = entMan.EnsureComponent<YautjaTrophyRecordComponent>(hunter);
                record.RankName = "cmu-yautja-rank-hunter";

                var examineText = examine.GetExamineText(hunter, hunter).ToMarkup();
                var ancientName = Loc.GetString(YautjaRankMetadata.For(YautjaRank.Ancient).LocalizedName);
                var hunterName = Loc.GetString("cmu-yautja-rank-hunter");

                Assert.Multiple(() =>
                {
                    Assert.That(examineText, Does.Contain(ancientName),
                        "Shift+LMB character information must show the rank granted by the Yautja whitelist.");
                    Assert.That(examineText, Does.Not.Contain(hunterName),
                        "The local trophy score must not replace the whitelist rank in character information.");
                });
            }
            finally
            {
                DeleteNow(entMan, hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarkForHuntBroadcastsCmss13HonorWorthAndAreaToYautja()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;
        var expectedArea = string.Empty;
        const string HunterName = "A'ke Ret";
        const string PreyName = "Guan Thwei";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var marks = entMan.System<YautjaMarkSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out bracer);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                metadata.SetEntityName(hunter, HunterName);
                metadata.SetEntityName(prey, PreyName);
                entMan.EnsureComponent<YautjaHonorWorthComponent>(prey).LifeKillsTotal = 7;
                expectedArea = areas.GetAreaName(prey);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(
                    marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, prey, YautjaMarkKind.Prey, null),
                    Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label =>
                        label.Contains($"{HunterName} has chosen {PreyName}", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains("(7 honor)", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains("as their next target", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains(expectedArea, StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 yaut_hudprocs.dm broadcasts '[real_name] has chosen [prey] ([max(life_kills_total, default_honor_value)] honor) as their next target at [area]'.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteNow(entMan, hunter, prey, bracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static async Task Delete(RobustIntegrationTest.ServerIntegrationInstance server, params EntityUid[] uids)
    {
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var uid in uids)
            {
                if (uid != default && !entMan.Deleted(uid))
                    entMan.DeleteEntity(uid);
            }
        });
    }

    private static EntityUid SpawnHunterWithBracer(IEntityManager entMan, EntityCoordinates coordinates, out EntityUid bracer)
    {
        var hunter = entMan.SpawnEntity("CMMobHuman", coordinates);
        bracer = entMan.SpawnEntity("CMUYautjaBracer", coordinates);
        entMan.EnsureComponent<YautjaComponent>(hunter);
        Assert.That(entMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
        return hunter;
    }

    private static void DeleteNow(IEntityManager entMan, params EntityUid[] uids)
    {
        foreach (var uid in uids)
        {
            if (uid != default && !entMan.Deleted(uid))
                entMan.DeleteEntity(uid);
        }
    }
}
