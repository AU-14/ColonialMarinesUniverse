using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Yautja;
using Content.Server.CMU14.Yautja;
using Content.Server.Gravity;
using Content.Shared._RMC14.Actions;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Gravity;
using Content.Shared.Throwing;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUYautjaLeapPresentationTest
{
    [TestCase(4f)]
    [TestCase(7f)]
    public async Task LeapReplicatesAnAirborneArcThenStrikesAndRestoresTheSprite(float distance)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        EntityUid hunter = default;
        EntityUid target = default;
        NetEntity hunterNet = default;
        Vector2 originalOffset = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var tiles = new List<(Vector2i, Tile)>();
            for (var x = -2; x <= 9; x++)
            for (var y = -2; y <= 2; y++)
                tiles.Add((new Vector2i(x, y), map.Tile.Tile));
            entities.System<SharedMapSystem>().SetTiles(map.Grid, tiles);
            entities.GetComponent<GravityComponent>(map.Grid).Inherent = true;
            entities.System<GravitySystem>().EnableGravity(map.Grid);
            hunter = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            target = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(distance, 0)));
            entities.EnsureComponent<YautjaComponent>(hunter);
            entities.EnsureComponent<YautjaBadBloodComponent>(hunter);
            entities.System<YautjaHonorboundAbilitiesSystem>().GrantActions((hunter, entities.GetComponent<YautjaComponent>(hunter)));
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), hunter);
            hunterNet = entities.GetNetEntity(hunter);
        });
        await pair.RunUntilSynced();
        await client.WaitPost(() => originalOffset = client.EntMan.GetComponent<SpriteComponent>(client.EntMan.GetEntity(hunterNet)).Offset);
        await server.WaitAssertion(() => Leap(server.EntMan, hunter, target));
        await pair.RunTicksSync(pair.SecondsToTicks(.2f));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<YautjaHuntingLeapingComponent>(hunter), Is.True,
                "The leap must still be airborne after the original blink-length flight would have ended.");
            Assert.That(server.EntMan.System<DamageableSystem>().GetTotalDamage(target), Is.EqualTo(FixedPoint2.Zero),
                "The strike must wait for the hunter to reach its target.");
        });
        await client.WaitAssertion(() =>
        {
            var localHunter = client.EntMan.GetEntity(hunterNet);
            Assert.That(client.EntMan.GetComponent<CMUYautjaMotionComponent>(localHunter).Kind, Is.EqualTo(CMUYautjaEffect.Leap));
            client.EntMan.System<CMUYautjaVisualsSystem>().FrameUpdate(0);
            Assert.That(client.EntMan.GetComponent<SpriteComponent>(localHunter).Offset.Y, Is.GreaterThan(originalOffset.Y),
                "The replicated flight must actually lift the composed player sprite.");
        });
        await pair.RunTicksSync(pair.SecondsToTicks(.65f));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<YautjaHuntingLeapingComponent>(hunter), Is.False);
            Assert.That(server.EntMan.System<DamageableSystem>().GetTotalDamage(target), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(server.EntMan.GetComponent<CMUYautjaMotionComponent>(hunter).Kind, Is.EqualTo(CMUYautjaEffect.Landing));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(.6f));
        await client.WaitAssertion(() =>
        {
            client.EntMan.System<CMUYautjaVisualsSystem>().FrameUpdate(0);
            Assert.That(client.EntMan.GetComponent<SpriteComponent>(client.EntMan.GetEntity(hunterNet)).Offset,
                Is.EqualTo(originalOffset), "Landing must restore the sprite, not leave a permanent airborne offset.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InterruptedFlightCanLeapAgainAndBeDeletedInFlight()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid hunter = default;
        EntityUid target = default;
        await pair.Server.WaitAssertion(() =>
        {
            hunter = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            target = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            entities.EnsureComponent<YautjaComponent>(hunter);
            entities.EnsureComponent<YautjaBadBloodComponent>(hunter);
            entities.System<YautjaHonorboundAbilitiesSystem>().GrantActions((hunter, entities.GetComponent<YautjaComponent>(hunter)));
            Leap(entities, hunter, target);
            entities.System<ThrownItemSystem>().StopThrow(hunter, entities.GetComponent<ThrownItemComponent>(hunter));
        });
        await pair.RunTicksSync(1);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<YautjaHuntingLeapingComponent>(hunter), Is.False);
            Leap(entities, hunter, target);
            Assert.That(entities.HasComponent<ThrownItemComponent>(hunter), Is.True);
            entities.DeleteEntity(hunter);
        });
        await pair.RunTicksSync(2);
        await pair.CleanReturnAsync();
    }

    private static void Leap(IEntityManager entities, EntityUid hunter, EntityUid target)
    {
        var action = entities.System<SharedRMCActionsSystem>().GetActionsWithEvent<YautjaHuntingLeapActionEvent>(hunter).Single();
        var leap = new YautjaHuntingLeapActionEvent { Performer = hunter, Target = target, Action = action };
        entities.EventBus.RaiseLocalEvent(hunter, leap);
        Assert.That(leap.Handled, Is.True);
    }
}
