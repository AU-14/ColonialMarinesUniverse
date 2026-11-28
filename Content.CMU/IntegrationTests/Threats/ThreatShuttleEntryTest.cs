#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._RMC14.Dropship;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.Threats;
using Content.Server.CMU14.Threats.Shuttle;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Buckle.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Threats;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats;

/// <summary>
///     <see cref="ThreatEntryMethod.Shuttle"/>: bodies spawn seated aboard the loaded ship, the ship flies itself to a
///     landing zone once the threat deploys, and leaving again removes unclaimed bodies but never seated players.
/// </summary>
[TestFixture]
public sealed class ThreatShuttleEntryTest
{
    private static readonly ProtoId<JobPrototype> ThreatMemberJob = "AU14JobThreatMember";
    private static readonly ProtoId<ThreatPrototype> Threat = "CMUTestShuttleThreat";
    private const string Body = "CMUTestShuttleThreatBody";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: MobHuman
          id: CMUTestShuttleThreatBody
        - type: partySpawn
          id: CMUTestShuttleThreatSpawn
          leadersToSpawn:
            CMUTestShuttleThreatBody: 1
          gruntsToSpawn:
            CMUTestShuttleThreatBody: 2
        - type: threat
          id: CMUTestShuttleThreat
          roundstartspawns: CMUTestShuttleThreatSpawn
          threatratio: 1
          spawnExtraVoteMembers: false
          entryMethod: Shuttle
          shuttlePath: /Maps/CMU14/Dropships/black_ert.yml
          landingZone: Neutral
          shuttleStartupTime: 1
          shuttleHyperspaceTime: 2
        """;

    [Test]
    public async Task ShuttleThreatSeatsBodiesFliesAndCleansUpOnDeparture()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Destructive = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var voters = await server.AddDummySessions(2);
        await pair.RunUntilSynced();

        var planet = MapId.Nullspace;
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            server.CfgMan.SetCVar(CCVars.FTLArrivalTime, 0.5f);
            server.CfgMan.SetCVar(CCVars.FTLCooldown, 1f);

            entMan.System<SharedMapSystem>().CreateMap(out planet);
            entMan.SpawnEntity("CMDropshipDestination", new MapCoordinates(Vector2.Zero, planet));

            var ticker = entMan.System<GameTicker>();
            var assignedJobs = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
            foreach (var voter in voters)
            {
                ticker.JoinAsObserver(voter);
                assignedJobs[voter.UserId] = (ThreatMemberJob, EntityUid.Invalid);
            }

            entMan.System<ThreatSystem>().SpawnThreatFromVote(
                server.ProtoMan.Index(Threat),
                planet,
                assignedJobs,
                voters.Select(voter => voter.UserId).ToList(),
                // a population of three gives a three-body budget for two voters, so one body stays unclaimed
                playerCount: voters.Length + 1);
        });

        await pair.RunSeconds(3);

        var ship = EntityUid.Invalid;
        var playerBodies = new List<EntityUid>();
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ships = entMan.EntityQuery<CMUThreatShuttleComponent>().Select(comp => comp.Owner).ToList();
            Assert.That(ships, Has.Count.EqualTo(1), "the threat must load exactly one ship");
            ship = ships[0];

            // The launch is the shuttle system's answer to ThreatDeployedEvent carrying this ship.
            Assert.That(entMan.GetComponent<CMUThreatShuttleComponent>(ship).State,
                Is.AnyOf(CMUThreatShuttleState.InFlight, CMUThreatShuttleState.Landed),
                "the deployed ship must launch itself");

            var bodies = entMan.EntityQuery<MetaDataComponent>()
                .Where(meta => meta.EntityPrototype?.ID == Body)
                .Select(meta => meta.Owner)
                .ToList();
            Assert.That(bodies, Has.Count.EqualTo(3));
            foreach (var body in bodies)
            {
                Assert.That(entMan.GetComponent<TransformComponent>(body).GridUid, Is.EqualTo(ship),
                    "bodies spawn aboard the ship");
                Assert.That(entMan.GetComponent<BuckleComponent>(body).Buckled, Is.True, "bodies start seated");
            }

            var minds = entMan.System<SharedMindSystem>();
            foreach (var voter in voters)
            {
                var owned = entMan.GetComponent<MindComponent>(minds.GetMind(voter.UserId)!.Value).OwnedEntity;
                Assert.That(owned, Is.Not.Null, "each voter is seated");
                Assert.That(bodies, Does.Contain(owned!.Value), "each voter is seated in a ship body");
                playerBodies.Add(owned!.Value);
            }
        });

        await WaitFor(pair, () => server.EntMan.GetComponent<CMUThreatShuttleComponent>(ship).State
            == CMUThreatShuttleState.Landed);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<TransformComponent>(ship).MapID, Is.EqualTo(planet));
            Assert.That(entMan.HasComponent<ThirdPartyDropshipAutoReturnComponent>(ship), Is.True,
                "a neutral landing must still arm the third-party auto-return");
        });

        // Leave the zone as the auto-return would, once the landing cooldown is over.
        await WaitFor(pair, () => !server.EntMan.HasComponent<FTLComponent>(ship));

        var unclaimed = new List<EntityUid>();
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            unclaimed.AddRange(entMan.EntityQuery<UnclaimedForceRoleComponent>()
                .Select(comp => comp.Owner)
                .Where(uid => entMan.GetComponent<TransformComponent>(uid).GridUid == ship));
            Assert.That(unclaimed, Has.Count.EqualTo(1), "the leader body nobody voted for is unclaimed");

            var returnPad = entMan.EntityQuery<ThirdPartyDropshipReturnDestinationComponent>()
                .Single(pad => pad.Shuttle == ship).Owner;
            var nav = entMan.GetComponent<CMUThreatShuttleComponent>(ship).Navigation!.Value;
            Assert.That(entMan.System<DropshipSystem>().FlyTo(
                    (nav, entMan.GetComponent<DropshipNavigationComputerComponent>(nav)),
                    returnPad,
                    null,
                    startupTime: 1f,
                    hyperspaceTime: 2f),
                Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<CMUThreatShuttleComponent>(ship).State,
                Is.EqualTo(CMUThreatShuttleState.Departed));
            Assert.Multiple(() =>
            {
                foreach (var body in unclaimed)
                    Assert.That(entMan.Deleted(body), Is.True, "unclaimed bodies leave with the ship");
                foreach (var body in playerBodies)
                    Assert.That(entMan.Deleted(body), Is.False, "seated players are never removed");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static async Task WaitFor(TestPair pair, Func<bool> condition, int seconds = 40)
    {
        var met = false;
        for (var i = 0; i < seconds && !met; i++)
        {
            await pair.RunSeconds(1);
            await pair.Server.WaitPost(() => met = condition());
        }

        Assert.That(met, Is.True, "timed out waiting for the threat ship");
    }
}
