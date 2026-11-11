using System.Numerics;
using System.Linq;
using Content.Client.Popups;
using Content.Server.CMU14.Yautja;
using Content.Server.Examine;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._RMC14.Areas;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Yautja;

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

}
