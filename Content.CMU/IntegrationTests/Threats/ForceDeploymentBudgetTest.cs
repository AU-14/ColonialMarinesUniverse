#pragma warning disable RA0002 // Drive one simulation update at a time to verify the deployment boundary.

using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.Threats;
using Content.Shared.CMU14.Threats;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Threats;

public sealed class ForceDeploymentBudgetTest : GameTest
{
    private static readonly ProtoId<ThirdPartyPrototype> Party = "CMUTestBudgetParty";
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestDeploymentBody
        - type: partySpawn
          id: CMUTestBudgetSpawn
          leadersToSpawn: {}
          entsToSpawn:
            CMUTestDeploymentBody: 3
        - type: thirdParty
          id: CMUTestBudgetParty
          partyspawn: CMUTestBudgetSpawn
          entrymethod: ground
          announcearrival: ""
        - type: partySpawn
          id: CMUTestMissingShuttleMarkers
          leadersToSpawn:
            CMUTestDeploymentBody: 1
          Markers:
            Leader: CMUTestNonexistentMarker
        - type: thirdParty
          parent: MissionariesParty
          id: CMUTestRejectedShuttle
          partyspawn: CMUTestMissingShuttleMarkers
          announcearrival: ""
        """;

    [Test]
    public async Task DeploymentSpansUpdatesAndCompletesWithoutDuplicateBodies()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            for (var i = 0; i < 3; i++)
                SEntMan.SpawnEntity("thirdpartyentityspawnmarker", map.GridCoords);
            QueueParty();
            var forces = SEntMan.System<ForceInterestSystem>();
            forces.Update(0f);
            Assert.That(BodyCount(), Is.Zero, "preparation must yield before the first body");
            for (var expected = 1; expected <= 3; expected++)
            {
                forces.Update(0f);
                Assert.That(BodyCount(), Is.EqualTo(expected), "one body may spawn per update");
            }
            for (var i = 0; i < 5; i++)
                forces.Update(0f);
            Assert.That(BodyCount(), Is.EqualTo(3), "completion must not restart the deployment");

            for (var i = 0; i < 3; i++)
                SEntMan.SpawnEntity("thirdpartyentityspawnmarker", map.GridCoords);
            var party = Server.ProtoMan.Index(Party);
            Assert.That(SEntMan.System<ThirdPartySystem>().SpawnThirdPartyImmediately(
                party, Server.ProtoMan.Index(party.PartySpawn)), Is.True);
            Assert.That(BodyCount(), Is.EqualTo(6), "explicit immediate spawning must still finish synchronously");
        });
    }

    [Test]
    public async Task RoundCleanupCancelsTheRestOfAPartialDeployment()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.SpawnEntity("thirdpartyentityspawnmarker", map.GridCoords);
            QueueParty();
            var forces = SEntMan.System<ForceInterestSystem>();
            forces.Update(0f);
            forces.Update(0f);
            Assert.That(BodyCount(), Is.EqualTo(1));
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            for (var i = 0; i < 5; i++)
                forces.Update(0f);
            Assert.That(BodyCount(), Is.EqualTo(1), "old-round work must not create bodies after cleanup");
        });
    }

    [Test]
    public async Task MissingMarkersRetryWithoutDuplicatingAnAcceptedDeployment()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            QueueParty();
            SEntMan.System<ForceInterestSystem>().Update(0f);
            Assert.That(BodyCount(), Is.Zero);
            SEntMan.SpawnEntity("thirdpartyentityspawnmarker", map.GridCoords);
        });
        await RunSeconds(32);
        await Server.WaitAssertion(() => Assert.That(BodyCount(), Is.EqualTo(3)));
        await RunSeconds(32);
        await Server.WaitAssertion(() => Assert.That(BodyCount(), Is.EqualTo(3)));
    }

    private void QueueParty()
    {
        var party = Server.ProtoMan.Index(Party);
        SEntMan.System<ThirdPartySystem>().SpawnThirdParty(party, Server.ProtoMan.Index(party.PartySpawn), false);
        typeof(ForceInterestSystem).GetField("_nextRefresh", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(SEntMan.System<ForceInterestSystem>(), TimeSpan.Zero);
    }

    private int BodyCount() => SEntMan.EntityQuery<MetaDataComponent>()
        .Count(meta => meta.EntityPrototype?.ID == "CMUTestDeploymentBody");

    [Test]
    public async Task RejectedShuttlePreparationDoesNotRetainMapsOrGridsOnRetry()
    {
        await Server.WaitAssertion(() =>
        {
            var beforeMaps = SEntMan.EntityQuery<MapComponent>().Select(map => map.Owner).ToHashSet();
            var beforeGrids = SEntMan.EntityQuery<MapGridComponent>().Select(grid => grid.Owner).ToHashSet();
            ProtoId<ThirdPartyPrototype> id = "CMUTestRejectedShuttle";
            var party = Server.ProtoMan.Index(id);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(SEntMan.System<ThirdPartySystem>().SpawnThirdPartyImmediately(
                    party, Server.ProtoMan.Index(party.PartySpawn), overrideDropship: true), Is.False);
                Assert.That(SEntMan.EntityQuery<MapComponent>().Select(map => map.Owner), Is.EquivalentTo(beforeMaps));
                Assert.That(SEntMan.EntityQuery<MapGridComponent>().Select(grid => grid.Owner), Is.EquivalentTo(beforeGrids));
            }
        });
    }
}
