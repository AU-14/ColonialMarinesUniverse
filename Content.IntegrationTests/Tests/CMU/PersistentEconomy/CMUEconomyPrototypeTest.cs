using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Clothing;
using Content.Shared.CMU14.PersistentEconomy;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.CMU.PersistentEconomy;

// CMU14: load the real server/client prototype catalogue, not a mock of the YAML.
[TestFixture]
public sealed class CMUEconomyPrototypeTest : GameTest
{
    [Test]
    public async Task CatalogueHasValidRolesEntitiesPricesAndCategories()
    {
        var loadouts = Server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<LoadoutSystem>();
        await Server.WaitAssertion(() =>
        {
            var count = 0;
            foreach (var item in SProtoMan.EnumeratePrototypes<CMULoadoutItemPrototype>())
            {
                count++;
                Assert.That(SProtoMan.HasIndex(item.Entity), Is.True, item.ID);
                Assert.That(item.Price, Is.InRange(0L, 1_000_000_000_000L), item.ID);
                Assert.That(item.CategoryLimit, Is.InRange(1, 3), item.ID);
                Assert.That(item.Jobs, Is.Not.Empty, item.ID);
                foreach (var job in item.Jobs)
                {
                    Assert.That(SProtoMan.TryIndex<JobPrototype>(job, out var prototype), Is.True, item.ID + ": " + job);
                    Assert.That(prototype!.CmuEconomyEnabled, Is.True, job);
                    Assert.That(
                        SProtoMan.TryIndex<RoleLoadoutPrototype>(LoadoutSystem.GetJobPrototype(job), out var roleLoadout),
                        Is.True,
                        item.ID + ": missing role loadout for " + job);
                    Assert.That(
                        roleLoadout!.Groups
                            .SelectMany(group => SProtoMan.Index<LoadoutGroupPrototype>(group).Loadouts)
                            .Select(loadout => SProtoMan.Index<LoadoutPrototype>(loadout))
                            .Any(loadout => loadouts.GetFirstOrNull(loadout) == item.Entity),
                        Is.True,
                        item.ID + ": " + item.Entity + " is not selectable by " + job);
                }
            }
            Assert.That(count, Is.GreaterThanOrEqualTo(2));
        });

        await Client.WaitAssertion(() =>
        {
            var prototypes = Client.ResolveDependency<IPrototypeManager>();
            Assert.That(prototypes.HasIndex<CMULoadoutItemPrototype>("CMUPersonalBlackScarf"), Is.True);
        });
    }
}
