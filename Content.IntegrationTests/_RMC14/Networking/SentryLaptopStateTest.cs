using Content.Shared._RMC14.Sentry.Laptop;
using Content.Shared._RMC14.Sentry;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._RMC14.Networking;

[TestFixture]
public sealed class SentryLaptopStateTest
{
    [Test]
    public async Task DeletingSentryCleansEveryLaptopIncludingAnAlreadyRemovedLink()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var sentry = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent<SentryComponent>(sentry);
            var first = entities.AddComponent<SentryLaptopComponent>(entities.SpawnEntity(null, MapCoordinates.Nullspace));
            var second = entities.AddComponent<SentryLaptopComponent>(entities.SpawnEntity(null, MapCoordinates.Nullspace));
            foreach (var laptop in new[] { first, second })
            {
                typeof(SentryLaptopComponent).GetField(nameof(laptop.SentryCustomNames))!.SetValue(laptop,
                    new Dictionary<EntityUid, string> { [sentry] = "Named sentry" });
            }
            typeof(SentryLaptopComponent).GetField(nameof(first.LinkedSentries))!.SetValue(first,
                new HashSet<EntityUid> { sentry });
            // The second laptop's link was already removed by the device-link system.
            entities.DeleteEntity(sentry);
            Assert.Multiple(() =>
            {
                Assert.That(first.LinkedSentries, Is.Empty);
                Assert.That(first.SentryCustomNames, Is.Empty);
                Assert.That(second.SentryCustomNames, Is.Empty);
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletedNamedSentriesDoNotBreakNetworkState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var owner = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var live = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var deleted = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var otherDeleted = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var laptop = entities.AddComponent<SentryLaptopComponent>(owner);
            typeof(SentryLaptopComponent).GetField(nameof(laptop.LinkedSentries))!.SetValue(laptop,
                new HashSet<EntityUid> { live, deleted, otherDeleted });
            typeof(SentryLaptopComponent).GetField(nameof(laptop.SentryCustomNames))!.SetValue(laptop,
                new Dictionary<EntityUid, string>
                {
                    [live] = "Working sentry", [deleted] = "Deleted sentry", [otherDeleted] = "Other deleted sentry",
                });
            typeof(SentryLaptopComponent).GetField(nameof(laptop.Watchers))!.SetValue(laptop, new List<EntityUid> { deleted });
            typeof(SentryLaptopComponent).GetField(nameof(laptop.CurrentCamera))!.SetValue(laptop, (EntityUid?) deleted);
            entities.DeleteEntity(deleted);
            entities.DeleteEntity(otherDeleted);

            // Exercise the actual PVS state-generation path, including the dictionary-key collision from the log.
            var state = entities.GetComponentState(entities.EventBus, laptop, null, GameTick.Zero)!;
            object Field(string name) => state.GetType().GetField(name)?.GetValue(state)
                                         ?? state.GetType().GetProperty(name)!.GetValue(state);
            Assert.Multiple(() =>
            {
                Assert.That((HashSet<NetEntity>) Field(nameof(laptop.LinkedSentries)),
                    Is.EquivalentTo(new[] { entities.GetNetEntity(live) }));
                var names = (Dictionary<NetEntity, string>) Field(nameof(laptop.SentryCustomNames));
                Assert.That(names, Has.Count.EqualTo(1));
                Assert.That(names[entities.GetNetEntity(live)], Is.EqualTo("Working sentry"));
                Assert.That((List<NetEntity>) Field(nameof(laptop.Watchers)), Is.Empty);
                Assert.That(Field(nameof(laptop.CurrentCamera)), Is.EqualTo(NetEntity.Invalid));
            });
        });
        await pair.CleanReturnAsync();
    }
}
