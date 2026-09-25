using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.Dropship.Fabricator;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
[TestOf(typeof(DropshipFabricatorSystem))]
public sealed class DropshipFabricatorTest : GameTest
{
    // Reproduce the Midway M90's inherited printable component and spawn-time removal.
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: RMCDropshipAttachmentGau21Cannon
  id: TestDropshipFixedGun
  components:
  - type: RemoveComponents
    components:
    - type: DropshipFabricatorPrintable
    - type: PowerLoaderDetachable

- type: entity
  parent: RMCDropshipAttachmentGau21Cannon
  id: TestDropshipPrintableGun
  components:
  - type: RemoveComponents
    components:
    - type: PowerLoaderDetachable
""";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task CatalogExcludesRemovedPrintablesOnBothSides()
    {
        await Server.WaitAssertion(() => AssertCatalog(SEntMan));
        await Client.WaitAssertion(() => AssertCatalog(CEntMan));
    }

    private static void AssertCatalog(IEntityManager entities)
    {
        var printables = entities.System<DropshipFabricatorSystem>().Printables.Select(id => id.Id).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(printables, Does.Not.Contain("TestDropshipFixedGun"));
            Assert.That(printables, Does.Contain("TestDropshipPrintableGun"));
            Assert.That(printables, Does.Contain("RMCDropshipAttachmentGau21Cannon"));
            Assert.That(printables, Does.Contain("RMCDropshipAttachmentAmmoGAU"));
        });
    }
}
