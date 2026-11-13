using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Threats;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-101: a forced (admin) threat spawn must seat the players who registered interest and expose every
/// other body as a ghost role. Before the fix it did neither, so a forced Bad Blood clan was unplayable.
/// </summary>
public sealed class BadBloodForcedSpawnTest : GameTest
{
    private const string SeatLeader = "CMUTestForcedSeatLeader";
    private const string SeatGrunt = "CMUTestForcedSeatGrunt";
    private static readonly ProtoId<ThreatPrototype> SeatThreat = "CMUTestForcedSeatThreat";
    private static readonly ProtoId<ThreatPrototype> BadBloodThreat = "BadBloodClanThreat";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestForcedSeatLeader
          components:
          - type: MindContainer
        - type: entity
          id: CMUTestForcedSeatGrunt
          components:
          - type: MindContainer
        - type: partySpawn
          id: CMUTestForcedSeatSpawn
          leadersToSpawn:
            CMUTestForcedSeatLeader: 1
          gruntsToSpawn:
            CMUTestForcedSeatGrunt: 2
        - type: threat
          id: CMUTestForcedSeatThreat
          roundstartspawns: CMUTestForcedSeatSpawn
        """;

    [Test]
    public async Task ForcedSpawnSeatsTheInterestedGhostAndExposesTheOtherBodies()
    {
        // seating a player books them into the current round; a fresh pooled server has never started one and
        // round 0 has no database row, so give it one the way StartRound does
        await Server.WaitPost(() =>
        {
            var ticker = Server.System<Content.Server.GameTicking.GameTicker>();
            if (ticker.RoundId == 0)
            {
                typeof(Content.Server.GameTicking.GameTicker)
                    .GetMethod("IncrementRoundNumber",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(ticker, null);
            }
        });

        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
            SEntMan.SpawnEntity("threatleaderspawnmarker", map.GridCoords);
            for (var i = 0; i < 2; i++)
                SEntMan.SpawnEntity("threatmemberspawnmarker", map.GridCoords);

            Assert.That(SEntMan.System<ThreatSystem>().ForceSpawnThreat(SeatThreat, map.Grid, false), Is.True);
            var interest = SEntMan.System<ForceInterestSystem>();
            var id = interest.GetForces(ServerSession!).Max(force => force.Identifier);
            interest.SetInterest(ServerSession!, id, true);
            Assert.That(interest.GetForces(ServerSession!).Single(force => force.Identifier == id).Interested, Is.True);
            // One volunteer is not a quorum for three roles, so staff deploy it by hand.
            interest.CompleteForce(ServerSession!, id);
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            var seated = ServerSession!.AttachedEntity;
            Assert.That(seated, Is.Not.Null, "a ghost who registered interest must be put into a body");
            Assert.That(PrototypeOf(seated!.Value), Is.EqualTo(SeatLeader), "volunteers fill the leader body first");
            Assert.That(SEntMan.GetComponent<ThreatComponent>(seated.Value).ObjectiveJob,
                Is.EqualTo(ThreatVoteSelection.ThreatLeaderJobId));

            var grunts = Bodies(SeatGrunt);
            Assert.That(grunts, Has.Count.EqualTo(2));
            AssertOpenGhostRoles(grunts);
        });
    }

    [Test]
    public async Task ForcedBadBloodClanBodiesAllBecomeGhostRoles()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
            SEntMan.SpawnEntity("threatleaderspawnmarker", map.GridCoords);
            for (var i = 0; i < 3; i++)
                SEntMan.SpawnEntity("threatmemberspawnmarker", map.GridCoords);

            Assert.That(SEntMan.System<ThreatSystem>().ForceSpawnThreat(BadBloodThreat, map.Grid, false), Is.True);
            var interest = SEntMan.System<ForceInterestSystem>();
            var id = interest.GetForces(ServerSession!).Max(force => force.Identifier);
            interest.CompleteForce(ServerSession!, id);
        });
        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            var clan = SEntMan.EntityQuery<YautjaBadBloodComponent>()
                .Select(comp => comp.Owner)
                .Where(uid => SEntMan.GetComponent<TransformComponent>(uid).MapID == map.MapId)
                .ToList();
            Assert.That(clan, Has.Count.EqualTo(4), "base party is one leader and three grunts");
            Assert.That(clan.Count(uid => SEntMan.GetComponent<ThreatComponent>(uid).ObjectiveJob
                == ThreatVoteSelection.ThreatLeaderJobId), Is.EqualTo(1));
            AssertOpenGhostRoles(clan);
        });
    }

    // spawnthreat <id> <nowin> true: no admin button, no volunteers, the clan lands at once as ghost roles
    [Test]
    public async Task ForcedSpawnThatSkipsInterestDeploysWithNoVolunteers()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
            SEntMan.SpawnEntity("threatleaderspawnmarker", map.GridCoords);
            for (var i = 0; i < 3; i++)
                SEntMan.SpawnEntity("threatmemberspawnmarker", map.GridCoords);

            Assert.That(SEntMan.System<ThreatSystem>().ForceSpawnThreat(BadBloodThreat, map.Grid, false, skipInterest: true),
                Is.True);
            Assert.That(SEntMan.System<ForceInterestSystem>().GetForces(ServerSession!), Is.Empty,
                "nothing is left waiting for interest");
        });
        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            var clan = SEntMan.EntityQuery<YautjaBadBloodComponent>()
                .Select(comp => comp.Owner)
                .Where(uid => SEntMan.GetComponent<TransformComponent>(uid).MapID == map.MapId)
                .ToList();
            Assert.That(clan, Has.Count.EqualTo(4), "the whole party lands without anyone showing interest");
            AssertOpenGhostRoles(clan);
        });
    }

    private void AssertOpenGhostRoles(IReadOnlyCollection<EntityUid> bodies)
    {
        var open = SEntMan.System<GhostRoleSystem>().GetGhostRolesInfo(null).Select(info => info.Entity).ToHashSet();
        foreach (var body in bodies)
        {
            Assert.That(SEntMan.HasComponent<GhostRoleComponent>(body), Is.True,
                "an unassigned forced body must be made a ghost role");
            Assert.That(SEntMan.HasComponent<PendingForceRoleComponent>(body), Is.False);
            Assert.That(open, Does.Contain(SEntMan.GetNetEntity(body)),
                "the ghost role must be registered once the deployment completes");
        }
    }

    private List<EntityUid> Bodies(string prototype)
    {
        return SEntMan.EntityQuery<ThreatComponent>()
            .Select(comp => comp.Owner)
            .Where(uid => PrototypeOf(uid) == prototype)
            .ToList();
    }

    private string? PrototypeOf(EntityUid uid) => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
}
