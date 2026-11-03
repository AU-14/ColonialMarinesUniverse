#pragma warning disable RA0002 // Configure an isolated round and drive landings through the real event.

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Dropship;
using Content.Server.CMU14.Fob;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14;
using Content.Shared.GameTicking;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Spawners;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
[TestOf(typeof(CMULandingZoneTurretDropSystem))]
public sealed class CMULandingZoneTurretDropTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private const string Turret = "RMCTurretGaussSpaceborne";

    private EntityUid _map;
    private Entity<DropshipComponent> _ship;
    private EntityUid _destination;

    private SharedTransformSystem Transform => Server.System<SharedTransformSystem>();

    private void Setup(TimeSpan elapsed, string preset = "DistressSignal")
    {
        // The integration ticker intentionally ignores SetGamePreset.
        typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!
            .SetValue(Server.System<GameTicker>(), SProtoMan.Index<GamePresetPrototype>(preset));
        typeof(SharedGameTicker).GetProperty(nameof(SharedGameTicker.RoundStartTimeSpan))!
            .SetValue(Server.System<GameTicker>(), SGameTiming.CurTime - elapsed);

        var maps = Server.System<SharedMapSystem>();
        var options = DeserializationOptions.Default with { InitializeMaps = true, PauseMaps = true };
        Assert.That(Server.System<MapLoaderSystem>().TryLoadMap(
            new ResPath("/Maps/CMU14/Planets/LV624/LV624MultiZ0.yml"), out var map, out _, options), Is.True);
        _map = map!.Value.Owner;
        maps.SetPaused(_map, false);

        _destination = SEntMan.GetComponent<CMUFobMapComponent>(_map).Sites.First();

        // Mapped legacy turrets are irrelevant here; the cap is exercised explicitly.
        foreach (var legacy in OnSurface<CMUFobLegacyDefenseComponent>())
            SEntMan.DeleteEntity(legacy);

        var grid = maps.CreateGridEntity(SEntMan.GetComponent<MapComponent>(_map).MapId);
        _ship = (grid, SEntMan.AddComponent<DropshipComponent>(grid));
        SEntMan.AddComponent<ShipFactionComponent>(grid).Faction = "govfor";
    }

    private void Land()
    {
        _ship.Comp.Destination = _destination;
        var ev = new DropshipLandedOnPlanetEvent(_ship);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }

    private EntityUid[] OnSurface<T>() where T : Component
    {
        var entities = new List<EntityUid>();
        var query = SEntMan.AllEntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid == _map && !SEntMan.IsQueuedForDeletion(uid))
                entities.Add(uid);
        }

        return entities.ToArray();
    }

    private void AssertLifetime(TimeSpan expected)
    {
        foreach (var turret in OnSurface<CMULandingZoneTurretComponent>())
        {
            var remaining = SEntMan.GetComponent<TimedDespawnComponent>(turret).Lifetime;
            Assert.That(remaining, Is.EqualTo((float) expected.TotalSeconds).Within(60f));
        }
    }

    [Test]
    public async Task EarlyLandingDropsSixTurretsThatExpireAtTheHourMark()
    {
        await Server.WaitAssertion(() =>
        {
            Setup(TimeSpan.FromMinutes(10));
            Land();
            // Nothing lands until the airdrop sequence finishes.
            Assert.That(OnSurface<CMULandingZoneTurretComponent>(), Is.Empty);
        });
        await Pair.RunSeconds(15);
        await Server.WaitAssertion(() =>
        {
            var turrets = OnSurface<CMULandingZoneTurretComponent>();
            Assert.That(turrets, Has.Length.EqualTo(CMULandingZoneTurretDropSystem.MaxTurrets));
            Assert.That(turrets.Any(SEntMan.HasComponent<CMUFobLegacyDefenseComponent>), Is.False,
                "The FOB purge must not target the airdropped battery.");
            AssertLifetime(TimeSpan.FromMinutes(50));

            var center = Transform.GetWorldPosition(_destination);
            Assert.That(turrets.All(t => Vector2.Distance(Transform.GetWorldPosition(t), center) <= 20f), Is.True);

            // Later landings never add a second battery.
            Land();
        });
        await Pair.RunSeconds(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(OnSurface<CMULandingZoneTurretComponent>(),
                Has.Length.EqualTo(CMULandingZoneTurretDropSystem.MaxTurrets));
        });
    }

    [Test]
    public async Task LateLandingTurretsLastTwentyFiveMinutes()
    {
        await Server.WaitAssertion(() =>
        {
            Setup(TimeSpan.FromMinutes(70));
            Land();
        });
        await Pair.RunSeconds(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(OnSurface<CMULandingZoneTurretComponent>(),
                Has.Length.EqualTo(CMULandingZoneTurretDropSystem.MaxTurrets));
            AssertLifetime(TimeSpan.FromMinutes(25));
        });
    }

    [Test]
    public async Task ExistingLandingZoneTurretsCountTowardTheCap()
    {
        await Server.WaitAssertion(() =>
        {
            Setup(TimeSpan.FromMinutes(10));
            var coords = SEntMan.GetComponent<TransformComponent>(_destination).Coordinates;
            for (var i = 0; i < 4; i++)
                SEntMan.SpawnAtPosition(Turret, coords.Offset(new Vector2(i * 2, 0)));
            Land();
        });
        await Pair.RunSeconds(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(OnSurface<CMULandingZoneTurretComponent>(), Has.Length.EqualTo(2));
        });
    }

    [Test]
    public async Task OtherPresetsDoNotDropTurrets()
    {
        await Server.WaitAssertion(() =>
        {
            Setup(TimeSpan.FromMinutes(10), "ColonyFall");
            Land();
        });
        await Pair.RunSeconds(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(OnSurface<CMULandingZoneTurretComponent>(), Is.Empty);
        });
    }
}
