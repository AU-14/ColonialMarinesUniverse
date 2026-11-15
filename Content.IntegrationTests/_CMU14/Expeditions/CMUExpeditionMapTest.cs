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

[TestFixture, NonParallelizable]
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

}
