using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Kitchen;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Nutrition;

[TestFixture]
public sealed class NutritionContentMergeRegressionTest
{
    private static readonly string[] MigratedEdibles =
    {
        "RMCJumpsuitDispatcherUniform",
        "CMMobMouse",
        "RMCFoodDonut",
        "RMCFoodDonutFrosted",
        "CMFortuneCookieCracked",
        "RMCFoodDonutJellyFrosted",
        "RMCFoodDonutJelly",
        "RMCFoodCookiePeanutButter",
        "RMCFoodPBJSandwich",
        "RMCFoodOnigiri",
        "RMCFoodCarbonara",
        "FoodBanhMi",
        "FoodBanhMiHalf",
        "FoodChickenParmHero",
        "FoodChickenParmHalf",
        "FoodMeatballSub",
        "FoodMeatballSubHalf",
        "FoodGabagool",
        "FoodGabagoolHalf",
        "FoodCheesesteak",
        "FoodCheesesteakHalf",
        "FoodYorkshirePudding",
        "FoodCakeBlackForest",
        "FoodBlackForestSlice",
        "FoodMeatSausage",
        "FoodMeatSausageCooked",
        "FoodHotDog",
        "FoodTapsilog",
        "FoodCarpsilog",
        "FoodChicksilog",
        "FoodTocilog",
        "FoodBangersMash",
        "FoodPieMash",
        "RMCFoodMeatFish",
        "RMCFoodMeatFishGrilled",
        "RMCFoodMeatFishAndChips",
        "RMCFoodMeatFishSushi",
        "CMMREFoodSpicedApples",
        "CMMREFoodBonelessPorkRibs",
        "RMCMREFoodMeatSlab",
        "CMMREFoodCracker",
        "CMMREFoodBiscuit",
        "RMCFoodPizzaMargheritaFull",
        "CMMarinePreparedMealCornbread",
        "CMMarinePreparedMealChicken",
        "CMMarinePreparedMealPasta",
        "CMMarinePreparedMealPizza",
        "CMMarinePreparedMealPork",
        "CMMarinePreparedMealTofu",
        "RMCMarinePreparedMealSPPBanush",
        "RMCMarinePreparedMealSPPChowMein",
        "RMCMarinePreparedMealSPPCuban",
        "RMCMarinePreparedMealSPPFrankfurter",
        "RMCMarinePreparedMealSPPJiaozi",
        "RMCMarinePreparedMealSPPLuncheon",
        "RMCMarinePreparedMealSPPMeatballs",
        "RMCMarinePreparedMealSPPShrimp",
        "RMCMarinePreparedMealSPPWursts",
        "RMCMarinePreparedMealPMCIkanBakar",
        "RMCMarinePreparedMealCookies",
        "RMCMarinePreparedMealGingerbread",
        "RMCMarinePreparedMealFruitcake",
        "RMCMarinePreparedMealRCMFish",
        "RMCMarinePreparedMealRCMKatsu",
        "RMCMarinePreparedMealRCMSausage",
        "RMCMarinePreparedMealRCMMeat",
        "RMCMarinePreparedMealRCMTikka",
        "RMCFoodSnackBarcaridine",
        "RMCFoodSnackKeplarCrisps",
        "RMCFoodSnackKeplarFlamehotCrisps",
        "RMCFoodSnackChipsPepper",
        "RMCFoodSnackEATBar",
        "RMCPlushieNyx",
        "CMPill",
        "CMSoap",
    };

    private static readonly string[] MigratedSlicingTools =
    {
        "CMUMobApe",
        "CMUYautjaWristBlades",
        "CMUYautjaScimitar",
        "CMUYautjaHarpoon",
        "CMUYautjaChainwhip",
        "CMUYautjaClanSword",
        "CMUYautjaDualWarScythe",
        "CMUYautjaCombistick",
        "CMUYautjaWarAxe",
        "CMUYautjaCeremonialDagger",
        "CMUYautjaHunterSpear",
        "CMUYautjaWarGlaive",
        "CMUYautjaDuellingBlade",
        "CMUYautjaDuellingHatchet",
        "CMUYautjaDuellingKnife",
        "CMShardGlass",
        "RMCPickaxe",
    };

    private static readonly RefinableContract[] RefinableCorpses =
    {
        new("CMUMobSmallHostCarp", "FoodMeatFish", 2, false),
        new("CMUMobCarpInvasive", "FoodMeatFish", 8, false),
        new("CMMobWiggles", "FoodMeat", 2, false),
        new("CMMobMouse", "FoodMeatRat", 1, false),
        new("CMMobHuman", "FoodMeatHuman", 6, true),
    };

    private static readonly string[] UpstreamSpikeButcherables =
    {
        "MobArachnid",
        "MobDiona",
        "MobGingerbread",
        "MobHuman",
        "MobReptilian",
        "MobSlimePerson",
        "MobVox",
        "MobVulpkanin",
        "MobMonkey",
        "MobKobold",
        "MobArgocyteFounder",
        "MobArgocyteLeviathing",
        "MobMonkeyPunpun",
        "MobScurret",
        "MobXeno",
        "MobXenoRouny",
        "MobXenoLonePraetorianNoGhost",
    };

    private static readonly SolutionContract[] FoodSolutions =
    {
        new("FoodBurgerCorgi", new() { ["Nutriment"] = 20, ["CMBicaridine"] = 20, ["Vitamin"] = 10 }),
        new("FoodMeatCorgi", new() { ["Nutriment"] = 10, ["CMBicaridine"] = 20 }),
        new("FoodSaladWatermelonFruitBowl", new()
        {
            ["Nutriment"] = 45, ["Vitamin"] = 60, ["Water"] = 5,
            ["CMBicaridine"] = 5, ["CMKelotane"] = 5,
        }),
        new("FoodSaladAesir", new() { ["Nutriment"] = 30, ["Vitamin"] = 15, ["CMTricordrazine"] = 8 }),
        new("FoodSaladHerb", new()
        {
            ["Nutriment"] = 19, ["Vitamin"] = 11, ["CMBicaridine"] = 15, ["CMKelotane"] = 15,
        }),
        new("FoodSaladJungle", new()
        {
            ["Nutriment"] = 19, ["Vitamin"] = 13, ["JuiceBanana"] = 5, ["JuiceApple"] = 5,
            ["JuiceGrape"] = 5, ["Sugar"] = 1, ["JuiceWatermelon"] = 12,
        }),
        new("FoodSaladEden", new() { ["Nutriment"] = 25, ["Vitamin"] = 15, ["CMTricordrazine"] = 5 }),
        new("FoodRicePork", new()
        {
            ["Nutriment"] = 42, ["Protein"] = 6, ["CMDexalin"] = 6.5, ["CMEpinephrine"] = 2,
        }),
        new("FoodSoupNettle", new()
        {
            ["Nutriment"] = 12, ["Vitamin"] = 5, ["CMTricordrazine"] = 3,
            ["SpaceDrugs"] = 10, ["Histamine"] = 3,
        }),
    };

    [Test]
    public async Task MigratedRmcEdiblesAndPillContractDeserialize()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(MigratedEdibles, Has.Length.EqualTo(75));
            foreach (var id in MigratedEdibles)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryComp<EdibleComponent>(out _, factory), Is.True, id);
            }

            var pill = prototypes.Index<EntityPrototype>("CMPill");
            Assert.That(pill.TryComp<EdibleComponent>(out var edible, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(edible!.Edible.Id, Is.EqualTo("Pill"));
                Assert.That(edible.Delay, Is.EqualTo(TimeSpan.Zero));
                Assert.That(edible.ForceFeedDelay, Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(edible.TransferAmount, Is.Null);
                Assert.That(edible.UseSound, Is.Null,
                    "CMPill must use the Pill edible prototype so its historic audio parameters are retained.");
            });

            var pillStyle = prototypes.Index<EdiblePrototype>("Pill");
            Assert.That(pillStyle.Message.Id, Is.EqualTo("edible-swallow"));
            Assert.That(pillStyle.UseSound, Is.TypeOf<SoundPathSpecifier>());
            var pillSound = (SoundPathSpecifier) pillStyle.UseSound;
            Assert.Multiple(() =>
            {
                Assert.That(pillSound.Path.ToString(), Is.EqualTo("/Audio/Items/pill.ogg"));
                Assert.That(pillSound.Params.Volume, Is.EqualTo(-1));
                Assert.That(pillSound.Params.Variation, Is.EqualTo(0.2f));
            });

            foreach (var id in new[]
                     {
                         "FoodBakedChevreChaudCotton",
                         "FoodBakedCroissantCotton",
                         "FoodBakedGrilledCheeseSandwichCotton",
                     })
            {
                var cotton = prototypes.Index<EntityPrototype>(id);
                Assert.That(cotton.TryComp<EdibleComponent>(out var cottonEdible, factory), Is.True, id);
                Assert.That(cottonEdible!.RequiresSpecialDigestion, Is.False, id);
            }

            var mcrib = prototypes.Index<EntityPrototype>("FoodBurgerMcrib");
            Assert.That(mcrib.TryComp<EdibleComponent>(out var mcribEdible, factory), Is.True);
            Assert.That(mcribEdible!.Trash.Select(id => id.Id), Is.EqualTo(new[] { "FoodKebabSkewer" }));

            var cannoli = prototypes.Index<EntityPrototype>("FoodBakedCannoli");
            Assert.That(cannoli.TryComp<FlavorProfileComponent>(out var flavor, factory), Is.True);
            Assert.That(flavor!.Flavors.Select(id => id.Id), Is.EquivalentTo(new[] { "crunchy", "creamy" }));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FoodSolutionsAndRecipesPreserveSchemaAndForkReagents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var contract in FoodSolutions)
            {
                var prototype = prototypes.Index<EntityPrototype>(contract.Id);
                Assert.That(prototype.TryComp<SolutionComponent>(out var solution, factory), Is.True, contract.Id);
                var actual = solution!.Solution.Contents.ToDictionary(
                    entry => entry.Reagent.Prototype.Id,
                    entry => entry.Quantity);
                Assert.That(actual, Is.EquivalentTo(contract.Reagents), contract.Id);
            }

            AssertRecipe(prototypes, "RecipeSuperBiteBurger",
                new Dictionary<string, FixedPoint2> { ["TableSalt"] = 5, ["Blackpepper"] = 5, ["Egg"] = 12 });
            AssertRecipe(prototypes, "RecipeRicePudding",
                new Dictionary<string, FixedPoint2> { ["Rice"] = 10, ["Milk"] = 30, ["Egg"] = 6, ["RMCSugar"] = 5 });
            AssertRecipe(prototypes, "RecipeColdChili", new Dictionary<string, FixedPoint2> { ["RMCNitrogen"] = 5 });

            Assert.That(prototypes.Index<FoodRecipePrototype>("RecipeCannoli").Result.Id,
                Is.EqualTo("FoodBakedCannoli"));
            Assert.That(prototypes.Index<FoodRecipePrototype>("RecipeCornedBeef").Result.Id,
                Is.EqualTo("FoodMealCornedbeef"));
            Assert.That(prototypes.Index<FoodRecipePrototype>("RecipePretzel").Result.Id,
                Is.EqualTo("FoodBakedPretzel"));
            Assert.That(prototypes.Index<FoodRecipePrototype>("RecipeMoproach").Result.Id,
                Is.EqualTo("MobMoproach"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SharpAndKnifeTargetsUseToolRefinableSuccessors()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(MigratedSlicingTools, Has.Length.EqualTo(17));
            foreach (var id in MigratedSlicingTools)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryComp<ToolComponent>(out var tool, factory), Is.True, id);
                Assert.That(tool!.Qualities.Select(quality => quality.Id), Does.Contain("Slicing"), id);
                Assert.That(tool.SpeedModifier, Is.EqualTo(id == "CMUMobApe" ? 2f : 1f), id);
            }

            foreach (var contract in RefinableCorpses)
            {
                var prototype = prototypes.Index<EntityPrototype>(contract.Id);
                Assert.That(prototype.TryComp<ButcherableComponent>(out var butcherable, factory), Is.True,
                    contract.Id);
                Assert.That(prototype.TryComp<ToolRefinableComponent>(out var refinable, factory), Is.True,
                    contract.Id);

                Assert.Multiple(() =>
                {
                    Assert.That(butcherable!.Type, Is.EqualTo(ButcheringType.Knife), contract.Id);
                    Assert.That(butcherable.WaitForRot, Is.EqualTo(contract.WaitForRot), contract.Id);
                    AssertSpawn(butcherable.SpawnedEntities, contract);
                    Assert.That(refinable!.QualityNeeded.Id, Is.EqualTo("Slicing"), contract.Id);
                    Assert.That(refinable.RequiredUtensil, Is.EqualTo(UtensilType.None), contract.Id);
                    Assert.That(refinable.RefineTime, Is.EqualTo(TimeSpan.FromSeconds(8)), contract.Id);
                    AssertSpawn(refinable.RefineResult, contract);
                    Assert.That(refinable.VerbText, Is.Not.Null, contract.Id);
                    Assert.That(refinable.VerbDefaultTooltip, Is.Not.Null, contract.Id);
                    Assert.That(refinable.ToolMissingQualityTooltip, Is.Not.Null, contract.Id);
                    Assert.That(refinable.PopupForUser, Is.Not.Null, contract.Id);
                    Assert.That(refinable.PopupForOther, Is.Not.Null, contract.Id);
                    Assert.That(refinable.PopupType, Is.EqualTo(PopupType.LargeCaution), contract.Id);
                    Assert.That(refinable.Sound, Is.TypeOf<SoundPathSpecifier>(), contract.Id);
                    Assert.That(((SoundPathSpecifier) refinable.Sound!).Path.ToString(),
                        Is.EqualTo("/Audio/Items/Culinary/chop.ogg"), contract.Id);
                });
            }

            Assert.That(UpstreamSpikeButcherables, Has.Length.EqualTo(17));
            foreach (var id in UpstreamSpikeButcherables)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryComp<ButcherableComponent>(out var butcherable, factory), Is.True, id);
                Assert.That(butcherable!.Type, Is.EqualTo(ButcheringType.Spike), id);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertRecipe(
        IPrototypeManager prototypes,
        string id,
        IReadOnlyDictionary<string, FixedPoint2> expected)
    {
        var recipe = prototypes.Index<FoodRecipePrototype>(id);
        var actual = recipe.IngredientsReagents.ToDictionary(pair => pair.Key.Id, pair => pair.Value);
        Assert.That(actual, Is.EquivalentTo(expected), id);
    }

    private static void AssertSpawn(IReadOnlyList<Content.Shared.Storage.EntitySpawnEntry> entries,
        RefinableContract contract)
    {
        Assert.That(entries, Has.Count.EqualTo(1), contract.Id);
        Assert.Multiple(() =>
        {
            Assert.That(entries[0].PrototypeId?.Id, Is.EqualTo(contract.Output), contract.Id);
            Assert.That(entries[0].Amount, Is.EqualTo(contract.Amount), contract.Id);
            Assert.That(entries[0].MaxAmount, Is.EqualTo(1), contract.Id);
        });
    }

    private sealed record RefinableContract(string Id, string Output, int Amount, bool WaitForRot);
    private sealed record SolutionContract(string Id, Dictionary<string, FixedPoint2> Reagents);
}
