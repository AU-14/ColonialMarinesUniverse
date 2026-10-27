#pragma warning disable RA0002 // Controlled live permission changes exercise the alert's action checks.
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Alert;
using Content.Shared.CMU14.ZLevels.Ordnance;
using Content.Shared.Coordinates;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Areas;

[TestFixture]
public sealed class CMUAreaInfoRoofingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task RoofPermissionsStayLiveAcrossOverlapsRangeAndMapChanges()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var areas = Server.System<AreaSystem>();
            var system = Server.System<AreaInfoSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var ordnance = Server.System<CMUTopDownOrdnanceSystem>();
            var grid = SEntMan.AddComponent<AreaGridComponent>(map.Grid.Owner);
            areas.ReplaceArea(grid, Vector2i.Zero, "RMCAreaBosenmoriBashoOob");
            var permissions = SEntMan.GetComponent<AreaComponent>(grid.AreaEntities.Values.Single());
            permissions.CAS = permissions.SupplyDrop = permissions.MortarFire = permissions.MortarPlacement = true;
            permissions.Lasing = permissions.Medevac = permissions.Paradropping = permissions.OB = true;
            var actor = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<AlertsComponent>(actor);
            var info = SEntMan.AddComponent<AreaInfoComponent>(actor);

            AreaInfoRestrictionState Refresh()
            {
                system.SetNextUpdateTime((actor, info), SGameTiming.CurTime);
                system.Update(0);
                system.Update(0);
                var actual = info.LastRestrictionState!.Value;
                var coordinates = actor.ToCoordinates();
                var world = transform.ToMapCoordinates(coordinates);
                Assert.Multiple(() =>
                {
                    Assert.That(actual.CloseAirSupport, Is.EqualTo(areas.CanCAS(coordinates)));
                    Assert.That(actual.MortarPlacement, Is.EqualTo(areas.CanMortarPlacement(coordinates)));
                    Assert.That(actual.LaserDesignation, Is.EqualTo(areas.CanLase(coordinates)));
                    Assert.That(actual.Medevac, Is.EqualTo(areas.CanMedevac(coordinates)));
                    Assert.That(actual.Paradropping, Is.EqualTo(areas.CanParadrop(coordinates)));
                    Assert.That(actual.SupplyDrops, Is.EqualTo(areas.CanSupplyDrop(world)));
                    Assert.That(actual.OrbitalBombardment, Is.EqualTo(ordnance.TryResolveImpactColumn(world, CMUTopDownOrdnanceKind.OrbitalBombardment, out _)));
                    Assert.That(actual.MortarFire, Is.EqualTo(ordnance.TryResolveImpactColumn(world, CMUTopDownOrdnanceKind.Mortar, out _)));
                });
                return actual;
            }

            Entity<RoofingEntityComponent> Roof(float range)
            {
                var uid = SEntMan.SpawnEntity(null,
                    new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(range, 0)));
                var roof = SEntMan.AddComponent<RoofingEntityComponent>(uid);
                roof.Range = range;
                roof.CanCAS = roof.CanMortarPlace = roof.CanMortarFire = roof.CanOrbitalBombard = true;
                roof.CanLase = roof.CanMedevac = roof.CanParadrop = roof.CanSupplyDrop = true;
                return (uid, roof);
            }

            Assert.That(Refresh().CloseAirSupport, Is.True);
            var pylon = Roof(5);
            pylon.Comp.CanCAS = pylon.Comp.CanLase = false;
            var core = Roof(11);
            core.Comp.CanOrbitalBombard = core.Comp.CanMedevac = false;
            var covered = Refresh();
            Assert.Multiple(() =>
            {
                Assert.That(covered.CloseAirSupport, Is.False, "A roof at exactly its range still covers the actor.");
                Assert.That(covered.LaserDesignation, Is.False);
                Assert.That(covered.Medevac, Is.False, "Overlapping roofs contribute independently.");
                Assert.That(covered.MortarPlacement, Is.True);
                Assert.That(covered.Paradropping, Is.True);
                Assert.That(covered.HiveCoreProtection && covered.PylonProtection, Is.True);
            });

            pylon.Comp.CanCAS = true;
            var changed = Refresh();
            Assert.That(changed.CloseAirSupport, Is.True, "Permissions can change without player movement.");
            Assert.That(changed.PylonProtection, Is.False);
            SEntMan.RemoveComponent<RoofingEntityComponent>(core);
            Assert.That(Refresh().Medevac, Is.True);
            Assert.That(info.LastRestrictionState!.Value.HiveCoreProtection, Is.False);

            var otherMap = Server.System<SharedMapSystem>().CreateMap(out _);
            transform.SetCoordinates(pylon, new EntityCoordinates(otherMap, transform.GetMapCoordinates(actor).Position));
            Assert.That(Refresh().LaserDesignation, Is.True, "A roof on another map must not block local actions.");
            transform.SetCoordinates(pylon, new EntityCoordinates(map.GridCoords.EntityId,
                map.GridCoords.Position + new Vector2(5.01f, 0)));
            Assert.That(Refresh().LaserDesignation, Is.True, "Just outside the range is uncovered.");
            transform.SetCoordinates(pylon, map.GridCoords);
            Assert.That(Refresh().LaserDesignation, Is.False);
            SEntMan.DeleteEntity(pylon);
            Assert.That(Refresh().LaserDesignation, Is.True);

            permissions.MortarPlacement = permissions.Paradropping = false;
            var denied = Refresh();
            Assert.That(denied.MortarPlacement || denied.Paradropping, Is.False, "Area restrictions still apply without roofs.");
        });
    }
}
