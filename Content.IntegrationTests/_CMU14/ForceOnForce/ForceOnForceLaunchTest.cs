#pragma warning disable RA0002 // Arrange flight and preset state for end-to-end launch checks.
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.WeedKiller;
using Content.Shared.CMU14.Round;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._CMU14.ForceOnForce;

[TestFixture]
public sealed class ForceOnForceLaunchTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task LaunchesAfterThreeMinutesAndOpponentCanJoinTheSameDeparture(bool join)
    {
        var home = await Pair.CreateTestMap();
        var planet = await Pair.CreateTestMap();
        EntityUid first = default, second = default, firstConsole = default, secondConsole = default;
        EntityUid firstLz = default, secondLz = default;
        TimeSpan deadline = default;
        await Server.WaitAssertion(() =>
        {
            SetPreset("ForceOnForce");
            SEntMan.EnsureComponent<RMCPlanetComponent>(planet.MapUid);
            (first, firstConsole) = CreateDropship(home.MapId, "govfor", home.Tile.Tile, 0);
            (second, secondConsole) = CreateDropship(home.MapId, "opfor", home.Tile.Tile, 30);
            firstLz = SEntMan.SpawnEntity("CMDropshipDestination", planet.GridCoords);
            secondLz = SEntMan.SpawnEntity("CMDropshipDestination", planet.GridCoords.Offset(new Vector2(30, 0)));
            Fly(firstConsole, firstLz);
            var flight = SEntMan.GetComponent<FTLComponent>(first);
            deadline = SGameTiming.CurTime + TimeSpan.FromMinutes(3);
            Assert.That(flight.StateTime.End, Is.EqualTo(deadline));
            Assert.That(flight.State, Is.EqualTo(FTLState.Starting));
        });
        await Pair.RunSeconds(60);
        if (join)
        {
            await Server.WaitAssertion(() =>
            {
                Fly(secondConsole, secondLz);
                Assert.That(SEntMan.GetComponent<FTLComponent>(second).StateTime.End, Is.EqualTo(deadline));
                Assert.That(SEntMan.GetComponent<FTLComponent>(first).StateTime.End, Is.EqualTo(deadline),
                    "the second request must not restart the first side's timer");
            });
        }
        await Pair.RunSeconds(119);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FTLComponent>(first).State, Is.EqualTo(FTLState.Starting));
            if (join) Assert.That(SEntMan.GetComponent<FTLComponent>(second).State, Is.EqualTo(FTLState.Starting));
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FTLComponent>(first).State, Is.EqualTo(FTLState.Travelling));
            if (join)
                Assert.That(SEntMan.GetComponent<FTLComponent>(second).State, Is.EqualTo(FTLState.Travelling));
            else
            {
                Assert.That(SEntMan.HasComponent<FTLComponent>(second), Is.False);
                Fly(secondConsole, secondLz);
                Assert.That(SEntMan.GetComponent<FTLComponent>(second).StateTime.End,
                    Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromMinutes(3)), "a late launch starts a new window");
            }
        });
    }

    [TestCase("ForceOnForce", false, false)]
    [TestCase("ForceOnForce", true, true)]
    [TestCase("DistressSignal", true, false)]
    public async Task ReturnsHijacksAndOtherModesKeepTheirNormalStartup(string preset, bool toPlanet, bool hijack)
    {
        var home = await Pair.CreateTestMap();
        var target = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SetPreset(preset);
            if (toPlanet) SEntMan.EnsureComponent<RMCPlanetComponent>(target.MapUid);
            var (ship, console) = CreateDropship(home.MapId, "govfor", home.Tile.Tile, 0);
            var destination = SEntMan.SpawnEntity("CMDropshipDestination", target.GridCoords);
            Fly(console, destination, hijack);
            Assert.That(SEntMan.GetComponent<FTLComponent>(ship).StartupTime, Is.EqualTo(0.5f));
        });
    }

    [TestCase("ForceOnForce", 0)]
    [TestCase("DistressSignal", 1)]
    public async Task WeedkillerCanisterDeploymentIsDisabledOnlyInFof(string preset, int expected)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SetPreset(preset);
            var ship = SEntMan.SpawnEntity(null, map.GridCoords);
            Server.System<WeedKillerSystem>().CreateWeedKiller(ship, map.GridCoords);
            Assert.That(SEntMan.Count<WeedKillerComponent>(), Is.EqualTo(expected));
        });
    }

    private void SetPreset(string preset) => typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!
        .SetValue(Server.System<GameTicker>(), SProtoMan.Index<GamePresetPrototype>(preset));

    private (EntityUid Ship, EntityUid Console) CreateDropship(MapId mapId, string faction, Tile tile, int offset)
    {
        var maps = Server.System<SharedMapSystem>();
        var grid = maps.CreateGridEntity(mapId);
        maps.SetTile(grid, Vector2i.Zero, tile);
        Server.System<SharedTransformSystem>().SetLocalPosition(grid, new Vector2(offset, 0));
        SEntMan.EnsureComponent<ShuttleComponent>(grid);
        SEntMan.AddComponent<DropshipComponent>(grid);
        var console = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, new Vector2(0.5f)));
        SEntMan.AddComponent<DropshipNavigationComputerComponent>(console);
        SEntMan.AddComponent<WhitelistedShuttleComponent>(console).Faction = faction;
        return (grid, console);
    }

    private void Fly(EntityUid console, EntityUid destination, bool hijack = false) =>
        Assert.That(Server.System<SharedDropshipSystem>().FlyTo(
            (console, SEntMan.GetComponent<DropshipNavigationComputerComponent>(console)), destination, null,
            hijack: hijack, startupTime: 0.5f, hyperspaceTime: 60), Is.True);
}
