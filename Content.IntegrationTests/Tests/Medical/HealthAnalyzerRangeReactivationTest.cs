using System.Numerics;
using Content.Server.Medical;
using Content.Server.Medical.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class HealthAnalyzerRangeReactivationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HealthAnalyzerRangeReactivationTestAnalyzer
  parent: HandheldHealthAnalyzerUnpowered
  components:
  - type: HealthAnalyzer
    updateInterval: 0
";

    [Test]
    public async Task PatientLinkPausesAndReactivatesAcrossRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var analyzerSystem = server.System<HealthAnalyzerSystem>();
        var transformSystem = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var analyzer = entMan.SpawnEntity(
                "HealthAnalyzerRangeReactivationTestAnalyzer",
                map.GridCoords);
            var patient = entMan.SpawnEntity(null, map.GridCoords);
            var component = entMan.GetComponent<HealthAnalyzerComponent>(analyzer);
            component.ScannedEntity = patient;

            analyzerSystem.Update(0f);
            Assert.Multiple(() =>
            {
                Assert.That(component.ScannedEntity, Is.EqualTo(patient));
                Assert.That(component.IsAnalyzerActive, Is.True);
            });

            transformSystem.SetCoordinates(
                patient,
                map.GridCoords.Offset(new Vector2(3f, 0f)));
            analyzerSystem.Update(0f);
            Assert.Multiple(() =>
            {
                Assert.That(component.ScannedEntity, Is.EqualTo(patient));
                Assert.That(component.IsAnalyzerActive, Is.False);
            });

            transformSystem.SetCoordinates(patient, map.GridCoords);
            analyzerSystem.Update(0f);
            Assert.Multiple(() =>
            {
                Assert.That(component.ScannedEntity, Is.EqualTo(patient));
                Assert.That(component.IsAnalyzerActive, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }
}
