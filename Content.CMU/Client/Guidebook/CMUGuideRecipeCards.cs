using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Guidebook;

/// <summary>
/// Builds one chemistry-style card per material produced by a RUMC machine or production station.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUGuideRecipeCards : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;

    public CMUGuideRecipeCards()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        MouseFilter = MouseFilterMode.Stop;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        var catalog = _entities.System<CMUGuideRecipeCatalogSystem>().GetCatalog();
        // Include every producible route: powered/steam machines, hand crafting,
        // tool processing, casting and other stations. Non-plannable gathering
        // entries remain visible as sources rather than primary recipes.
        var recipes = catalog.Entries.Where(entry => entry.CanPlan).ToList();
        var products = recipes
            .SelectMany(entry => entry.Outputs.Select(output => catalog.Canonical(output.Material)))
            .Distinct()
            .OrderBy(catalog.Name, StringComparer.CurrentCultureIgnoreCase);

        foreach (var product in products)
        {
            var productRecipes = new List<CMUGuideRecipe>();
            var otherSources = new List<CMUGuideRecipe>();
            foreach (var entry in catalog.Sources(product)!
                         .Select(catalog.TryGet)
                         .OfType<CMUGuideRecipe>()
                         .OrderBy(entry => entry.Method, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (entry.CanPlan)
                    productRecipes.Add(entry);
                else
                    otherSources.Add(entry);
            }

            AddChild(new CMUGuideRecipeCard(catalog, product, productRecipes, otherSources));
        }

        control = this;
        return true;
    }
}
