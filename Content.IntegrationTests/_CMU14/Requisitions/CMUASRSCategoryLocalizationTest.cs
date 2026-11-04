using System.Linq;
using Content.Client._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Requisitions;

/// <summary>
///     The ASRS client resolves every catalog category through a fixed locale map. A category that
///     is missing from it once threw during BUI state handling and trapped the client in a
///     full-state refresh loop, so every category any console can show must be mapped.
/// </summary>
[TestFixture]
public sealed class CMUASRSCategoryLocalizationTest
{
    [Test]
    public async Task EveryCatalogCategoryHasAClientLocaleKey()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var loc = client.ResolveDependency<ILocalizationManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            var categories = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(p => !p.Abstract && !pair.IsTestPrototype(p))
                .Select(p => p.TryComp<RequisitionsComputerComponent>(out var computer, factory) ? computer : null)
                .Where(c => c != null)
                .SelectMany(c => c!.Categories.Select(category => category.Name))
                .ToHashSet();

            Assert.That(categories, Does.Contain("Anti-Armor"), "The ATGM category should be reachable.");

            Assert.Multiple(() =>
            {
                foreach (var category in categories.Order())
                {
                    Assert.That(RequisitionsBui.CategoryLocKeys.TryGetValue(category, out var key), Is.True,
                        $"ASRS category '{category}' has no entry in RequisitionsBui.CategoryLocKeys");

                    if (key != null)
                        Assert.That(loc.HasString(key), Is.True, $"Locale key '{key}' for '{category}' is missing");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
