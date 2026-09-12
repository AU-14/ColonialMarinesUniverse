using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Dropship.Integrity;
using Content.Server.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class GunshipTerrainAdoptionTest : InteractionTest
{
    [TestCase("CMUZLevelLadderThroughUp3")]
    [TestCase("CMUZLevelLadderThroughDown3")]
    [TestCase("RMCGrate")]
    [TestCase("RMCCatwalkHybrisaLattice")]
    [TestCase("RMCMonorailStraight")]
    public async Task NonblockingTerrainKeepsItsGroundPose(string prototype)
    {
        await SpawnTarget(prototype);
        await Server.WaitAssertion(() =>
        {
            var terrain = STarget!.Value;
            var terrainXform = SEntMan.GetComponent<TransformComponent>(terrain);
            Assert.That(terrainXform.Anchored, Is.True);
            var pose = new DropshipTerrainAnchorPose(terrainXform.LocalPosition, terrainXform.LocalRotation);
            var position = Transform.GetWorldPosition(terrain) - new Vector2(0.5f);

            var ship = MapSystem.CreateGridEntity(MapData.MapId);
            Transform.SetWorldPosition(ship.Owner, position + new Vector2(10f, 0f));
            MapSystem.SetTile(ship, Vector2i.Zero, new Tile(TileMan[Plating].TileId));
            var hover = SEntMan.AddComponent<DropshipTacticalHoverComponent>(ship.Owner);

            // Exercise the actual footprint probe: terrain without hard
            // fixtures must be recorded without becoming a flight blocker.
            var probe = typeof(DropshipTacticalLandSystem).GetMethod(
                "IsGunshipFootprintClear",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Entity<MapGridComponent>), typeof(EntityUid), typeof(Vector2), typeof(Angle) },
                null)!;
            Assert.That(probe.Invoke(Server.System<DropshipTacticalLandSystem>(),
                new object[] { ship, MapData.Grid.Owner, position, Angle.Zero }), Is.True);
            Assert.That(hover.FlightTerrainAnchors.TryGetValue(terrain, out var captured), Is.True);
            Assert.That(captured, Is.EqualTo(pose));

            // Simulate adoption, followed by translation and rotation, before
            // the guard runs. Restoration must use the captured ground pose.
            Transform.SetParent(terrain, ship.Owner);
            Transform.SetWorldPosition(ship.Owner, position + new Vector2(20f, 10f));
            Transform.SetWorldRotation(ship.Owner, Angle.FromDegrees(90));
            var passenger = SEntMan.SpawnEntity(null, new EntityCoordinates(ship.Owner, Vector2.Zero));
            Server.System<DropshipIntegritySystem>().GuardFlightAdoptions(
                ship.Owner, MapData.Grid.Owner, new HashSet<EntityUid>(), hover.FlightTerrainAnchors);

            Assert.Multiple(() =>
            {
                Assert.That(terrainXform.ParentUid, Is.EqualTo(MapData.Grid.Owner));
                Assert.That(terrainXform.LocalPosition, Is.EqualTo(pose.Position));
                Assert.That(terrainXform.LocalRotation, Is.EqualTo(pose.Rotation));
                Assert.That(SEntMan.GetComponent<TransformComponent>(passenger).ParentUid, Is.EqualTo(ship.Owner));
            });
            SEntMan.DeleteEntity(ship.Owner);
        });
    }
}
