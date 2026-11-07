using System.Collections.Generic;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server._RMC14.Admin;
using Content.Server._RMC14.TacticalMap;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Maths;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Xenonids;

[TestFixture]
public sealed class CMUIndependentXenoHiveTest
{

    [Test]
    public async Task HunterShipTacticalMapOnlyShowsOwnHive()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid alphaHive = default;
        EntityUid forsakenHive = default;
        EntityUid alphaXeno = default;
        EntityUid forsakenXeno = default;
        EntityUid alphaStructure = default;
        EntityUid forsakenStructure = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hives = entMan.System<XenoHiveSystem>();
                var tacticalMaps = entMan.System<TacticalMapSystem>();
                entMan.EnsureComponent<TacticalMapComponent>(map.Grid);

                alphaHive = hives.CreateHive("Hunter Ship Alpha Hive", "CMUHunterShipAlphaHive");
                forsakenHive = hives.CreateHive("Hunter Ship Forsaken Hive", "CMUHunterShipForsakenHive");
                alphaXeno = entMan.SpawnEntity("CMXenoQueen", map.GridCoords);
                forsakenXeno = entMan.SpawnEntity("CMXenoQueen", map.GridCoords);
                alphaStructure = entMan.SpawnEntity("HiveCoreXeno", map.GridCoords);
                forsakenStructure = entMan.SpawnEntity("HiveCoreXeno", map.GridCoords);

                hives.SetHive(alphaXeno, alphaHive);
                hives.SetHive(forsakenXeno, forsakenHive);
                hives.SetHive(alphaStructure, alphaHive);
                hives.SetHive(forsakenStructure, forsakenHive);

                tacticalMaps.RefreshTracked(alphaXeno);
                tacticalMaps.RefreshTracked(forsakenXeno);
                tacticalMaps.RefreshTracked(alphaStructure);
                tacticalMaps.RefreshTracked(forsakenStructure);
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var tacticalMaps = entMan.System<TacticalMapSystem>();
                var tacticalMap = entMan.GetComponent<TacticalMapComponent>(map.Grid);
                var alphaUser = entMan.GetComponent<TacticalMapUserComponent>(alphaXeno);
                var forsakenUser = entMan.GetComponent<TacticalMapUserComponent>(forsakenXeno);

                tacticalMaps.UpdateUserData((alphaXeno, alphaUser), tacticalMap);
                tacticalMaps.UpdateUserData((forsakenXeno, forsakenUser), tacticalMap);

                Assert.Multiple(() =>
                {
                    Assert.That(alphaUser.XenoBlips, Does.ContainKey(alphaXeno.Id));
                    Assert.That(alphaUser.XenoBlips, Does.Not.ContainKey(forsakenXeno.Id));
                    Assert.That(alphaUser.XenoStructureBlips, Does.ContainKey(alphaStructure.Id));
                    Assert.That(alphaUser.XenoStructureBlips, Does.Not.ContainKey(forsakenStructure.Id));
                    Assert.That(forsakenUser.XenoBlips, Does.ContainKey(forsakenXeno.Id));
                    Assert.That(forsakenUser.XenoBlips, Does.Not.ContainKey(alphaXeno.Id));
                    Assert.That(forsakenUser.XenoStructureBlips, Does.ContainKey(forsakenStructure.Id));
                    Assert.That(forsakenUser.XenoStructureBlips, Does.Not.ContainKey(alphaStructure.Id));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                if (entMan.EntityExists(alphaXeno))
                    entMan.DeleteEntity(alphaXeno);

                if (entMan.EntityExists(forsakenXeno))
                    entMan.DeleteEntity(forsakenXeno);

                if (entMan.EntityExists(alphaStructure))
                    entMan.DeleteEntity(alphaStructure);

                if (entMan.EntityExists(forsakenStructure))
                    entMan.DeleteEntity(forsakenStructure);

                if (entMan.EntityExists(alphaHive))
                    entMan.DeleteEntity(alphaHive);

                if (entMan.EntityExists(forsakenHive))
                    entMan.DeleteEntity(forsakenHive);
            });
        }

        await pair.CleanReturnAsync();
    }

}
