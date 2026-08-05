#nullable enable
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Construction.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.Construction;
using Content.Shared.Temperature.Components;

namespace Content.IntegrationTests.Tests.Construction.Interaction;

public sealed class ConstructionEventValidation : InteractionTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: Tag
  id: ConstructionEventValidationPart

- type: constructionGraph
  id: ConstructionTemperatureValidationGraph
  start: Start
  graph:
  - node: Start
    edges:
    - to: Complete
      steps:
      - minTemperature: 300
  - node: Complete

- type: entity
  id: ConstructionTemperatureValidationTarget
  components:
  - type: Temperature
  - type: Construction
    graph: ConstructionTemperatureValidationGraph
    node: Start

- type: constructionGraph
  id: ConstructionPartAssemblyValidationGraph
  start: Start
  graph:
  - node: Start
    edges:
    - to: Complete
      steps:
      - assemblyId: validation
  - node: Complete

- type: entity
  id: ConstructionPartAssemblyValidationTarget
  components:
  - type: ContainerContainer
    containers:
      part-container: !type:Container
  - type: PartAssembly
    parts:
      validation:
      - ConstructionEventValidationPart
  - type: Construction
    graph: ConstructionPartAssemblyValidationGraph
    node: Start

- type: entity
  id: ConstructionPartAssemblyValidationPart
  components:
  - type: Item
  - type: Tag
    tags:
    - ConstructionEventValidationPart
";

    [Test]
    public async Task TemperatureValidationIsPureAndQueued()
    {
        await SpawnTarget("ConstructionTemperatureValidationTarget");
        var target = SEntMan.GetEntity(Target!.Value);
        var construction = SEntMan.GetComponent<ConstructionComponent>(target);
        var temperature = SEntMan.GetComponent<TemperatureComponent>(target);

        await Server.WaitPost(() =>
        {
            var temperatureSystem = SEntMan.System<TemperatureSystem>();
            var heat = (400f - temperature.Temperature) * temperature.HeatCapacity;
            temperatureSystem.ChangeHeat((target, temperature), heat);

            Assert.That(construction.Node, Is.EqualTo("Start"));
        });

        await RunTicks(1);
        Assert.That(construction.Node, Is.EqualTo("Complete"));
    }

    [Test]
    public async Task PartAssemblyValidationIsPureAndQueued()
    {
        await SpawnTarget("ConstructionPartAssemblyValidationTarget");
        var target = SEntMan.GetEntity(Target!.Value);
        var construction = SEntMan.GetComponent<ConstructionComponent>(target);

        await Server.WaitPost(() =>
        {
            var part = SEntMan.SpawnAtPosition(
                "ConstructionPartAssemblyValidationPart",
                SEntMan.GetCoordinates(TargetCoords));

            var assembly = SEntMan.System<PartAssemblySystem>();
            Assert.That(assembly.TryInsertPart(part, target), Is.True);
            Assert.That(construction.Node, Is.EqualTo("Start"));
        });

        await RunTicks(1);
        Assert.That(construction.Node, Is.EqualTo("Complete"));
    }
}
