using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Threats;
using Content.Server.Preferences.Managers;
using Content.Shared.CMU14.Threats;
using Content.Shared.Mind;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats;

/// <summary>
/// ARCH-101 (threat-role helper), FLOW-102 (leftover voters), FLOW-103 (preference seating order) and
/// FLOW-104 (seat counts on the ballot).
/// </summary>
public sealed class ThreatSeatingTest : GameTest
{
    private static readonly ProtoId<NpcFactionPrototype> ThreatFaction = "THREAT";
    private static readonly ProtoId<ThreatPrototype> RespawnThreat = "CMUTestLeftoverRespawnThreat";
    private static readonly ProtoId<ThreatPrototype> ReserveThreat = "CMUTestLeftoverReserveThreat";
    private static readonly ProtoId<ThreatPrototype> PreferenceThreat = "CMUTestPreferenceThreat";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestSeatingBody
          components:
          - type: MindContainer
        - type: partySpawn
          id: CMUTestLeftoverSpawn
          leadersToSpawn:
            CMUTestSeatingBody: 1
        - type: threat
          id: CMUTestLeftoverRespawnThreat
          roundstartspawns: CMUTestLeftoverSpawn
          spawnExtraVoteMembers: false
        - type: threat
          id: CMUTestLeftoverReserveThreat
          roundstartspawns: CMUTestLeftoverSpawn
          spawnExtraVoteMembers: false
          reserveLeftoverVoters: true
        - type: partySpawn
          id: CMUTestPreferenceSpawn
          leadersToSpawn: {}
          gruntsToSpawn:
            CMUTestSeatingBody: 1
        - type: threat
          id: CMUTestPreferenceThreat
          roundstartspawns: CMUTestPreferenceSpawn
          spawnExtraVoteMembers: false
          reserveLeftoverVoters: true
        """;

    [Test]
    public async Task ApplyThreatRolePromotesBodyAndSwapsTheMindJob()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("CMUTestSeatingBody", map.GridCoords);
            var minds = SEntMan.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, body);
            SEntMan.System<SharedRoleSystem>().MindAddJobRole(mind, silent: true,
                jobPrototype: ThreatVoteSelection.ThreatMemberJobId);

            var threats = SEntMan.System<ThreatSystem>();
            threats.ApplyThreatRole(body, ThreatVoteSelection.ThreatLeaderJobId);
            // Applying the same role twice must not stack a second job role on the mind.
            threats.ApplyThreatRole(body, ThreatVoteSelection.ThreatLeaderJobId);

            Assert.That(SEntMan.GetComponent<ThreatComponent>(body).ObjectiveJob,
                Is.EqualTo(ThreatVoteSelection.ThreatLeaderJobId),
                "kill objectives read the leader job from ThreatComponent");
            Assert.That(SEntMan.System<NpcFactionSystem>().IsMember(body, ThreatFaction), Is.True);
            Assert.That(SEntMan.System<SharedJobSystem>().MindTryGetJobId(mind.Owner, out var job), Is.True);
            Assert.That(job, Is.EqualTo(ThreatVoteSelection.ThreatLeaderJobId));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LeftoverVotersAreReservedOnlyWhenTheThreatAsksForIt(bool reserve)
    {
        // No leader marker on the map, so the only voter is left without a body.
        var map = await Pair.CreateTestMap();
        var voter = ServerSession!.UserId;
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
            // a respawn to the lobby wipes the player's mind, so a bodiless mind shows which path ran
            SEntMan.System<SharedMindSystem>().GetOrCreateMind(voter);
            Assert.That(SEntMan.System<SharedMindSystem>().GetMind(voter), Is.Not.Null);
            SEntMan.System<ThreatLeftoverVotersProbeSystem>().Events.Clear();
            var threat = Server.ProtoMan.Index(reserve ? ReserveThreat : RespawnThreat);
            SEntMan.System<ThreatSystem>().SpawnThreatFromVote(threat, map.MapId, new(),
                new List<NetUserId> { voter }, 1);
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            var events = SEntMan.System<ThreatLeftoverVotersProbeSystem>().Events;
            var mind = SEntMan.System<SharedMindSystem>().GetMind(voter);
            if (!reserve)
            {
                Assert.That(events, Is.Empty, "the default path must keep respawning leftovers without the event");
                Assert.That(mind, Is.Null, "the default path must respawn the leftover voter to the lobby");
                return;
            }

            Assert.That(mind, Is.Not.Null, "a reserved voter must not be respawned to the lobby");
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].ThreatId, Is.EqualTo(ReserveThreat));
            Assert.That(events[0].Voters, Is.EqualTo(new[] { voter }),
                "the event carries exactly the unseated, consenting voters");
            Assert.That(SEntMan.System<ThreatVoteSystem>().IsRoundJoinBlocked(voter), Is.False);
        });
    }

    [Test]
    public async Task VoterWhoPickedTheThreatIsSeatedAheadOfAnyThreatVoters()
    {
        // One seat, eight voters. Only the real client lists the threat by name, and it is last in the held
        // list, so without the preference ordering it gets the seat one shuffle in eight.
        var map = await Pair.CreateTestMap();
        var dummies = await Server.AddDummySessions(7);
        var voter = ServerSession!.UserId;
        var prefMan = Server.ResolveDependency<IServerPreferencesManager>();
        var original = prefMan.GetPreferences(voter).Characters[0];
        await Server.WaitPost(() => prefMan.SetProfile(voter, 0,
            original.WithThreatPreference(PreferenceThreat, true)).Wait());

        try
        {
            await Server.WaitAssertion(() =>
            {
                Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
                var profile = prefMan.GetPreferences(voter).SelectedCharacter;
                Assert.That(ThreatVoteSelection.ExplicitlyPrefersThreat(profile,
                        SEntMan.System<AuRoundSystem>().SelectedPreset?.ID, PreferenceThreat.Id), Is.True,
                    "the voter must list the threat by name for this to prove anything");

                SEntMan.SpawnEntity("threatmemberspawnmarker", map.GridCoords);
                var held = dummies.Select(dummy => dummy.UserId).Append(voter).ToList();
                SEntMan.System<ThreatSystem>().SpawnThreatFromVote(Server.ProtoMan.Index(PreferenceThreat),
                    map.MapId, new(), held, held.Count);
            });
            await RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                var seated = ServerSession!.AttachedEntity;
                Assert.That(seated, Is.Not.Null, "the voter who picked the threat must get the only seat");
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(seated!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUTestSeatingBody"));
            });
        }
        finally
        {
            await Server.WaitPost(() => prefMan.SetProfile(voter, 0, original).Wait());
        }
    }

    [Test]
    public async Task BallotLabelShowsTheSeatCount()
    {
        await Server.WaitAssertion(() =>
        {
            var label = SEntMan.System<ThreatVoteSystem>().GetThreatBallotLabel("XenoThreat", 7);
            Assert.That(label, Does.Contain("7"));
            Assert.That(label, Does.Contain(Robust.Shared.Localization.Loc.GetString("au14-threat-vote-option-xeno")));
        });
    }
}

public sealed class ThreatLeftoverVotersProbeSystem : EntitySystem
{
    public readonly List<ThreatLeftoverVotersEvent> Events = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ThreatLeftoverVotersEvent>(OnLeftoverVoters);
    }

    private void OnLeftoverVoters(ref ThreatLeftoverVotersEvent ev)
    {
        Events.Add(ev);
    }
}
