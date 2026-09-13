using System.Linq;
using Content.Client.UserInterface.Systems.Ghost.Controls.Roles;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.Threats;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.CMU14.Threats;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Roles;
using Content.Shared.Mind;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats;

public sealed class ForceInterestTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: TestForceInterestBody
          components:
          - type: MindContainer
          - type: GhostRole
            name: au14-threat-ghost-role-name
            description: au14-threat-ghost-role-description
          - type: GhostTakeoverAvailable
        - type: partySpawn
          id: TestForceInterestSpawn
          leadersToSpawn:
            TestForceInterestBody: 1
          entsToSpawn:
            Chair: 1
        - type: thirdParty
          id: TestForceInterestParty
          displayName: Interest test party
          partyspawn: TestForceInterestSpawn
          entrymethod: ground
          announcearrival: ""
        - type: threat
          id: TestForceInterestThreat
          roundstartspawns: TestForceInterestSpawn
        """;

    [Test]
    public async Task CalledPartyWaitsForInterestAndShowsPlayableCount()
    {
        var map = await Pair.CreateTestMap();
        uint id = 0;
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
            SEntMan.SpawnEntity("thirdpartyleaderspawnmarker", map.GridCoords);
            SEntMan.SpawnEntity("thirdpartyentityspawnmarker", map.GridCoords);

            var party = Server.ProtoMan.Index<ThirdPartyPrototype>("TestForceInterestParty");
            var spawn = Server.ProtoMan.Index(party.PartySpawn);
            Assert.That(SEntMan.System<ThirdPartySystem>().SpawnThirdParty(party, spawn, false), Is.True);
            var forces = SEntMan.System<ForceInterestSystem>().GetForces(ServerSession!);
            Assert.That(forces, Has.Length.EqualTo(1));
            id = forces[0].Identifier;
            Assert.That(forces[0].TotalRoles, Is.EqualTo(1), "furniture must not increase the player threshold");
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQuery<UnclaimedForceRoleComponent>().Count(), Is.Zero);
            var interest = SEntMan.System<ForceInterestSystem>();
            Assert.That(interest.IsPending(id), Is.True);
            interest.SetInterest(ServerSession!, id, true);
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<ForceInterestSystem>().IsPending(id), Is.False);
            Assert.That(SEntMan.EntityQuery<UnclaimedForceRoleComponent>().Count(), Is.EqualTo(1));
            Assert.That(SEntMan.System<ThirdPartySystem>().GetQueuedThirdParties(), Is.Empty);
        });
    }

    [Test]
    public async Task ScheduledThreatIsHiddenUntilArrivalAndThenRequiresInterest()
    {
        var map = await Pair.CreateTestMap();
        uint id = 0;
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
            SEntMan.SpawnEntity("threatleaderspawnmarker", map.GridCoords);
            SEntMan.SpawnEntity("threatentityspawnmarker", map.GridCoords);
            var threat = Server.ProtoMan.Index<ThreatPrototype>("TestForceInterestThreat");
            SEntMan.System<ThreatSystem>().SchedulePendingThreatSpawn(threat, map.MapId, new(), TimeSpan.FromSeconds(1));
            Assert.That(SEntMan.System<ForceInterestSystem>().GetForces(ServerSession!), Is.Empty);
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var interest = SEntMan.System<ForceInterestSystem>();
            var force = interest.GetForces(ServerSession!).Single();
            id = force.Identifier;
            Assert.That(force.Ready, Is.True);
            Assert.That(force.TotalRoles, Is.EqualTo(1));
            Assert.That(SEntMan.EntityQuery<UnclaimedForceRoleComponent>().Count(), Is.Zero);
            interest.SetInterest(ServerSession!, id, true);
            Assert.That(interest.GetForces(ServerSession!).Single().InterestedPlayers, Is.EqualTo(1));
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<ForceInterestSystem>().IsPending(id), Is.False);
            Assert.That(SEntMan.EntityQuery<UnclaimedForceRoleComponent>().Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ShelvedForceDoesNotBlockScheduledForceAndInterestIsUniqueAndRevocable()
    {
        uint shelved = 0;
        uint scheduled = 0;
        var spawned = 0;
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
            var interest = SEntMan.System<ForceInterestSystem>();
            shelved = interest.QueueForce("Shelved", new Dictionary<string, int> { ["TestForceInterestBody"] = 2 },
                _ => throw new AssertionException("One volunteer cannot deploy two roles."));
            scheduled = interest.QueueForce("Scheduled", new Dictionary<string, int> { ["TestForceInterestBody"] = 1 },
                _ => { spawned++; return true; }, false);
            interest.SetInterest(ServerSession!, shelved, true);
            interest.SetInterest(ServerSession!, shelved, true);
            Assert.That(interest.GetForces(ServerSession!).Single(force => force.Identifier == shelved).InterestedPlayers, Is.EqualTo(1));
            interest.SetInterest(ServerSession!, scheduled, true);
            Assert.That(interest.GetForces(ServerSession!).Select(force => force.Identifier), Is.EqualTo(new[] { shelved }));
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(spawned, Is.Zero, "a scheduled force must wait for its arrival time");
            var interest = SEntMan.System<ForceInterestSystem>();
            interest.SetReady(scheduled);
            Assert.That(interest.GetForces(ServerSession!).Single(force => force.Identifier == scheduled).InterestedPlayers,
                Is.Zero, "interest sent before a force is due must be ignored");
            interest.SetInterest(ServerSession!, scheduled, true);
            interest.SetInterest(ServerSession!, scheduled, false);
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(spawned, Is.Zero, "withdrawing interest must prevent deployment");
            SEntMan.System<ForceInterestSystem>().SetInterest(ServerSession!, scheduled, true);
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(spawned, Is.EqualTo(1));
            Assert.That(SEntMan.System<ForceInterestSystem>().IsPending(shelved), Is.True);
        });
    }

    [Test]
    public async Task JoiningAnotherBodyRemovesInterestAndRestartClearsQueue()
    {
        var map = await Pair.CreateTestMap();
        uint id = 0;
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
            var interest = SEntMan.System<ForceInterestSystem>();
            id = interest.QueueForce("Waiting", new Dictionary<string, int> { ["TestForceInterestBody"] = 2 }, _ => true);
            interest.SetInterest(ServerSession!, id, true);
            Assert.That(interest.GetForces(ServerSession!).Single().InterestedPlayers, Is.EqualTo(1));
            Server.PlayerMan.SetAttachedEntity(ServerSession!, SEntMan.SpawnEntity(null, map.GridCoords));
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var interest = SEntMan.System<ForceInterestSystem>();
            Assert.That(interest.GetForces(ServerSession!).Single().InterestedPlayers, Is.Zero);
            interest.SetInterest(ServerSession!, id, true);
            Assert.That(interest.GetForces(ServerSession!).Single().InterestedPlayers, Is.Zero);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(interest.GetForces(ServerSession!), Is.Empty);
        });
    }

    [Test]
    public async Task UnclaimedBodiesDespawnAfter240SecondsButPreviouslyClaimedBodiesSurvive()
    {
        var map = await Pair.CreateTestMap();
        EntityUid unclaimed = default;
        EntityUid claimed = default;
        EntityUid prop = default;
        await Server.WaitAssertion(() =>
        {
            var interest = SEntMan.System<ForceInterestSystem>();
            unclaimed = SEntMan.SpawnEntity("TestForceInterestBody", map.GridCoords);
            claimed = SEntMan.SpawnEntity("TestForceInterestBody", map.GridCoords);
            prop = SEntMan.SpawnEntity("Chair", map.GridCoords);
            interest.TrackRole(unclaimed);
            interest.TrackRole(claimed);
            interest.TrackRole(prop);
            var mind = SEntMan.System<SharedMindSystem>();
            var occupant = mind.CreateMind(null);
            mind.TransferTo(occupant, claimed);
            mind.TransferTo(occupant, null);
            Assert.That(SEntMan.HasComponent<UnclaimedForceRoleComponent>(claimed), Is.False);
        });
        await RunSeconds(239);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(unclaimed), Is.True));
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(unclaimed), Is.False);
            Assert.That(SEntMan.EntityExists(claimed), Is.True);
            Assert.That(SEntMan.EntityExists(prop), Is.True);
        });
    }

    [Test]
    public async Task PendingForcesRemainVisibleWhenNoGhostBodiesHaveSpawned()
    {
        await Client.WaitAssertion(() =>
        {
            var window = new GhostRolesWindow();
            window.BeginEntryUpdate();
            window.AddForceEntry(new ForceInterestInfo(1, "Called marines", 10, 4, 7, true, true, true));
            window.EndEntryUpdate();
            Assert.That(window.FindControl<PanelContainer>("ContentPanel").Visible, Is.True);
            Assert.That(window.FindControl<Label>("NoRolesMessage").Visible, Is.False);
            var entry = window.FindControl<BoxContainer>("EntryContainer").Children.Single();
            Assert.That(entry.FindControl<Label>("Counts").Text, Does.Contain("10").And.Contain("4").And.Contain("7"));
            Assert.That(entry.FindControl<Button>("InterestButton").Text, Is.EqualTo(Loc.GetString("cmu-force-interest-withdraw")));
            window.Close();
        });
    }
}
