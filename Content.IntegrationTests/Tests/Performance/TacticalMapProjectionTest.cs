#pragma warning disable RA0002 // Exercise publication and direct edits of authorized source dictionaries.
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.TacticalMap;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class TacticalMapProjectionTest : GameTest
{
    [Test]
    public async Task StableProjectionKeepsRevisionAndPayloadButDetectsDirectSourceEdits()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity(null, map.GridCoords);
            var computer = SEntMan.AddComponent<TacticalMapComputerComponent>(uid);
            computer.Faction = SharedTacticalMapSystem.GovforFaction;
            var source = new TacticalMapComponent();
            source.GovforBlips[1234] = new TacticalMapBlip { Indices = new(1, 2), Color = Color.Cyan };
            source.GovforLabels[new(1, 2)] = "First";
            var system = Server.System<TacticalMapSystem>();
            var method = typeof(SharedTacticalMapSystem).GetMethod("UpdateMapData",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(Entity<TacticalMapComputerComponent>), typeof(TacticalMapComponent) }, null)!;
            void Publish() => method.Invoke(system, new object[] { new Entity<TacticalMapComputerComponent>(uid, computer), source });
            Publish();
            var first = computer.Blips;
            var revision = computer.BlipRevision;
            var labels = SEntMan.GetComponent<TacticalMapLabelsComponent>(uid);
            var oldLabels = labels.GovforLabels;
            Publish();
            Assert.That(computer.Blips, Is.SameAs(first));
            Assert.That(computer.BlipRevision, Is.EqualTo(revision));
            Assert.That(labels.GovforLabels, Is.SameAs(oldLabels));
            Assert.That(labels.GovforLabels, Is.Not.SameAs(source.GovforLabels));
            source.GovforBlips[1234] = source.GovforBlips[1234] with { Indices = new(7, 8) };
            source.GovforLabels[new(1, 2)] = "Second";
            Publish();
            Assert.That(computer.BlipRevision, Is.EqualTo(revision + 1));
            Assert.That(computer.Blips[1234].Indices, Is.EqualTo(new Vector2i(7, 8)));
            Assert.That(first[1234].Indices, Is.EqualTo(new Vector2i(1, 2)));
            Assert.That(labels.GovforLabels[new(1, 2)], Is.EqualTo("Second"));
            Assert.That(oldLabels[new(1, 2)], Is.EqualTo("First"));
            computer.Faction = SharedTacticalMapSystem.MarinesFaction;
            Publish();
            Assert.That(computer.Blips, Is.Empty);
            Assert.That(labels.GovforLabels, Is.Empty);
            Assert.That(computer.BlipRevision, Is.EqualTo(revision + 2));
            SEntMan.DeleteEntity(uid);
        });
    }
}
