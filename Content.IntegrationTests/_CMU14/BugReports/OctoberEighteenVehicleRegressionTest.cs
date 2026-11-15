using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Dropship.TacticalLand;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.Radio;
using Content.Shared.Interaction;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberEighteenVehicleRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    // nests aren't hard so the turf check let ships land on them and gib the hosts, and other grids on the
    // landing map (a landed mohawk, another dropship) were never looked at
    [Test]
    public async Task TacticalLandingIsBlockedByNestsAndOtherGrids()
    {
        var map = await Pair.CreateTestMap();
        EntityUid eye = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMFloorSteel"].TileId);
            for (var x = -2; x <= 3; x++)
            for (var y = -2; y <= 2; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), floor);

            // landing requires an OB-allowed area since level 4 areas were gated; a bare grid has none
            var areas = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            for (var x = -2; x <= 3; x++)
            for (var y = -2; y <= 2; y++)
                SEntMan.System<AreaSystem>().ReplaceArea(areas, new Vector2i(x, y), "RMCAreaKutjevoExterior");

            // two tile ship parked on its own map
            maps.CreateMap(out var shipMap);
            var ship = maps.CreateGridEntity(shipMap);
            maps.SetTile(ship, Vector2i.Zero, floor);
            maps.SetTile(ship, new Vector2i(1, 0), floor);
            SEntMan.EnsureComponent<DropshipComponent>(ship);
            var console = SEntMan.SpawnEntity("CMComputerDropshipNavigationERTThirdParty",
                new EntityCoordinates(ship, 0.5f, 0.5f));

            eye = SEntMan.SpawnEntity("CMUDropshipPilotEye", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var pilotEye = SEntMan.EnsureComponent<DropshipPilotEyeComponent>(eye);
            typeof(DropshipPilotEyeComponent).GetField(nameof(DropshipPilotEyeComponent.Console))!
                .SetValue(pilotEye, (EntityUid?) console);
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<DropshipPilotEyeComponent>(eye).ClearForLanding, Is.True));

        EntityUid nest = default;
        await Server.WaitAssertion(() =>
            nest = SEntMan.SpawnEntity("XenoNest", new EntityCoordinates(map.Grid, 1.5f, 0.5f)));
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var pilotEye = SEntMan.GetComponent<DropshipPilotEyeComponent>(eye);
            Assert.That(pilotEye.ClearForLanding, Is.False);
            Assert.That(pilotEye.BlockedTiles, Does.Contain(new Vector2i(1, 0)));
            SEntMan.DeleteEntity(nest);
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DropshipPilotEyeComponent>(eye).ClearForLanding, Is.True);

            var maps = SEntMan.System<SharedMapSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMFloorSteel"].TileId);
            var parked = maps.CreateGridEntity(map.MapId);
            SEntMan.System<SharedTransformSystem>().SetWorldPosition(parked,
                SEntMan.System<SharedTransformSystem>().GetWorldPosition(map.Grid));
            maps.SetTile(parked, Vector2i.Zero, floor);
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var pilotEye = SEntMan.GetComponent<DropshipPilotEyeComponent>(eye);
            Assert.That(pilotEye.ClearForLanding, Is.False);
            Assert.That(pilotEye.BlockedTiles, Does.Contain(Vector2i.Zero));
        });
    }
}
