using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.ManageHive.Boons;
using Content.Shared._RMC14.Xenonids.Weeds;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.BugReports;

// BUG-1057: a cluster on a tower before the takeover time had to be rebuilt to become a pylon
[TestFixture]
public sealed class ClusterTowerTakeoverTest : GameTest
{
    [Test]
    public async Task EarlyClusterBecomesPylonOnceTakeoverTimePasses()
    {
        var map = await Pair.CreateTestMap();
        EntityUid tower = default;
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(RMCCVars.RMCCommunicationTowerXenoTakeoverMinutes, 10000);
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            tower = SEntMan.SpawnEntity("RMCCommunicationsTower", map.GridCoords);
            SEntMan.GetComponent<CommunicationsTowerComponent>(tower).State = CommunicationsTowerState.Broken;

            var cluster = SEntMan.SpawnEntity("HiveClusterXeno", map.GridCoords);
            SEntMan.System<SharedXenoHiveSystem>().SetHive(cluster, hive);
            // the weed tile under the tower points back at the cluster that grew it
            var weeds = SEntMan.SpawnEntity("XenoHiveWeedsCluster", map.GridCoords);
#pragma warning disable RA0002
            SEntMan.GetComponent<XenoWeedsComponent>(weeds).Source = cluster;
#pragma warning restore RA0002
        });

        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CommunicationsTowerComponent>(tower).XenoControlled, Is.False);
            Assert.That(SEntMan.EntityQuery<HivePylonComponent>(), Is.Empty);
        });

        await Server.WaitPost(() => Server.CfgMan.SetCVar(RMCCVars.RMCCommunicationTowerXenoTakeoverMinutes, 0));
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CommunicationsTowerComponent>(tower).XenoControlled, Is.True);
            Assert.That(SEntMan.EntityQuery<HivePylonComponent>(), Is.Not.Empty);
        });

        await Server.WaitPost(() =>
            Server.CfgMan.SetCVar(RMCCVars.RMCCommunicationTowerXenoTakeoverMinutes,
                RMCCVars.RMCCommunicationTowerXenoTakeoverMinutes.DefaultValue));
    }
}
