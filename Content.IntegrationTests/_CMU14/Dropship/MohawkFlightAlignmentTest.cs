using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkFlightAlignmentTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task UndersideStaysAlignedDuringFlight(bool client)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        NetEntity netShip = default;
        NetEntity netLower = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath("/Maps/CMU14/ShuttlesDropships/Mohawk/midway.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            netShip = entities.GetNetEntity(ship);
            netLower = entities.GetNetEntity(entities.GetComponent<MultiDeckDropshipComponent>(ship).Decks[-1]);
            var viewer = entities.SpawnEntity(null, new EntityCoordinates(ship, Vector2.Zero));
            pair.Server.PlayerMan.SetAttachedEntity(pair.Server.PlayerMan.Sessions.Single(), viewer);
            var destination = entities.SpawnEntity(null, new EntityCoordinates(maps.CreateMap(), 20, 20));
            entities.AddComponent<DropshipDestinationComponent>(destination);
            var nav = entities.QueryEntities<DropshipNavigationComputerComponent>()
                .Single(c => entities.GetComponent<TransformComponent>(c.Owner).GridUid == ship);
            Assert.That(entities.System<SharedDropshipSystem>().FlyTo((nav.Owner, nav), destination, null,
                startupTime: 0.5f, hyperspaceTime: 30f), Is.True);
        });

        await pair.RunSeconds(2);
        for (var sample = 0; sample < 5; sample++)
        {
            await pair.RunSeconds(0.2f);
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                Assert.That(entities.GetComponent<FTLComponent>(ship).State, Is.EqualTo(FTLState.Travelling));
                if (!client)
                    AssertAligned(entities, ship, entities.GetComponent<MultiDeckDropshipComponent>(ship).Decks[-1], "server");
            });
            if (!client)
                continue;
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                Assert.That(entities.TryGetEntity(netShip, out var clientShip), Is.True);
                Assert.That(entities.TryGetEntity(netLower, out var clientLower), Is.True);
                AssertAligned(entities, clientShip!.Value, clientLower!.Value, "client");
                // Check the rendered transforms between snapshots, not only tick boundaries.
                var timing = pair.Client.Timing;
                var remainder = timing.TickRemainder;
                try
                {
                    foreach (var fraction in new[] { 0.0, 0.5, 0.99 })
                    {
                        timing.TickRemainder = timing.TickPeriod * fraction;
                        entities.System<Robust.Client.GameObjects.TransformSystem>().FrameUpdate(0f);
                        AssertAligned(entities, clientShip.Value, clientLower.Value, $"client frame {fraction}");
                    }
                }
                finally
                {
                    timing.TickRemainder = remainder;
                }
            });
        }

        await pair.Server.WaitPost(() => pair.Server.EntMan.DeleteEntity(ship));
        await pair.CleanReturnAsync();
    }

    private static void AssertAligned(IEntityManager entities, EntityUid ship, EntityUid lower, string side)
    {
        var transform = entities.System<SharedTransformSystem>();
        var position = transform.GetWorldPosition(ship);
        var underside = transform.GetWorldPosition(lower);
        var cabinVelocity = entities.GetComponent<PhysicsComponent>(ship).LinearVelocity;
        var lowerVelocity = entities.GetComponent<PhysicsComponent>(lower).LinearVelocity;
        Assert.That(Vector2.Distance(position, underside), Is.LessThan(0.05f),
            $"{side}: cabin {position}, underside {underside}; velocities {cabinVelocity} / {lowerVelocity}");
    }
}
