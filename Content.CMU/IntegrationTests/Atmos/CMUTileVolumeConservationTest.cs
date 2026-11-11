using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Atmos;
using Content.Shared.Atmos;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Atmos;

public sealed class CMUTileVolumeConservationTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/DeltaPressure/deltapressuretest.yml");

    [Test]
    [RunOnSide(Side.Server)]
    public void BlockingAndReopeningTileConservesGasAndHeat()
    {
        SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
        foreach (var tile in RelevantAtmos.Comp.Tiles.Values)
        {
            if (tile.Air is { Immutable: false } air)
                air.Clear();
        }

        var center = RelevantAtmos.Comp.Tiles[Vector2i.Zero].Air!;
        center.AdjustMoles(Gas.Nitrogen, 100);
        center.Temperature = 300;
        var neighbor = RelevantAtmos.Comp.Tiles[new Vector2i(0, 1)].Air!;
        neighbor.AdjustMoles(Gas.Nitrogen, 20);
        neighbor.Temperature = 600;

        float Moles() => RelevantAtmos.Comp.Tiles.Values.Where(t => t.Air is { Immutable: false }).Sum(t => t.Air!.TotalMoles);
        float Energy() => RelevantAtmos.Comp.Tiles.Values.Where(t => t.Air is { Immutable: false }).Sum(t => SAtmos.GetThermalEnergy(t.Air!));
        var energy = Energy();

        for (var cycle = 0; cycle < 3; cycle++)
        {
            var wall = SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(RelevantAtmos.Owner, Vector2.Zero));
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
            Assert.That(RelevantAtmos.Comp.Tiles[Vector2i.Zero].Air, Is.Null, "The wall must actually close the tested volume.");
            Assert.That(Moles(), Is.EqualTo(120).Within(0.01), "Closing the tile must displace its gas, not delete it.");
            Assert.That(Energy(), Is.EqualTo(energy).Within(energy * 0.001));
            SEntMan.DeleteEntity(wall);
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
            Assert.That(RelevantAtmos.Comp.Tiles[Vector2i.Zero].Air!.TotalMoles, Is.GreaterThan(0), "Reopening must refill the volume from its neighbors.");
            Assert.Multiple(() =>
            {
                Assert.That(Moles(), Is.EqualTo(120).Within(0.01), "Reopening must transfer existing gas, not copy it.");
                Assert.That(Energy(), Is.EqualTo(energy).Within(energy * 0.001), "Different-temperature donors must mix by heat capacity.");
            });
        }
    }
}
