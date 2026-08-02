using System.Collections.Generic;
using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaMedicompCmss13ParityTest
{
    [Test]
    public async Task HerbalCaseContainsTwoTenUseHerbStacksLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var herbalCase = entMan.SpawnEntity("CMUYautjaHerbalCase", MapCoordinates.Nullspace);

            try
            {
                var storage = entMan.GetComponent<StorageComponent>(herbalCase);
                var contents = storage.Container.ContainedEntities.ToArray();

                Assert.That(contents, Has.Length.EqualTo(4),
                    "CMSS13 /obj/item/storage/herbal_case/full fills exactly four herb stacks.");

                Assert.That(contents.Count(uid => PrototypeId(entMan, uid) == "CMUYautjaAdvancedBruisePack"), Is.EqualTo(2));
                Assert.That(contents.Count(uid => PrototypeId(entMan, uid) == "CMUYautjaAdvancedOintment"), Is.EqualTo(2));

                foreach (var bruise in contents.Where(uid => PrototypeId(entMan, uid) == "CMUYautjaAdvancedBruisePack"))
                {
                    Assert.That(entMan.GetComponent<StackComponent>(bruise).Count, Is.EqualTo(10),
                        "Each CMSS13 mending-herb stack starts with amount/max_amount = 10.");
                }

                foreach (var ointment in contents.Where(uid => PrototypeId(entMan, uid) == "CMUYautjaAdvancedOintment"))
                {
                    Assert.That(entMan.GetComponent<StackComponent>(ointment).Count, Is.EqualTo(10),
                        "Each CMSS13 soothing-herb stack starts with amount/max_amount = 10.");
                }
            }
            finally
            {
                if (!entMan.Deleted(herbalCase))
                    entMan.DeleteEntity(herbalCase);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaCrystalsUseSingleThirtyUnitSourceEquivalentReagentsAndVisuals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var solutionSystem = entMan.System<SharedSolutionContainerSystem>();

            var thwei = prototypes.Index<ReagentPrototype>("thwei");
            var dathwei = prototypes.Index<ReagentPrototype>("dathwei");

            Assert.Multiple(() =>
            {
                Assert.That(thwei.ID, Is.EqualTo("thwei"));
                Assert.That(thwei.SubstanceColor, Is.EqualTo(Color.FromHex("#41c498")));
                Assert.That(dathwei.ID, Is.EqualTo("dathwei"));
                Assert.That(dathwei.SubstanceColor, Is.EqualTo(Color.FromHex("#c46b41")));
            });

            var clientPrototypes = client.ResolveDependency<IPrototypeManager>();
            var clientFactory = client.EntMan.ComponentFactory;

            AssertCrystalPrototypeFacts(
                clientPrototypes,
                clientFactory,
                "CMUYautjaAutoInjector",
                reagentId: "thwei",
                expectedOverlayColor: Color.White);
            AssertCrystalPrototypeFacts(
                clientPrototypes,
                clientFactory,
                "CMUYautjaThrallAutoInjector",
                reagentId: "dathwei",
                expectedOverlayColor: Color.FromHex("#c46b41"));

            var ordinary = entMan.SpawnEntity("CMUYautjaAutoInjector", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMUYautjaThrallAutoInjector", MapCoordinates.Nullspace);

            try
            {
                AssertCrystalSolutionFacts(entMan, solutionSystem, ordinary, "thwei");
                AssertCrystalSolutionFacts(entMan, solutionSystem, thrall, "dathwei");
            }
            finally
            {
                foreach (var uid in new[] { ordinary, thrall })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FilledMedicompVariantsUseDiscreteHealingCapsulesAndSourceWhitelist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var storageSystem = entMan.System<SharedStorageSystem>();
            var medicomp = entMan.SpawnEntity("CMUYautjaMedicomp", MapCoordinates.Nullspace);
            var filled = entMan.SpawnEntity("CMUYautjaMedicompFull", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMUYautjaMedicompThrall", MapCoordinates.Nullspace);
            var survivor = entMan.SpawnEntity("CMUYautjaMedicompSurvivor", MapCoordinates.Nullspace);
            var herbCase = entMan.SpawnEntity("CMUYautjaHerbalCase", MapCoordinates.Nullspace);
            var mcasteHerbs = entMan.SpawnEntity("CMUYautjaMcasteHerbContainer", MapCoordinates.Nullspace);

            try
            {
                var medicompStorage = entMan.GetComponent<StorageComponent>(medicomp);

                Assert.That(storageSystem.CanInsert(medicomp, herbCase, null, out _, medicompStorage), Is.True,
                    "CMSS13 medicomp can_hold includes /obj/item/storage/herbal_case.");
                Assert.That(storageSystem.CanInsert(medicomp, mcasteHerbs, null, out _, medicompStorage), Is.False,
                    "CMSS13 medicomp does not admit the sibling military-caste herb container.");

                AssertDiscreteMedicompContents(entMan, entMan.GetComponent<StorageComponent>(filled),
                    new Dictionary<string, int>
                    {
                        ["CMUYautjaStabilizerGel"] = 1,
                        ["CMUYautjaHealingGun"] = 1,
                        ["CMUYautjaWoundClamp"] = 1,
                        ["CMUYautjaAlienHealthAnalyzer"] = 1,
                        ["CMUYautjaAutoInjector"] = 3,
                        ["CMUYautjaHealingCapsule"] = 3,
                    });

                AssertDiscreteMedicompContents(entMan, entMan.GetComponent<StorageComponent>(thrall),
                    new Dictionary<string, int>
                    {
                        ["CMUYautjaStabilizerGel"] = 1,
                        ["CMUYautjaHealingGun"] = 1,
                        ["CMUYautjaWoundClamp"] = 1,
                        ["CMUYautjaAlienHealthAnalyzer"] = 1,
                        ["CMUYautjaThrallAutoInjector"] = 3,
                        ["CMUYautjaHealingCapsule"] = 3,
                    });

                AssertDiscreteMedicompContents(entMan, entMan.GetComponent<StorageComponent>(survivor),
                    new Dictionary<string, int>
                    {
                        ["CMUYautjaStabilizerGel"] = 1,
                        ["CMUYautjaHealingGun"] = 1,
                        ["CMUYautjaWoundClamp"] = 1,
                        ["CMUYautjaAlienHealthAnalyzer"] = 1,
                        ["CMUYautjaAutoInjector"] = 3,
                        ["CMUYautjaHealingCapsule"] = 3,
                        ["CMUYautjaHerbalCase"] = 1,
                    });
            }
            finally
            {
                foreach (var uid in new[] { medicomp, filled, thrall, survivor, herbCase, mcasteHerbs })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScopedYautjaMedicalSpriteStatesMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var cache = client.ResolveDependency<IResourceCache>();
            var rsi = cache.GetResource<RSIResource>(new ResPath("/Textures/_CMU14/Yautja/medical.rsi")).RSI;

            Assert.That(rsi.Size, Is.EqualTo(new Vector2i(32, 32)));
            Assert.That(rsi.TryGetState("thwei_1", out var thwei), Is.True);
            Assert.That(thwei!.DelayCount, Is.EqualTo(4));
            Assert.That(thwei.GetDelays(), Is.EqualTo(new[] { 6f, 1f, 1f, 1f }));
            Assert.That(rsi.TryGetState("healing_gun_on", out var healingGunOn), Is.True);
            Assert.That(healingGunOn!.DelayCount, Is.EqualTo(9));
            Assert.That(healingGunOn.GetDelays(), Is.EqualTo(new[] { 19f, 19f, 19f, 19f, 19f, 19f, 19f, 19f, 19f }));

            AssertPrototypeSprite(prototypes, factory, "CMUYautjaHealingGel", "/Textures/_CMU14/Yautja/medical.rsi", "healing_gel");
            AssertPrototypeSprite(prototypes, factory, "CMUYautjaStabilizerGel", "/Textures/_CMU14/Yautja/medical.rsi", "stabilizer_gel");
            AssertPrototypeSprite(prototypes, factory, "CMUYautjaWoundClamp", "/Textures/_CMU14/Yautja/medical.rsi", "wound_clamp");
            AssertPrototypeSprite(prototypes, factory, "CMUYautjaAlienHealthAnalyzer", "/Textures/_CMU14/Yautja/medical.rsi", "scanner");
            AssertPrototypeSprite(prototypes, factory, "CMUYautjaHerbalCase", "/Textures/_CMU14/Yautja/medical.rsi", "surgical_case", "surgical_case");
            AssertPrototypeSprite(prototypes, factory, "CMUYautjaHealingCapsule", "/Textures/_CMU14/Yautja/medical.rsi", "healing_gel");
            AssertPrototypeSprite(prototypes, factory, "CMUYautjaMedicomp", "/Textures/_CMU14/Yautja/yautja_items.rsi", "medicomp", "medicomp", "medicomp_open");
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertCrystalPrototypeFacts(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string id,
        string reagentId,
        Color expectedOverlayColor)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, id);
        Assert.That(prototype.TryGetComponent<HyposprayComponent>(out var hypospray, factory), Is.True, id);
        Assert.That(prototype.TryGetComponent<SolutionContainerManagerComponent>(out var solutions, factory), Is.True, id);
        Assert.That(prototype.TryGetComponent<SolutionContainerVisualsComponent>(out var visuals, factory), Is.True, id);
        var layers = sprite!.AllLayers.ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(sprite.BaseRSI?.Path, Is.EqualTo(new ResPath("/Textures/_CMU14/Yautja/medical.rsi")), $"{id} scoped RSI");
            Assert.That(layers.Select(layer => layer.RsiState.Name).ToArray(), Is.EqualTo(new[] { "crystal", "thwei_1" }), $"{id} CMSS13 crystal states");
            Assert.That(layers[1].Color, Is.EqualTo(expectedOverlayColor), $"{id} source overlay color");
            Assert.That(hypospray!.TransferAmount, Is.EqualTo((FixedPoint2) 30), $"{id} CMSS13 amount_per_transfer_from_this = 30");
            Assert.That(solutions!.Solutions["pen"].MaxVolume, Is.EqualTo((FixedPoint2) 30), $"{id} CMSS13 volume = 30");
            Assert.That(solutions.Solutions["pen"].Contents.Count, Is.EqualTo(1), $"{id} contains one source-equivalent reagent");
            Assert.That(solutions.Solutions["pen"].Contents.First().Reagent.Prototype, Is.EqualTo(reagentId), $"{id} reagent id");
            Assert.That(solutions.Solutions["pen"].Contents.First().Quantity, Is.EqualTo((FixedPoint2) 30), $"{id} reagent quantity");
            Assert.That(visuals!.FillBaseName, Is.EqualTo("thwei_"), $"{id} filled state prefix");
            Assert.That(visuals.EmptySpriteName, Is.Null, $"{id} empty state must hide the fill layer");
        });
    }

    private static void AssertCrystalSolutionFacts(
        IEntityManager entMan,
        SharedSolutionContainerSystem solutionSystem,
        EntityUid uid,
        string reagentId)
    {
        var hypospray = entMan.GetComponent<HyposprayComponent>(uid);
        var solutions = entMan.GetComponent<SolutionContainerManagerComponent>(uid);

        Assert.That(hypospray.TransferAmount, Is.EqualTo((FixedPoint2) 30));
        Assert.That(solutionSystem.TryGetSolution((uid, solutions), "pen", out _, out var solution), Is.True);
        Assert.That(solution!.MaxVolume, Is.EqualTo((FixedPoint2) 30));
        Assert.That(solution.Contents.Count, Is.EqualTo(1));
        Assert.That(solution.GetTotalPrototypeQuantity(reagentId), Is.EqualTo((FixedPoint2) 30));
    }

    private static void AssertDiscreteMedicompContents(
        IEntityManager entMan,
        StorageComponent storage,
        IReadOnlyDictionary<string, int> expected)
    {
        var actual = storage.Container.ContainedEntities
            .GroupBy(uid => PrototypeId(entMan, uid))
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.That(actual, Is.EqualTo(expected));

        foreach (var capsule in storage.Container.ContainedEntities.Where(uid => PrototypeId(entMan, uid) == "CMUYautjaHealingCapsule"))
        {
            Assert.That(entMan.HasComponent<StackComponent>(capsule), Is.False,
                "CMSS13 healing-gel capsules are discrete items, not stack-count multipliers.");
        }
    }

    private static void AssertPrototypeSprite(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string id,
        string expectedPath,
        params string[] expectedStates)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, id);
        Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(new ResPath(expectedPath)), $"{id} scoped RSI");
        Assert.That(sprite.AllLayers.Select(layer => layer.RsiState.Name).ToArray(), Is.EqualTo(expectedStates), $"{id} source sprite states");
    }

    private static string PrototypeId(IEntityManager entMan, EntityUid uid)
    {
        return entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID
               ?? throw new AssertionException($"Expected prototype-backed entity for {uid}.");
    }
}
