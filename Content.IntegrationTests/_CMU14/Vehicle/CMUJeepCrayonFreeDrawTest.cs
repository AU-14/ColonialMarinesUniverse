using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Charges.Systems;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Crayon;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A crayon set to draw freely and dragged across a jeep leaves a line of dots on its body, using a
/// charge for every so many; drawing the same line again adds nothing, and cleaning takes it off.
/// </summary>
[TestFixture]
public sealed class CMUJeepCrayonFreeDrawTest : GameTest
{
    [Test]
    public async Task DraggedCrayonDrawsALineOnTheBody()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid user = default;
        EntityUid crayon = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.5f));
            crayon = SEntMan.SpawnEntity("CrayonWhite", new EntityCoordinates(map.Grid, 0.5f, 2.5f));
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<SharedCMUVehicleCrayonSystem>();
            var charges = SEntMan.System<SharedChargesSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var comp = SEntMan.GetComponent<CMUVehicleCrayonComponent>(jeep);
            var free = SEntMan.EnsureComponent<CMUCrayonFreeDrawComponent>(crayon);
            Entity<CrayonComponent, CMUCrayonFreeDrawComponent> held = (crayon, SEntMan.GetComponent<CrayonComponent>(crayon), free);

            // Dragged straight across the jeep as it is drawn facing south.
            var line = Enumerable.Range(0, 9)
                .Select(i => transform.ToMapCoordinates(new EntityCoordinates(map.Grid, -0.3f + i * 0.2f, 0.4f)).Position)
                .ToList();

            var before = charges.GetCurrentCharges(crayon);
            var added = system.Draw((jeep, comp), user, held, line, true);
            Assert.That(added, Is.GreaterThan(comp.DotsPerCharge), "The line should cover the body.");
            Assert.That(comp.Dots, Has.Count.EqualTo(added));
            Assert.That(comp.Dots.Select(d => d.Face).Distinct().Count(), Is.GreaterThan(1), "It runs over top faces and the front.");
            Assert.That(charges.GetCurrentCharges(crayon), Is.EqualTo(before - added / comp.DotsPerCharge));

            Assert.That(system.Draw((jeep, comp), user, held, line, false), Is.Zero, "Going over it again adds nothing.");

            SEntMan.System<CMUVehicleGrimeSystem>().Clean((jeep, SEntMan.GetComponent<CMUVehicleGrimeComponent>(jeep)), 1f);
            Assert.That(comp.Dots, Is.Empty, "Cleaning takes the drawing off.");
        });
    }
}
