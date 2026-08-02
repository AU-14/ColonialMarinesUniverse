using System.Linq;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaChemistryVisualTest
{
    [Test]
    public async Task ShipGlasswareFillLayersUseDedicatedRsiStates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var spriteSystem = client.System<SpriteSystem>();

        EntityUid backendBeaker = default;
        EntityUid placedBeakerOffset6x9 = default;
        EntityUid placedBeakerOffsetNeg5x0 = default;
        EntityUid vial = default;
        NetEntity backendBeakerNet = default;
        NetEntity placedBeakerOffset6x9Net = default;
        NetEntity placedBeakerOffsetNeg5x0Net = default;
        NetEntity vialNet = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            backendBeaker = entMan.SpawnEntity("CMUHunterShipSilverCatalystBeaker", map.GridCoords);
            placedBeakerOffset6x9 = entMan.SpawnEntity("CMUHunterShipPlacedBeakerBeakersilverSouthOffset6x9", map.GridCoords.Offset(new(1, 0)));
            placedBeakerOffsetNeg5x0 = entMan.SpawnEntity("CMUHunterShipPlacedBeakerBeakersilverSouthOffsetNeg5x0", map.GridCoords.Offset(new(2, 0)));
            vial = entMan.SpawnEntity("CMUHunterShipPlacedBaseChemistryEmptyVialVialSouthOffset1x7", map.GridCoords.Offset(new(3, 0)));
            FillBeaker(backendBeaker, solutions);
            FillBeaker(placedBeakerOffset6x9, solutions);
            FillBeaker(placedBeakerOffsetNeg5x0, solutions);
            FillBeaker(vial, solutions);
            backendBeakerNet = entMan.GetNetEntity(backendBeaker);
            placedBeakerOffset6x9Net = entMan.GetNetEntity(placedBeakerOffset6x9);
            placedBeakerOffsetNeg5x0Net = entMan.GetNetEntity(placedBeakerOffsetNeg5x0);
            vialNet = entMan.GetNetEntity(vial);
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            Assert.That(client.EntMan.TryGetEntity(backendBeakerNet, out var clientBackendBeaker), Is.True);
            Assert.That(client.EntMan.TryGetEntity(placedBeakerOffset6x9Net, out var clientPlacedBeakerOffset6x9), Is.True);
            Assert.That(client.EntMan.TryGetEntity(placedBeakerOffsetNeg5x0Net, out var clientPlacedBeakerOffsetNeg5x0), Is.True);
            Assert.That(client.EntMan.TryGetEntity(vialNet, out var clientVial), Is.True);

            Assert.Multiple(() =>
            {
                AssertShipGlasswareFillRsi(
                    client.EntMan,
                    spriteSystem,
                    clientBackendBeaker.Value,
                    "CMUHunterShipSilverCatalystBeaker",
                    new ResPath("/Textures/_CMU14/HunterShip/obj/items/chemistry.rsi"),
                    new ResPath("/Textures/_RMC14/Objects/Medical/large_beaker.rsi"),
                    "beakerlarge",
                    5);

                AssertShipGlasswareFillRsi(
                    client.EntMan,
                    spriteSystem,
                    clientPlacedBeakerOffset6x9.Value,
                    "CMUHunterShipPlacedBeakerBeakersilverSouthOffset6x9",
                    new ResPath("/Textures/_CMU14/HunterShip/obj/items/chemistry.rsi"),
                    new ResPath("/Textures/_RMC14/Objects/Medical/large_beaker.rsi"),
                    "beakerlarge",
                    5);

                AssertShipGlasswareFillRsi(
                    client.EntMan,
                    spriteSystem,
                    clientPlacedBeakerOffsetNeg5x0.Value,
                    "CMUHunterShipPlacedBeakerBeakersilverSouthOffsetNeg5x0",
                    new ResPath("/Textures/_CMU14/HunterShip/obj/items/chemistry.rsi"),
                    new ResPath("/Textures/_RMC14/Objects/Medical/large_beaker.rsi"),
                    "beakerlarge",
                    5);

                AssertShipGlasswareFillRsi(
                    client.EntMan,
                    spriteSystem,
                    clientVial.Value,
                    "CMUHunterShipPlacedBaseChemistryEmptyVialVialSouthOffset1x7",
                    new ResPath("/Textures/_CMU14/HunterShip/obj/items/chemistry.rsi"),
                    new ResPath("/Textures/_RMC14/Objects/Chemistry/vials.rsi"),
                    "vial",
                    6);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllSolutionContainerFillStatesExistInConfiguredRsi()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var componentFactory = client.ResolveDependency<IComponentFactory>();
        var resourceCache = client.ResolveDependency<IResourceCache>();

        await client.WaitAssertion(() =>
        {
            var protos = protoMan.EnumeratePrototypes<EntityPrototype>()
                .Where(p => !p.Abstract)
                .Where(p => p.TryComp<SolutionContainerVisualsComponent>(out _, componentFactory))
                .Where(p => p.TryComp<SpriteComponent>(out _, componentFactory))
                .OrderBy(p => p.ID);

            foreach (var proto in protos)
            {
                Assert.That(proto.TryComp<SolutionContainerVisualsComponent>(out var visuals, componentFactory));
                Assert.That(proto.TryComp<SpriteComponent>(out var sprite, componentFactory));

                if (string.IsNullOrEmpty(visuals.FillBaseName))
                    continue;

                if (!sprite.LayerExists(visuals.Layer))
                {
                    Assert.That(visuals.FillSprite, Is.Null,
                        $"{proto.ID} configures fillSprite/fillBaseName but Sprite lacks mapped {visuals.Layer} layer");
                    continue;
                }

                var rsi = ResolveFillRsi(proto, visuals, sprite, resourceCache);

                for (var i = 1; i <= visuals.MaxFillLevels; i++)
                {
                    var state = $"{visuals.FillBaseName}{i}";
                    Assert.That(rsi.TryGetState(state, out _), Is.True,
                        $"{proto.ID} fill RSI {rsi.Path} should contain state {state}");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void FillBeaker(EntityUid uid, SharedSolutionContainerSystem solutions)
    {
        Assert.That(solutions.TryGetSolution(uid, "beaker", out var solutionEnt, out _), Is.True);
        Assert.That(solutions.TryAddSolution(solutionEnt!.Value, new Solution("Water", 5)), Is.True);
    }

    private static void AssertShipGlasswareFillRsi(
        IEntityManager entMan,
        SpriteSystem spriteSystem,
        EntityUid uid,
        string id,
        ResPath expectedBaseRsi,
        ResPath expectedFillRsi,
        string fillBaseName,
        int maxFillLevels)
    {
        var sprite = entMan.GetComponent<SpriteComponent>(uid);

        Assert.That(spriteSystem.TryGetLayer((uid, sprite), 0, out var baseLayer, false), Is.True, $"{id} base layer missing");
        // Red-test coverage: the ship prototype must expose the mapped fill layer before RSI-state checks can pass.
        Assert.That(spriteSystem.TryGetLayer((uid, sprite), SolutionContainerLayers.Fill, out var fillLayer, false), Is.True, $"{id} fill layer missing");

        Assert.That(baseLayer!.ActualRsi?.Path, Is.EqualTo(expectedBaseRsi), $"{id} base layer RSI");
        Assert.That(fillLayer!.ActualRsi?.Path, Is.EqualTo(expectedFillRsi), $"{id} fill layer RSI");

        for (var i = 1; i <= maxFillLevels; i++)
        {
            var state = $"{fillBaseName}{i}";
            Assert.That(fillLayer.ActualRsi!.TryGetState(state, out _), Is.True,
                $"{id} fill layer RSI {fillLayer.ActualRsi.Path} should contain state {state}");
        }
    }

    private static RSI ResolveFillRsi(
        EntityPrototype proto,
        SolutionContainerVisualsComponent visuals,
        SpriteComponent sprite,
        IResourceCache resourceCache)
    {
        if (visuals.FillSprite is SpriteSpecifier.Rsi fillSprite)
        {
            var rsiPath = SpriteSpecifierSerializer.TextureRoot / fillSprite.RsiPath;
            Assert.That(resourceCache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True,
                $"{proto.ID} fillSprite RSI {rsiPath} should load");
            return resource!.RSI;
        }

        Assert.That(visuals.FillSprite, Is.Null,
            $"{proto.ID} fillSprite must be an RSI sprite specifier so fillBaseName states can be resolved");
        Assert.That(sprite.BaseRSI, Is.Not.Null, $"{proto.ID} Sprite base RSI should exist for fillBaseName {visuals.FillBaseName}");
        return sprite.BaseRSI!;
    }
}
