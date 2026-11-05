using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Climbing;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.DoAfter;
using Content.Shared.Damage.Systems;
using Content.Shared.Atmos;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Climbing;

public sealed class CMUWallClimbTest : GameTest
{
    [Test]
    public async Task UndergroundClimbWaitsThenMovesExactlyOneLevel()
    {
        var scene = await CreateScene(-2);
        Content.Shared.DoAfter.DoAfter pending = default!;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<CMUWallClimbSystem>().TryStartClimb(scene.User, scene.Wall), Is.True);
            pending = SComp<DoAfterComponent>(scene.User).DoAfters.Values.Single();
        });

        await RunSeconds(14);
        await Server.WaitAssertion(() =>
        {
            Assert.That(pending.Cancelled, Is.False,
                $"Cancelled at {pending.CancelledTime}; position {SComp<TransformComponent>(scene.User).Coordinates}; " +
                $"damage {Server.System<DamageableSystem>().GetTotalDamage(scene.User)}");
            Assert.That(SComp<TransformComponent>(scene.User).MapUid, Is.EqualTo(scene.Lower));
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<TransformComponent>(scene.User).MapUid, Is.EqualTo(scene.Upper));
            Assert.That(Server.System<SharedTransformSystem>().GetWorldPosition(scene.User),
                Is.EqualTo(new Vector2(1.5f, 0.5f)).Using<Vector2>((a, b) => a.EqualsApprox(b)));
            SEntMan.DeleteEntity(scene.Network);
        });
    }

    [Test]
    public async Task GroundLevelCannotStartWallClimb()
    {
        var scene = await CreateScene(0);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<CMUWallClimbSystem>().TryStartClimb(scene.User, scene.Wall), Is.False);
            SEntMan.DeleteEntity(scene.Network);
        });
    }

    [Test]
    public async Task CeilingBlocksStartAndNewLandingWallBlocksCompletion()
    {
        var scene = await CreateScene(-1);
        Content.Shared.DoAfter.DoAfter pending = default!;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var climb = Server.System<CMUWallClimbSystem>();
            maps.SetTile(scene.UpperGrid, SComp<MapGridComponent>(scene.UpperGrid), Vector2i.Zero, scene.Floor);
            Assert.That(climb.TryStartClimb(scene.User, scene.Wall), Is.False,
                "wall climbing must not pass through a ceiling above the user");
            maps.SetTile(scene.UpperGrid, SComp<MapGridComponent>(scene.UpperGrid), Vector2i.Zero, Tile.Empty);
            Assert.That(climb.TryStartClimb(scene.User, scene.Wall), Is.True);
            pending = SComp<DoAfterComponent>(scene.User).DoAfters.Values.Single();
            SSpawnAtPosition("WallSolid", new EntityCoordinates(scene.UpperGrid, new Vector2(1.5f, 0.5f)));
        });

        await RunSeconds(16);
        await Server.WaitAssertion(() =>
        {
            Assert.That(pending.Completed, Is.True, "the changed landing must be checked at completion");
            Assert.That(SComp<TransformComponent>(scene.User).MapUid, Is.EqualTo(scene.Lower));
            SEntMan.DeleteEntity(scene.Network);
        });
    }

    private async Task<(EntityUid User, EntityUid Wall, EntityUid Lower, EntityUid Upper,
        EntityUid UpperGrid, Tile Floor, EntityUid Network)> CreateScene(int depth)
    {
        var lower = await Pair.CreateTestMap();
        var upper = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid wall = default;
        EntityUid network = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var levels = Server.System<CMUZLevelsSystem>();
            // The default test maps are vacuum; keep environmental damage from
            // interrupting a stationary climber during the full 15-second action.
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            var atmos = Server.System<AtmosphereSystem>();
            atmos.SetMapAtmosphere(lower.MapUid, false, new GasMixture(moles, Atmospherics.T20C));
            atmos.SetMapAtmosphere(upper.MapUid, false, new GasMixture(moles, Atmospherics.T20C));
            var createdNetwork = levels.CreateZNetwork();
            network = createdNetwork;
            Assert.That(levels.TryAddMapsIntoZNetwork(createdNetwork,
                new() { [lower.MapUid] = depth, [upper.MapUid] = depth + 1 }), Is.True);
            maps.SetTile(lower.Grid, new Vector2i(1, 0), lower.Tile.Tile);
            maps.SetTile(upper.Grid, new Vector2i(1, 0), upper.Tile.Tile);
            maps.SetTile(upper.Grid, Vector2i.Zero, Tile.Empty);
            user = SSpawnAtPosition("CMMobHuman", new EntityCoordinates(lower.Grid.Owner, new Vector2(0.5f)));
            wall = SSpawnAtPosition("WallSolid", new EntityCoordinates(lower.Grid.Owner, new Vector2(1.5f, 0.5f)));
        });
        await RunSeconds(1);
        // RMC removes input movers from unoccupied humans. Exercise a player-controlled climber.
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, user));
        return (user, wall, lower.MapUid, upper.MapUid, upper.Grid.Owner, upper.Tile.Tile, network);
    }
}
