using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Expeditions;

[TestFixture]
public sealed class CMUIncomingFireMovementTest : GameTest
{
    [Test]
    public async Task RoutesAroundBendToEscapeUnseenFireBeforeResumingAssault()
    {
        var map = await Pair.CreateTestMap();
        EntityUid guard = default;
        await Server.WaitAssertion(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();
            for (var x = 0; x < 11; x++)
            for (var y = 0; y < 8; y++)
                tiles.Add((new Vector2i(x, y), map.Tile.Tile));
            Server.System<SharedMapSystem>().SetTiles(map.Grid, tiles);
            for (var x = 0; x < 11; x++)
            for (var y = 0; y < 8; y++)
                if (!(y == 2 && x is >= 1 and <= 9 || x == 1 && y is >= 2 and <= 6))
                    SEntMan.SpawnEntity("CMUExpeditionHull", new EntityCoordinates(map.Grid, x + 0.5f, y + 0.5f));
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", new EntityCoordinates(map.Grid, 7.5f, 2.5f));
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(Server.System<CMUExpeditionAgentSystem>().OrderAssault(guard,
                new EntityCoordinates(map.Grid, 9.5f, 2.5f)), Is.True);
            agent.LastSafePosition = new EntityCoordinates(map.Grid, 1.5f, 5.5f);
            agent.KnownFireLanes.Add(new CMUExpeditionFireLane(
                new EntityCoordinates(map.Grid, 1.5f, 2.5f), new EntityCoordinates(map.Grid, 10.5f, 2.5f),
                SGameTiming.CurTime + TimeSpan.FromSeconds(20), 12, 1.25f));
            Server.System<DamageableSystem>().TryChangeDamage(guard,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 5 } }, ignoreResistances: true);
        });
        await Pair.RunSeconds(8);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(SEntMan.GetComponent<TransformComponent>(guard).Coordinates.Y, Is.GreaterThan(3.5f),
                $"Complete the escape around the bend before retrying the dangerous order: {agent.IncomingFireDecision}, {agent.State}");
            Assert.That(agent.Target, Is.Null, "The route uses observed fire, not hidden shooter knowledge.");
        });
    }

    [Test]
    public async Task SpecialistDefenseSquadUsesCoveredAlternateApproach()
    {
        var map = await Pair.CreateTestMap();
        EntityUid marksman = default;
        await Server.WaitAssertion(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();
            for (var x = 0; x < 15; x++)
            for (var y = 0; y < 12; y++)
                tiles.Add((new Vector2i(x, y), map.Tile.Tile));
            Server.System<SharedMapSystem>().SetTiles(map.Grid, tiles);
            for (var y = 0; y < 12; y++)
                SEntMan.SpawnEntity("CMUExpeditionHull", new EntityCoordinates(map.Grid, 8.5f, y + 0.5f));
            var roles = new[] { CMUExpeditionCombatRole.Marksman, CMUExpeditionCombatRole.Support, CMUExpeditionCombatRole.Medic };
            for (var i = 0; i < roles.Length; i++)
            {
                var guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", new EntityCoordinates(map.Grid, 3.5f, 2.5f + i * 3));
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                agent.Squad = 42;
                agent.CombatRole = roles[i];
                agent.LastSeen = new EntityCoordinates(map.Grid, 12.5f, 2.5f);
                agent.LastContact = SGameTiming.CurTime;
                agent.ForgetAt = SGameTiming.CurTime + TimeSpan.FromSeconds(20);
                agent.NextGrenade = TimeSpan.MaxValue;
                agent.KnownDanger.Add(new CMUExpeditionDanger(new EntityCoordinates(map.Grid, 7.5f, 2.5f),
                    SGameTiming.CurTime + TimeSpan.FromSeconds(20), 16, 1.7f));
                if (i == 0)
                    marksman = guard;
            }
        });
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(marksman);
            Assert.That(agent.CornerFlanks, Is.GreaterThan(0),
                $"A specialist-only squad must still choose a safe alternate: {agent.CornerDecision}, {agent.State}");
            Assert.That(SEntMan.GetComponent<TransformComponent>(marksman).Coordinates.X, Is.LessThan(8),
                "An alternate approach does not authorize crossing the blocking wall.");
        });
    }

    [Test]
    public async Task EscapesUnseenFireBesideSquadmateInTwoWideHallway()
    {
        var map = await Pair.CreateTestMap();
        EntityUid guard = default;
        await Server.WaitAssertion(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();
            for (var x = 0; x < 12; x++)
            for (var y = 0; y < 4; y++)
                tiles.Add((new Vector2i(x, y), map.Tile.Tile));
            Server.System<SharedMapSystem>().SetTiles(map.Grid, tiles);
            for (var x = 0; x < 12; x++)
            {
                SEntMan.SpawnEntity("CMUExpeditionHull", new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
                SEntMan.SpawnEntity("CMUExpeditionHull", new EntityCoordinates(map.Grid, x + 0.5f, 3.5f));
            }
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", new EntityCoordinates(map.Grid, 5.5f, 1.5f));
            // A friendly body occupies the other lane, slightly behind. The gap is
            // physically passable but smaller than ordinary tactical cover spacing.
            var buddy = SEntMan.SpawnEntity("CMUExpeditionScavenger", new EntityCoordinates(map.Grid, 4.5f, 2.5f));
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(buddy).NextThink = TimeSpan.MaxValue;
            var other = SEntMan.SpawnEntity("CMUExpeditionScavenger", new EntityCoordinates(map.Grid, 6.5f, 2.5f));
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(other).NextThink = TimeSpan.MaxValue;
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            agent.Home = new EntityCoordinates(map.Grid, 5.5f, 1.5f);
            agent.HoldPosition = true;
            agent.KnownFireLanes.Add(new CMUExpeditionFireLane(
                new EntityCoordinates(map.Grid, 2.5f, 1.5f), new EntityCoordinates(map.Grid, 10.5f, 1.5f),
                SGameTiming.CurTime + TimeSpan.FromSeconds(10), 12, 1.25f));
            // Native damage wakes self-preservation without identifying an attacker.
            Server.System<DamageableSystem>().TryChangeDamage(guard,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 5 } }, ignoreResistances: true);
        });
        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(SEntMan.GetComponent<TransformComponent>(guard).Coordinates.Y, Is.GreaterThan(2.1f),
                $"Leave the observed firing lane without a visible shooter: {agent.IncomingFireDecision} / {agent.TrafficDecision}");
            Assert.That(agent.Target, Is.Null, "Damage must not grant sight of an unseen attacker.");
        });
    }
}
