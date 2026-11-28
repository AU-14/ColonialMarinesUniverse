#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Threats;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Threats;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats;

/// <summary>
///     <see cref="ThreatPrototype.UngatedLeader"/>: an empty leader body falls back to a member voter, preferring one
///     who asked for the threat-leader job, while a held leader always keeps the seat.
/// </summary>
[TestFixture]
public sealed class ThreatUngatedLeaderTest
{
    private static readonly ProtoId<JobPrototype> ThreatLeaderJob = "AU14JobThreatLeader";
    private static readonly ProtoId<JobPrototype> ThreatMemberJob = "AU14JobThreatMember";
    private static readonly ProtoId<ThreatPrototype> Threat = "CMUTestUngatedLeaderThreat";
    private const string LeaderBody = "CMUTestUngatedLeaderBody";
    private const string MemberBody = "CMUTestUngatedMemberBody";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestUngatedLeaderBody
          components:
          - type: MindContainer
        - type: entity
          id: CMUTestUngatedMemberBody
          components:
          - type: MindContainer
        - type: partySpawn
          id: CMUTestUngatedLeaderSpawn
          leadersToSpawn:
            CMUTestUngatedLeaderBody: 1
          gruntsToSpawn:
            CMUTestUngatedMemberBody: 2
        - type: threat
          id: CMUTestUngatedLeaderThreat
          roundstartspawns: CMUTestUngatedLeaderSpawn
          threatratio: 1
          spawnExtraVoteMembers: false
          ungatedLeader: true
        """;

    [Test]
    public async Task HeldLeaderKeepsTheLeaderSeat()
    {
        await using var pair = await GetPair();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var voters = await server.AddDummySessions(3);
        await pair.RunUntilSynced();
        // A member who asked to lead must not displace the held leader.
        await pair.SetJobPriority(ThreatLeaderJob, JobPriority.High, voters[1].UserId);

        await Deploy(pair, map, voters, heldLeader: voters[0]);
        await pair.RunSeconds(3);

        await server.WaitAssertion(() =>
        {
            var bodies = OwnedBodies(server.EntMan, voters);
            Assert.Multiple(() =>
            {
                Assert.That(bodies[voters[0].UserId], Is.EqualTo(LeaderBody), "the held leader keeps the leader seat");
                Assert.That(bodies[voters[1].UserId], Is.EqualTo(MemberBody));
                Assert.That(bodies[voters[2].UserId], Is.EqualTo(MemberBody));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmptyLeaderSeatGoesToTheVoterWhoAskedToLead()
    {
        await using var pair = await GetPair();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var voters = await server.AddDummySessions(3);
        await pair.RunUntilSynced();
        await pair.SetJobPriority(ThreatLeaderJob, JobPriority.High, voters[2].UserId);

        await Deploy(pair, map, voters, heldLeader: null);
        await pair.RunSeconds(3);

        await server.WaitAssertion(() =>
        {
            var bodies = OwnedBodies(server.EntMan, voters);
            Assert.Multiple(() =>
            {
                Assert.That(bodies[voters[2].UserId], Is.EqualTo(LeaderBody),
                    "the empty leader seat prefers the voter whose profile lists the leader job");
                Assert.That(bodies[voters[0].UserId], Is.EqualTo(MemberBody));
                Assert.That(bodies[voters[1].UserId], Is.EqualTo(MemberBody));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static Task<TestPair> GetPair()
        => PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

    private static async Task Deploy(TestPair pair, TestMapData map, ICommonSession[] voters, ICommonSession? heldLeader)
    {
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            // Held voters wait as observers, as they do after a threat vote.
            var ticker = entMan.System<GameTicker>();
            foreach (var voter in voters)
                ticker.JoinAsObserver(voter);

            entMan.SpawnEntity("threatleaderspawnmarker", map.GridCoords);
            for (var i = 0; i < 2; i++)
                entMan.SpawnEntity("threatmemberspawnmarker", map.GridCoords);

            var assignedJobs = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
            foreach (var voter in voters)
                assignedJobs[voter.UserId] = (voter == heldLeader ? ThreatLeaderJob : ThreatMemberJob, EntityUid.Invalid);

            entMan.System<ThreatSystem>().SpawnThreatFromVote(
                server.ProtoMan.Index(Threat),
                map.MapId,
                assignedJobs,
                voters.Select(voter => voter.UserId).ToList(),
                playerCount: voters.Length);
        });
    }

    /// <summary>Each voter's body prototype; every voter must own exactly one body and no body two voters.</summary>
    private static Dictionary<NetUserId, string> OwnedBodies(IEntityManager entMan, ICommonSession[] voters)
    {
        var minds = entMan.System<SharedMindSystem>();
        var result = new Dictionary<NetUserId, string>();
        var owned = new HashSet<EntityUid>();
        foreach (var voter in voters)
        {
            var mindId = minds.GetMind(voter.UserId);
            Assert.That(mindId, Is.Not.Null, $"{voter.Name} has no mind");
            var body = entMan.GetComponent<MindComponent>(mindId!.Value).OwnedEntity;
            Assert.That(body, Is.Not.Null, $"{voter.Name} was not seated");
            Assert.That(owned.Add(body!.Value), Is.True, "two voters share one body");
            result[voter.UserId] = entMan.GetComponent<MetaDataComponent>(body.Value).EntityPrototype!.ID;
        }

        return result;
    }
}
