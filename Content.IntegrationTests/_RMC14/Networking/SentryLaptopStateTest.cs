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

}
