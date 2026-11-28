using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Synthesizer;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Chemistry;

/// <summary>
/// N6 (GraphAudit-ChemBotanyPlan): the product → reaction index skips breakdowns and generated ignition reactions
/// and picks up reactions generated mid-round.
/// </summary>
[TestFixture]
public sealed class CMUReactionProductIndexTest : GameTest
{
    // Generated prototypes survive entity cleanup and would change later tests' reactions.
    public override PoolSettings PoolSettings => new() { Connected = false, Destructive = true };

    /// <summary>Ingredients known not to collide with a hand-authored reaction (see CMUProcgenIgnitionHeatTest).</summary>
    private static readonly string[] Ingredients = ["RMCGold", "RMCPlatinum", "CMUTearGas"];

    [Test]
    public async Task StockProductResolves()
    {
        await Server.WaitAssertion(() =>
        {
            var index = Server.System<CMUReactionProductIndexSystem>();
            Assert.That(index.TryGetRecipes("CMUPhenolFormaldehyde", out var recipes), Is.True);
            Assert.That(recipes!.Select(r => r.ID), Does.Contain("CMUPhenolFormaldehyde"));

            // every list is in engine order
            for (var i = 1; i < recipes.Count; i++)
            {
                Assert.That(recipes[i - 1].CompareTo(recipes[i]), Is.LessThanOrEqualTo(0));
            }
        });
    }

    [Test]
    public async Task BreakdownReactionsAreNotRecipes()
    {
        await Server.WaitAssertion(() =>
        {
            var index = Server.System<CMUReactionProductIndexSystem>();
            var checkedAny = false;
            foreach (var reaction in SProtoMan.EnumeratePrototypes<ReactionPrototype>())
            {
                if (!reaction.Source)
                    continue;

                foreach (var product in reaction.Products.Keys)
                {
                    checkedAny = true;
                    if (index.TryGetRecipes(product.Id, out var recipes))
                    {
                        Assert.That(recipes.Select(r => r.ID), Does.Not.Contain(reaction.ID),
                            $"breakdown {reaction.ID} was listed as a recipe for {product.Id}");
                    }
                }
            }

            Assert.That(checkedAny, Is.True, "expected at least one breakdown (source: true) reaction with products");
        });
    }

    [Test]
    public async Task GeneratedReagentResolvesToItsRecipe()
    {
        await Server.WaitAssertion(() =>
        {
            var index = Server.System<CMUReactionProductIndexSystem>();
            const string id = "CMUProductIndexTest-Fuel";

            // build (and cache) the index before the reagent exists
            Assert.That(index.TryGetRecipes(id, out _), Is.False);

            var data = new GeneratedReagentData
            {
                ID = id,
                Name = id,
                GenTier = 2,
                Class = ReagentClass.Ultra,
                Effects = [],
                Recipe = [],
            };
            // Fueling gives the reagent an {ID}Ignition reaction as well
            data.Effects["Fueling"] = 6;
            foreach (var ingredient in Ingredients)
                data.Recipe[ingredient] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));

            var ignitionId = SharedReagentGeneratorSystem.IgnitionRecipeId(id);
            Assert.That(SProtoMan.HasIndex<ReactionPrototype>(ignitionId), Is.True, "the test reagent should be ignitable");

            Assert.That(index.TryGetRecipes(id, out var recipes), Is.True, "the index was not rebuilt after generation");
            Assert.Multiple(() =>
            {
                Assert.That(recipes!.Select(r => r.ID), Does.Contain(id));
                Assert.That(recipes!.Select(r => r.ID), Does.Not.Contain(ignitionId));
                Assert.That(recipes![0].ID, Is.EqualTo(id));
            });
        });
    }
}
