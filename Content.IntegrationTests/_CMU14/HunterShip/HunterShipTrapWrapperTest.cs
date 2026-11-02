using System.Linq;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.CMU14.HunterShip;

[TestFixture]
public sealed class HunterShipTrapWrapperTest
{
    // one of the eight pixel-offset traps mapped on both hunter ship decks
    private const string MappedTrap = "CMUHunterShipPlacedCMUYautjaHuntingTrapYauttrap0SouthOffset9x9";

    [Test]
    public async Task MappedHuntingTrapCanBeSteppedIntoAndShowsArmed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid trap = default;
        EntityUid hunter = default;
        NetEntity trapNet = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                trap = entMan.SpawnEntity(MappedTrap, map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                trapNet = entMan.GetNetEntity(trap);

                // a mob only walks into the trap if one of its fixtures sits on a layer the mob's mask hits
                var mobMask = entMan.GetComponent<FixturesComponent>(hunter).Fixtures.Values
                    .Aggregate(0, (mask, fixture) => mask | fixture.CollisionMask);
                var trapFixtures = entMan.GetComponent<FixturesComponent>(trap).Fixtures.Values;
                Assert.That(trapFixtures.Any(fixture => (fixture.CollisionLayer & mobMask) != 0), Is.True,
                    "a mapped trap no mob collides with can never be stepped into");

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(entMan.System<YautjaTrapSystem>().TryArmTrap((trap, trapComp), hunter), Is.True);
            });

            await pair.RunTicksSync(10);

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                Assert.That(entMan.TryGetEntity(trapNet, out var clientTrap), Is.True);

                var sprites = entMan.System<SpriteSystem>();
                var sprite = entMan.GetComponent<SpriteComponent>(clientTrap!.Value);
                Assert.That(sprites.TryGetLayer((clientTrap.Value, sprite), "base", out var layer, false), Is.True);
                Assert.That(layer!.State.Name, Is.EqualTo("yauttrap1"), "an armed mapped trap shows the armed sprite");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var uid in new[] { trap, hunter })
                {
                    if (uid != default && !server.EntMan.Deleted(uid))
                        server.EntMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }
}
