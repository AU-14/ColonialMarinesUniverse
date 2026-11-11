using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.CMU14.Marines;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUNestedTurbulenceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task LaunchTurbulenceLeavesNestedPrisonersAtTheirNest()
    {
        var map = await Pair.CreateTestMap();
        EntityUid prisoner = default;
        EntityUid passenger = default;
        Vector2 prisonerStart = default;
        Vector2 passengerStart = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(map.Grid);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = -4; x <= 4; x++)
            for (var y = -4; y <= 4; y++)
                maps.SetTile(map.Grid, grid, new Vector2i(x, y), floor);

            prisoner = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            passenger = SSpawnAtPosition("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            SEntMan.EnsureComponent<XenoNestedComponent>(prisoner);
            var standing = SEntMan.System<StandingStateSystem>();
            standing.Stand(prisoner, force: true);
            standing.Stand(passenger, force: true);
            var transform = SEntMan.System<SharedTransformSystem>();
            prisonerStart = transform.GetWorldPosition(prisoner);
            passengerStart = transform.GetWorldPosition(passenger);

            SEntMan.EnsureComponent<WarshipComponent>(map.Grid);
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            // This fixture exercises flight turbulence without an engine FTL destination.
            SEntMan.RemoveComponent<ShuttleComponent>(map.Grid);
            var ftl = SEntMan.EnsureComponent<FTLComponent>(map.Grid);
            ftl.State = FTLState.Starting;
            ftl.StateTime = new(SGameTiming.CurTime, SGameTiming.CurTime);
        });

        // The launch lurch is followed by a guaranteed heavy turbulence bout after three seconds.
        await Pair.RunTicksSync(330);
        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            Assert.That(transform.GetWorldPosition(prisoner), Is.EqualTo(prisonerStart),
                "nest collision suppression must not let turbulence move a prisoner through the hull");
            Assert.That(Vector2.Distance(transform.GetWorldPosition(passenger), passengerStart), Is.GreaterThan(0.05f),
                "the same flight must still throw an ordinary unrestrained passenger");
            Assert.That(SEntMan.HasComponent<XenoNestedComponent>(prisoner), Is.True,
                "flight must not silently release the prisoner to restore collision");
        });
    }
}
