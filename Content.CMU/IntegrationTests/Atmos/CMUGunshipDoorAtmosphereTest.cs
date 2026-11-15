using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Atmos;
using Content.Shared.Atmos;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Atmos;

public sealed class CMUGunshipDoorAtmosphereTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/DeltaPressure/deltapressuretest.yml");

    // Mirror the Dynamic Gunship's mapped Normandy door and fan overrides.
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: CMNormandyDoorSinglePort
  id: CMUTestGunshipAtmosphereDoor
  components:
  - type: Airtight
    noAirWhenFullyAirBlocked: false

- type: entity
  parent: RMCTinyFan
  id: CMUTestGunshipAtmosphereFan
  components:
  - type: CMUGunshipDoorAtmosphere
""";

    [TestCase(0f, Atmospherics.OneAtmosphere)]
    [TestCase(0.001f, Atmospherics.OneAtmosphere)]
    [TestCase(60f, 60f)]
    [RunOnSide(Side.Server)]
    public void HatchAndFanStartWithAirAndPreserveItWhenCycled(float startingPressure, float expectedPressure)
    {
        SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
        var air = RelevantAtmos.Comp.Tiles[Vector2i.Zero].Air!;
        air.Clear();
        air.Temperature = Atmospherics.T20C;
        var moles = startingPressure * air.Volume / (Atmospherics.R * air.Temperature);
        air.SetMoles(Gas.Oxygen, moles * Atmospherics.OxygenStandard);
        air.SetMoles(Gas.Nitrogen, moles * Atmospherics.NitrogenStandard);

        var coordinates = new EntityCoordinates(RelevantAtmos.Owner, new Vector2(0.5f, 0.5f));
        var fan = SEntMan.SpawnEntity("CMUTestGunshipAtmosphereFan", coordinates);
        var door = SEntMan.SpawnEntity("CMUTestGunshipAtmosphereDoor", coordinates);
        var airtight = SEntMan.GetComponent<AirtightComponent>(door);
        Assert.That(airtight.NoAirWhenFullyAirBlocked, Is.False);
        SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

        var startup = SEntMan.System<CMUGunshipDoorAtmosphereSystem>();
        startup.Update(0);
        air = RelevantAtmos.Comp.Tiles[Vector2i.Zero].Air!;
        Assert.That(air.Pressure, Is.EqualTo(expectedPressure).Within(0.01f));
        Assert.That(air.GetMoles(Gas.Oxygen) / air.TotalMoles, Is.EqualTo(Atmospherics.OxygenStandard).Within(0.001f));
        Assert.That(SEntMan.GetComponent<CMUGunshipDoorAtmosphereComponent>(fan).Initialized, Is.True);

        // Exercise the same airtight changes used by opening and closing a door.
        var airtightSystem = SEntMan.System<AirtightSystem>();
        for (var i = 0; i < 3; i++)
        {
            airtightSystem.SetAirblocked((door, airtight), false);
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
            airtightSystem.SetAirblocked((door, airtight), true);
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
            Assert.That(RelevantAtmos.Comp.Tiles[Vector2i.Zero].Air, Is.SameAs(air));
            Assert.That(air.Pressure, Is.EqualTo(expectedPressure).Within(0.01f));
        }

        air.Clear();
        startup.Update(0);
        Assert.That(air.TotalMoles, Is.Zero, "Startup initialization must not become permanent life support.");
    }
}
