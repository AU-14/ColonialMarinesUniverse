using Content.Shared.Kitchen;

namespace Content.IntegrationTests.Tests.Nutrition;

[TestFixture]
public sealed class RecipeManagerMergeRegressionTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: microwaveMealRecipe
  id: RecipeManagerMergeSimple
  result: FoodBakedCannoli
  reagents:
    Water: 1

- type: microwaveMealRecipe
  id: RecipeManagerMergeComplex
  result: FoodBakedCannoli
  reagents:
    Water: 1
  solids:
    FoodBreadPlainSlice: 2

- type: microwaveMealRecipe
  id: RecipeManagerMergeSecret
  result: FoodBakedCannoli
  secretRecipe: true
  solids:
    FoodBreadPlainSlice: 4
";

    [Test]
    public async Task StartupExcludesSecretRecipesAndSortsByIngredientCountDescending()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var manager = server.EntMan.System<RecipeManager>();
            var ids = manager.Recipes.Select(recipe => recipe.ID).ToList();
            var ingredientCounts = manager.Recipes.Select(recipe => recipe.IngredientCount()).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(manager.Recipes, Is.Not.Empty);
                Assert.That(manager.Recipes, Has.All.Matches<FoodRecipePrototype>(recipe => !recipe.SecretRecipe));
                Assert.That(ids, Does.Contain("RecipeManagerMergeSimple"));
                Assert.That(ids, Does.Contain("RecipeManagerMergeComplex"));
                Assert.That(ids, Does.Not.Contain("RecipeManagerMergeSecret"));
                Assert.That(ids.IndexOf("RecipeManagerMergeComplex"),
                    Is.LessThan(ids.IndexOf("RecipeManagerMergeSimple")),
                    "The more specific overlapping recipe must be tried first.");
                Assert.That(ingredientCounts, Is.Ordered.Descending);
            });
        });

        await pair.CleanReturnAsync();
    }
}
