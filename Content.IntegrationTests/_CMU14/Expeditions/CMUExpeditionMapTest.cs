using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Water;
using Content.Shared._RMC14.Atmos;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Movement.Components;
using Content.Shared.Paper;
using Content.Shared.RMCLoreExaminable;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Expeditions;

[TestFixture]
public sealed class CMUExpeditionMapTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task ProfilesBuildBeforeTheirGovforLandingZoneOpens()
    {
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            var map = Server.System<SharedMapSystem>();
            foreach (var biome in Enum.GetValues<CMUExpeditionBiome>())
            {
                var landform = biome switch
                {
                    CMUExpeditionBiome.Beach => CMUExpeditionLandform.Coast,
                    CMUExpeditionBiome.Mountain => CMUExpeditionLandform.Highlands,
                    CMUExpeditionBiome.SwampJungle => CMUExpeditionLandform.Wetlands,
                    _ => CMUExpeditionLandform.RiverValley,
                };
                Assert.That(generator.TryGenerate($"CMUExpedition{biome}", 42, landform,
                    CMUExpeditionStory.CrashRecovery, out var uid, out var error), Is.True, error);
                try
                {
                    var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(uid);
                    Assert.That(expedition.Ready, Is.False);
                    Assert.That(generator.OpenLandingZone(uid), Is.False);
                    Assert.That(expedition.LandingBeacon, Is.Null);
                    Assert.That(map.IsInitialized(SEntMan.GetComponent<MapComponent>(uid).MapId), Is.False);
                    Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 0, 0, 0, out _, out error), Is.False);
                    Assert.That(error, Is.EqualTo("cmu-expedition-busy"));

                    generator.Update(0);
                    Assert.That(expedition.Ready, Is.False, "One batch must not build the entire map.");
                    for (var i = 0; i < 900 && !expedition.Ready; i++)
                        generator.Update(0);
                    Assert.That(expedition.Ready, Is.True, "Generation must finish within the bounded work budget.");
                    Assert.That(map.IsInitialized(SEntMan.GetComponent<MapComponent>(uid).MapId), Is.True);
                    var grid = SEntMan.GetComponent<MapGridComponent>(uid);
                    Assert.That(map.GetAllTiles(uid, grid).Count(), Is.EqualTo(140 * 140));
                    Assert.That(expedition.LandingBeacon, Is.Null, "Readiness alone must not publish an LZ.");

                    var plan = expedition.Plan;
                    Assert.That(expedition.UpperMaps, Has.Count.EqualTo(2));
                    var groundLevel = SEntMan.GetComponent<Content.Shared.CMU14.ZLevels.Core.Components.CMUZLevelMapComponent>(uid);
                    Assert.That(groundLevel.Depth, Is.Zero);
                    Assert.That(groundLevel.MapAbove, Is.EqualTo(expedition.UpperMaps[0]));
                    Assert.That(SEntMan.GetComponent<Content.Shared.Gravity.GravityComponent>(uid).Enabled, Is.True);
                    Assert.That(generator.SetExpeditionTime(uid, 18), Is.True);
                    Assert.That(generator.SetExpeditionTime(uid, float.NaN), Is.False);
                    foreach (var upper in expedition.UpperMaps)
                    {
                        Assert.That(SEntMan.GetComponent<Content.Shared._NC14.DayNightCycle.DayNightCycleComponent>(upper).CurrentCycleTime, Is.EqualTo(0.75f));
                        Assert.That(map.GetTileRef(upper, SEntMan.GetComponent<MapGridComponent>(upper),
                            new Vector2i(plan.LandingZone.X, plan.LandingZone.Y)).Tile.IsEmpty, Is.True,
                            "The landing corridor must remain open sky on every upper level.");
                    }
                    var waterCount = 0;
                    var details = 0;
                    var wreckPieces = 0;
                    // Inspect spawned components, not only palette IDs or the pure plan.
                    var entities = SEntMan.GetComponent<TransformComponent>(uid).ChildEnumerator;
                    while (entities.MoveNext(out var child))
                    {
                        var transform = SEntMan.GetComponent<TransformComponent>(child);
                        var position = transform.LocalPosition;
                        var index = plan.Index((int) position.X, (int) position.Y);
                        if (plan.WreckObjects.TryGetValue(index, out var piece) &&
                            SEntMan.GetComponent<MetaDataComponent>(child).EntityPrototype?.ID == piece.Prototype)
                        {
                            wreckPieces++;
                            Assert.That(transform.LocalRotation.EqualsApprox(Angle.FromDegrees(piece.QuarterTurns * 90)), Is.True);
                        }
                        if (SEntMan.TryGetComponent<RMCWaterComponent>(child, out var water))
                        {
                            waterCount++;
                            Assert.That(plan.Terrain[index], Is.EqualTo(CMUExpeditionTerrain.Water));
                            var tile = CMUExpeditionGenerator.GetWaterTile(plan, (int) position.X, (int) position.Y);
                            var expectedDepth = tile.Kind switch
                            {
                                CMUExpeditionWaterKind.Deep => 18,
                                CMUExpeditionWaterKind.Shallow => 12,
                                CMUExpeditionWaterKind.InnerCorner => 2,
                                _ => 4,
                            };
                            Assert.That(water.Depth, Is.EqualTo(expectedDepth));
                            Assert.That(transform.LocalRotation.EqualsApprox(Angle.FromDegrees(tile.QuarterTurns * 90)), Is.True);
                            var expectedPrototype = tile.Kind switch
                            {
                                CMUExpeditionWaterKind.Deep => "RMCEntityDesertWaterDeep",
                                CMUExpeditionWaterKind.Shallow => "RMCEntityDesertWaterShallow",
                                CMUExpeditionWaterKind.Edge => "RMCEntityDesertWaterShallowEdge",
                                CMUExpeditionWaterKind.Corner => "RMCEntityDesertWaterShallowCorner",
                                _ => "RMCEntityDesertWaterShallowCornerEdge",
                            };
                            Assert.That(SEntMan.GetComponent<MetaDataComponent>(child).EntityPrototype?.ID, Is.EqualTo(expectedPrototype));
                            var speed = SEntMan.GetComponent<SpeedModifierContactsComponent>(child);
                            Assert.That(speed.SprintSpeedModifier, Is.LessThan(1));
                            continue;
                        }
                        if (SEntMan.TryGetComponent<PhysicsComponent>(child, out var physics) && !physics.CanCollide)
                            details++;
                    }
                    Assert.That(waterCount, Is.EqualTo(plan.Terrain.Count(t => t == CMUExpeditionTerrain.Water)));
                    Assert.That(wreckPieces, Is.EqualTo(plan.WreckObjects.Count), "Use the original hull art for every planned wreck piece.");
                    Assert.That(CountFires(), Is.Zero, "Fire must wait until the expedition is opened.");
                    Assert.That(details, Is.GreaterThanOrEqualTo(plan.Details.Count(d => d != CMUExpeditionDetail.None)),
                        "Forest-floor dressing must not block the clear routes or LZ.");

                    Assert.That(generator.OpenLandingZone(uid), Is.True);
                    var beacon = expedition.LandingBeacon;
                    Assert.That(beacon, Is.Not.Null);
                    var destination = SEntMan.GetComponent<DropshipDestinationComponent>(beacon!.Value);
                    Assert.That(destination.FactionController, Is.EqualTo("govfor"));
                    Assert.That(destination.CanBePrimary, Is.False);
                    Assert.That(SEntMan.GetComponent<TransformComponent>(beacon.Value).MapUid, Is.EqualTo(uid));
                    Assert.That(generator.OpenLandingZone(uid), Is.True);
                    Assert.That(expedition.LandingBeacon, Is.EqualTo(beacon), "Opening twice must not create duplicate destinations.");
                    Assert.That(CountFires(), Is.EqualTo(plan.FirePockets.Count), "Opening twice must not double the wildfires.");

                    int CountFires()
                    {
                        var count = 0;
                        var children = SEntMan.GetComponent<TransformComponent>(uid).ChildEnumerator;
                        while (children.MoveNext(out var child))
                            if (SEntMan.HasComponent<TileFireComponent>(child))
                                count++;
                        return count;
                    }
                }
                finally
                {
                    SEntMan.DeleteEntity(uid);
                }
            }
        });
    }

    [Test]
    public async Task NamedScenariosPlaceReadableEvidenceOnDryConnectedSites()
    {
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var localization = Server.ResolveDependency<ILocalizationManager>();
            var scenarios = prototypes.EnumeratePrototypes<CMUExpeditionScenarioPrototype>().ToArray();
            var finalClues = new Dictionary<string, string>
            {
                ["CMUBlackwaterReach"] = "closed eleven months earlier",
                ["CMUReedwakeBasin"] = "before an answer",
                ["CMUAshfallScar"] = "no completion signature",
                ["CMUWhiteoutShelf"] = "not a refusal to proceed",
                ["CMUGlasswaterStrand"] = "reserve power fails",
                ["CMUCairnPass"] = "the same document an authorization",
                ["CMUHollowSignal"] = "through a local maintenance connection",
            };
            Assert.That(scenarios.Select(s => s.ID), Is.SupersetOf(new[]
            {
                "CMUBlackwaterReach", "CMUReedwakeBasin", "CMUAshfallScar", "CMUWhiteoutShelf",
                "CMUGlasswaterStrand", "CMUCairnPass", "CMUHollowSignal",
            }));
            var previousEvidence = new List<EntityUid>();
            foreach (var scenario in scenarios)
            {
                Assert.That(generator.TryGenerateScenario(scenario.ID, 42, out var uid, out var error), Is.True, error);
                try
                {
                    var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(uid);
                    Assert.That(expedition.Ready, Is.False);
                    Assert.That(expedition.Evidence, Is.Empty);
                    Assert.That(generator.OpenLandingZone(uid), Is.False);
                    for (var i = 0; i < 900 && !expedition.Ready; i++)
                        generator.Update(0);
                    Assert.That(expedition.Ready, Is.True, scenario.ID);
                    Assert.That(expedition.Scenario?.Id, Is.EqualTo(scenario.ID));
                    Assert.That(expedition.Plan.Landform, Is.EqualTo(scenario.Landform));
                    Assert.That(expedition.Plan.Story, Is.EqualTo(scenario.Story));
                    Assert.That(expedition.Evidence, Has.Count.EqualTo(3));
                    var plan = expedition.Plan;
                    var reachable = FindDryReachableTiles(plan);
                    foreach (var evidence in expedition.Evidence)
                    {
                        var transform = SEntMan.GetComponent<TransformComponent>(evidence);
                        Assert.That(transform.MapUid, Is.EqualTo(uid));
                        var index = plan.Index((int) transform.LocalPosition.X, (int) transform.LocalPosition.Y);
                        Assert.That(plan.Terrain[index], Is.Not.EqualTo(CMUExpeditionTerrain.Water));
                        Assert.That(plan.Terrain[index], Is.Not.EqualTo(CMUExpeditionTerrain.Cliff));
                        Assert.That(plan.Props[index], Is.EqualTo(CMUExpeditionProp.None), "Evidence must not share a solid prop's tile.");
                        Assert.That(reachable[index], Is.True, "The evidence must be accessible from the LZ.");
                    }
                    foreach (var evidence in expedition.Evidence.Take(2))
                    {
                        var paper = SEntMan.GetComponent<PaperComponent>(evidence);
                        Assert.That(paper.Content.Length, Is.GreaterThan(150));
                        Assert.That(paper.Content, Does.Not.Contain("cmu-expedition-"), "Paper map initialization must resolve locale keys and references.");
                    }
                    Assert.That(SEntMan.GetComponent<PaperComponent>(expedition.Evidence[0]).Content, Does.Contain("SUNDIAL"));
                    var recorder = SEntMan.GetComponent<RMCLoreExaminableComponent>(expedition.Evidence[2]);
                    Assert.That(localization.GetString(recorder.Content).Length, Is.GreaterThan(150));
                    if (finalClues.TryGetValue(scenario.ID, out var finalClue))
                        Assert.That(localization.GetString(recorder.Content), Does.Contain(finalClue),
                            "A partially parsed transcript must not silently lose the story's final clue.");
                    Assert.That(expedition.RecoveryTarget, Is.Not.Null);
                    var target = SEntMan.GetComponent<RMCLoreExaminableComponent>(expedition.RecoveryTarget!.Value);
                    Assert.That(target.Content, Is.EqualTo(scenario.TargetLore));
                    Assert.That(localization.GetString(target.Content).Length, Is.GreaterThan(150));
                    Assert.That(SEntMan.GetComponent<MetaDataComponent>(uid).EntityName, Does.Contain(localization.GetString(scenario.Name)));
                    Assert.That(generator.OpenLandingZone(uid), Is.True);
                    Assert.That(SEntMan.GetComponent<MetaDataComponent>(expedition.LandingBeacon!.Value).EntityName,
                        Does.Contain(localization.GetString(scenario.Name)));
                    Assert.That(previousEvidence.Intersect(expedition.Evidence), Is.Empty, "Stories must own separate evidence entities.");
                    previousEvidence.AddRange(expedition.Evidence);
                }
                finally
                {
                    SEntMan.DeleteEntity(uid);
                }
                Assert.That(previousEvidence.All(e => SEntMan.Deleted(e)), Is.True, "Removing a map must clean up its story entities.");
            }
        });
    }

    [Test]
    public async Task InvalidProfilesAndDeletedBuildsDoNotLeaveTheGeneratorBusy()
    {
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            Assert.That(generator.TryGenerateScenario("CMUMissingScenario", 42, out _, out var scenarioError), Is.False);
            Assert.That(scenarioError, Is.EqualTo("cmu-expedition-invalid-scenario"));
            Assert.That(generator.TryGenerate("CMUExpeditionMissing", 0, 0, 0, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo("cmu-expedition-invalid-profile"));
            Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 0, 0, 0, out var first, out _), Is.True);
            generator.Update(0);
            SEntMan.DeleteEntity(first);
            generator.Update(0);
            Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 1, 0, 0, out var second, out _), Is.True);
            SEntMan.DeleteEntity(second);
            generator.Update(0);
        });
    }

    private static bool[] FindDryReachableTiles(CMUExpeditionPlan plan)
    {
        var visited = new bool[plan.Terrain.Length];
        var pending = new Queue<CMUExpeditionPoint>();
        pending.Enqueue(plan.LandingZone);
        visited[plan.Index(plan.LandingZone.X, plan.LandingZone.Y)] = true;
        while (pending.TryDequeue(out var point))
        {
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var x = point.X + dx;
                var y = point.Y + dy;
                if (x < 0 || y < 0 || x >= plan.Size || y >= plan.Size)
                    continue;
                var index = plan.Index(x, y);
                if (visited[index] || plan.Props[index] != CMUExpeditionProp.None ||
                    plan.Terrain[index] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff)
                    continue;
                visited[index] = true;
                pending.Enqueue(new CMUExpeditionPoint(x, y));
            }
        }
        return visited;
    }
}
