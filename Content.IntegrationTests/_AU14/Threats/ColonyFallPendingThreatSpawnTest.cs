#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.Server._CMU14.Threats;
using Content.Server.AU14.Scenario;
using Content.Shared._CMU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Threats;

[TestFixture]
public sealed class ColonyFallPendingThreatSpawnTest
{
    private const int PlayerCount = 40;
    private static readonly ProtoId<JobPrototype> ThreatLeaderJob = "AU14JobThreatLeader";
    private static readonly ProtoId<JobPrototype> ThreatMemberJob = "AU14JobThreatMember";
    private static readonly ProtoId<ThreatPrototype> XenoThreat = "XenoThreat";
    private static readonly ProtoId<ThreatPrototype> CultistThreat = "cultistThreatOnMarker";

    [Test]
    public async Task DelayedThreatSpawnsKeepMultiplePlannedForceContracts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var threatSystem = server.System<ThreatSystem>();
            var xeno = prototypes.Index(XenoThreat);
            var cultist = prototypes.Index(CultistThreat);
            var assignedJobs = BuildThreatAssignments();
            var heldPlayers = assignedJobs.Keys.ToList();

            threatSystem.SchedulePendingThreatSpawn(
                xeno,
                map.MapId,
                assignedJobs,
                TimeSpan.FromSeconds(30),
                heldPlayers,
                requireObserverForVotePlayers: true);
            threatSystem.SchedulePendingThreatSpawn(
                cultist,
                map.MapId,
                assignedJobs,
                TimeSpan.FromSeconds(20));

            var pending = threatSystem.PendingThreatSpawnsForDebug;
            Assert.That(pending, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(pending[0].ThreatId, Is.EqualTo(cultist.ID));
                Assert.That(pending[1].ThreatId, Is.EqualTo(xeno.ID));
                Assert.That(pending[0].FireAt, Is.LessThan(pending[1].FireAt));
                Assert.That(pending[0].PlannedForce, Is.Not.Null);
                Assert.That(pending[1].PlannedForce, Is.Not.Null);
            });

            AssertPlannedForceMatchesSpawnPlan(cultist, pending[0].PlannedForce!);
            AssertPlannedForceMatchesSpawnPlan(xeno, pending[1].PlannedForce!);
        });

        await pair.CleanReturnAsync();
    }

    private static Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> BuildThreatAssignments()
    {
        var assignedJobs = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
        for (var i = 0; i < PlayerCount; i++)
        {
            assignedJobs[new NetUserId(Guid.NewGuid())] = (
                i == 0 ? ThreatLeaderJob : ThreatMemberJob,
                EntityUid.Invalid);
        }

        return assignedJobs;
    }

    private static void AssertPlannedForceMatchesSpawnPlan(
        ThreatPrototype threat,
        ResolvedThreatForcePlan force)
    {
        var leaderBucket = force.SpawnPlan.BodyBuckets.Single(bucket =>
            bucket.Bucket.Equals(ThreatMarkerType.Leader.ToString(), StringComparison.OrdinalIgnoreCase));
        var memberBucket = force.SpawnPlan.BodyBuckets.Single(bucket =>
            bucket.Bucket.Equals(ThreatMarkerType.Member.ToString(), StringComparison.OrdinalIgnoreCase));

        Assert.Multiple(() =>
        {
            Assert.That(force.ThreatId, Is.EqualTo(threat.ID));
            Assert.That(force.LeaderBodies, Is.EqualTo(leaderBucket.Count));
            Assert.That(force.MemberBodies, Is.EqualTo(memberBucket.Count));
            Assert.That(leaderBucket.Bodies.Values.Sum(), Is.EqualTo(leaderBucket.Count));
            Assert.That(memberBucket.Bodies.Values.Sum(), Is.EqualTo(memberBucket.Count));
            Assert.That(force.WinConditionRuleIds, Is.EquivalentTo(threat.WinConditions));
        });
    }
}
