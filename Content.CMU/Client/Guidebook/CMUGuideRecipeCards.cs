using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Guidebook;

/// <summary>
/// Builds one chemistry-style card per material produced by a RUMC machine or production station.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUGuideRecipeCards : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public CMUGuideRecipeCards()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        MouseFilter = MouseFilterMode.Stop;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        var catalog = new CMUGuideRecipeCatalog(_entities, _prototypes);
        var recipes = catalog.Entries.Where(entry => entry.StationGroup != null).ToList();

        var products = recipes
            .SelectMany(entry => entry.Outputs.Select(output => catalog.Canonical(output.Material)))
            .Distinct()
            .OrderBy(catalog.Name, StringComparer.CurrentCultureIgnoreCase);

        foreach (var product in products)
        {
            var productRecipes = recipes
                .Where(entry => entry.Outputs.Any(output => catalog.Canonical(output.Material) == product))
                .OrderBy(entry => entry.Method, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var recipeIds = productRecipes.Select(entry => entry.Id).ToHashSet();
            var otherSources = catalog.Sources(product)?
                .Where(id => !recipeIds.Contains(id))
                .Select(catalog.TryGet)
                .Where(entry => entry != null)
                .Cast<CMUGuideRecipe>()
                .OrderBy(entry => entry.Method, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList() ?? new List<CMUGuideRecipe>();

            AddChild(new CMUGuideRecipeCard(catalog, product, productRecipes, otherSources));
        }

        control = this;
        return true;
    }
}
