#nullable enable

using Content.Server.AU14.Scenario;
using Content.Shared.CMU.Round;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Scenario;

[TestFixture]
public sealed class RoundPlanSelectionSnapshotTest
{
    [Test]
    public void TypedAssignmentsProjectTheLegacySelectionContract()
    {
        var govfor = new RoundForceAssignment(
            RoundSide.Govfor,
            new RoundForceId("USCM"),
            "GovforShip");
        var opfor = new RoundForceAssignment(
            RoundSide.Opfor,
            new RoundForceId("UPP"),
            "OpforShip");

        var snapshot = RoundPlanSelectionSnapshot.FromAssignments(
            "DistressSignal",
            80,
            govfor,
            opfor,
            "LV624",
            "LV624Map",
            "XenoThreat");

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.GovforAssignment, Is.EqualTo(govfor));
            Assert.That(snapshot.OpforAssignment, Is.EqualTo(opfor));
            Assert.That(snapshot.GovforPlatoonId, Is.EqualTo("USCM"));
            Assert.That(snapshot.OpforPlatoonId, Is.EqualTo("UPP"));
            Assert.That(snapshot.GovforShipId, Is.EqualTo("GovforShip"));
            Assert.That(snapshot.OpforShipId, Is.EqualTo("OpforShip"));
        });
    }

    [Test]
    public void ConvertsFrozenSelectionWithoutDroppingContext()
    {
        var snapshot = new RoundPlanSelectionSnapshot(
            "DistressSignal",
            80,
            "GovforPlatoon",
            "OpforPlatoon",
            "LV624",
            "LV624Map",
            "XenoThreat",
            "GovforShip",
            "OpforShip");

        ScenarioPlanValidationRequest request = snapshot.ToScenarioPlanRequest();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.HasWorldSelection, Is.True);
            Assert.That(request.PresetId, Is.EqualTo(snapshot.PresetId));
            Assert.That(request.PlayerCount, Is.EqualTo(snapshot.PlayerCount));
            Assert.That(request.GovforPlatoonId, Is.EqualTo(snapshot.GovforPlatoonId));
            Assert.That(request.OpforPlatoonId, Is.EqualTo(snapshot.OpforPlatoonId));
            Assert.That(request.PlanetId, Is.EqualTo(snapshot.PlanetId));
            Assert.That(request.MapId, Is.EqualTo(snapshot.MapId));
            Assert.That(request.SelectedThreatId, Is.EqualTo(snapshot.SelectedThreatId));
            Assert.That(request.GovforShipId, Is.EqualTo(snapshot.GovforShipId));
            Assert.That(request.OpforShipId, Is.EqualTo(snapshot.OpforShipId));
        });
    }

    [TestCase(null, "Map")]
    [TestCase("Planet", null)]
    public void RequiresWorldSelection(string? planetId, string? mapId)
    {
        var snapshot = new RoundPlanSelectionSnapshot(
            "Insurgency",
            40,
            null,
            null,
            planetId,
            mapId,
            null,
            null,
            null);

        Assert.That(snapshot.HasWorldSelection, Is.False);
    }

    [Test]
    public void RuntimeContextKeepsFrozenWorldAndShipsWhileUsingTheEffectivePreset()
    {
        var frozen = new RoundPlanSelectionSnapshot(
            "UnavailablePreset",
            60,
            "GovforPlatoon",
            "OpforPlatoon",
            "LV624",
            "LV624Map",
            null,
            "GovforShip",
            "OpforShip");

        var runtime = frozen.WithRuntimeContext(80, "DistressSignal", "XenoThreatDS");

        Assert.Multiple(() =>
        {
            Assert.That(runtime.PresetId, Is.EqualTo("DistressSignal"));
            Assert.That(runtime.PlayerCount, Is.EqualTo(80));
            Assert.That(runtime.SelectedThreatId, Is.EqualTo("XenoThreatDS"));
            Assert.That(frozen.PresetId, Is.EqualTo("UnavailablePreset"));
            Assert.That(runtime.PlanetId, Is.EqualTo(frozen.PlanetId));
            Assert.That(runtime.MapId, Is.EqualTo(frozen.MapId));
            Assert.That(runtime.GovforPlatoonId, Is.EqualTo(frozen.GovforPlatoonId));
            Assert.That(runtime.OpforPlatoonId, Is.EqualTo(frozen.OpforPlatoonId));
            Assert.That(runtime.GovforShipId, Is.EqualTo(frozen.GovforShipId));
            Assert.That(runtime.OpforShipId, Is.EqualTo(frozen.OpforShipId));
        });
    }
}
