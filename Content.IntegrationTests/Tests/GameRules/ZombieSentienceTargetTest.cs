using Content.Server.StationEvents.Components;
using Content.Server.Zombies;
using Content.Shared.Zombies;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
[TestOf(typeof(ZombieSystem))]
public sealed class ZombieSentienceTargetTest
{
    [Test]
    public async Task ZombificationRemovesSentienceEligibility()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var zombies = server.System<ZombieSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var monkey = entMan.SpawnEntity("MobMonkey", map.GridCoords);
            Assert.That(entMan.HasComponent<SentienceTargetComponent>(monkey), Is.True);

            zombies.ZombifyEntity(monkey);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<ZombieComponent>(monkey), Is.True);
                Assert.That(entMan.HasComponent<SentienceTargetComponent>(monkey), Is.False);
            });

            entMan.DeleteEntity(monkey);
        });

        await pair.CleanReturnAsync();
    }
}
