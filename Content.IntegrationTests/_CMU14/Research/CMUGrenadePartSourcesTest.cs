using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Research;

/// <summary>
/// The custom chemical grenade parts (casing, two-beaker payload, timer trigger) are obtainable from the
/// WY Tool Vend, the CM autolathe and the sapper's workbench.
/// </summary>
[TestFixture]
public sealed class CMUGrenadePartSourcesTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private static readonly string[] Parts = ["RMCCustomGrenade", "ChemicalPayload", "RMCTimerTrigger"];

    // Materials the CM autolathe's material storage holds (Resources/Prototypes/_RMC14/.../Lathe/lathe.yml).
    private static readonly string[] AutolatheMaterials = ["CMSteel", "CMGlass", "RMCPlastic"];

    [Test]
    public async Task GrenadePartsAreObtainableFromToolVendAutolatheAndSapperBench()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = Server.ResolveDependency<IComponentFactory>();

            var vendor = SProtoMan.Index<EntityPrototype>("AU14CashVendorTools");
            Assert.That(vendor.TryGetComponent<AU14CashVendorComponent>(out var vend, factory), Is.True);
            var sold = vend!.Items.Select(i => i.ItemId.Id).ToHashSet();
            Assert.That(sold, Is.SupersetOf(Parts), "WY Tool Vend must sell every grenade part");

            var lathe = SProtoMan.Index<EntityPrototype>("CMAutolathe");
            Assert.That(lathe.TryGetComponent<LatheComponent>(out var latheComp, factory), Is.True);
            var recipes = latheComp!.StaticPacks
                .SelectMany(pack => SProtoMan.Index<LatheRecipePackPrototype>(pack).Recipes)
                .Select(id => SProtoMan.Index<LatheRecipePrototype>(id))
                .ToList();
            foreach (var part in Parts)
            {
                var recipe = recipes.FirstOrDefault(r => r.Result?.Id == part);
                Assert.That(recipe, Is.Not.Null, $"CM autolathe has no recipe for {part}");
                Assert.That(recipe!.Materials.Keys.Select(k => k.Id), Is.SubsetOf(AutolatheMaterials),
                    $"{recipe.ID} uses a material the CM autolathe cannot hold");
                Assert.That(recipe.Materials, Is.Not.Empty);
            }

            var bench = SProtoMan.Index<EntityPrototype>("AU14SapperWorkbench");
            Assert.That(bench.TryGetComponent<SapperWorkbenchComponent>(out var benchComp, factory), Is.True);
            var crafted = benchComp!.Recipes.Select(r => r.Prototype.Id).ToHashSet();
            Assert.That(crafted, Is.SupersetOf(Parts), "sapper's workbench must fabricate every grenade part");
        });
    }
}
