// Arrange faction ownership and account timers directly for transaction isolation tests.
#pragma warning disable RA0002

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Round;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._RMC14;

// CMU14: fabricator faction account regressions.
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

- type: entity
  id: TestDropshipBudgetPart
  components:
  - type: DropshipFabricatorPrintable
    cost: 100
    recycleMultiplier: 0.8
    delay: 600
""";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task CatalogExcludesRemovedPrintablesOnBothSides()
    {
        await Server.WaitAssertion(() => AssertCatalog(SEntMan));
        await Client.WaitAssertion(() => AssertCatalog(CEntMan));
    }

    [Test]
    public async Task FactionAccountsIsolatePurchasesRefundsIncomeAndTechRewardsAfterLateShipAssignment()
    {
        var govforMap = await Pair.CreateTestMap();
        var opforMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<DropshipFabricatorSystem>();
            var secondGovforCoords = govforMap.GridCoords.Offset(new Vector2(3, 0));
            for (var x = 1; x <= 3; x++)
                Server.System<SharedMapSystem>().SetTile(govforMap.Grid,
                    govforMap.GridCoords.Offset(new Vector2(x, 0)), govforMap.Tile.Tile);
            // Navigation console defaults must not establish ownership before the carrier is assigned.
            var navigation = SEntMan.SpawnEntity(null, opforMap.GridCoords);
            SEntMan.EnsureComponent<WhitelistedShuttleComponent>(navigation).Faction = "govfor";
            var govfor = SEntMan.SpawnEntity("RMCDropshipFabricator", govforMap.GridCoords);
            var secondGovfor = SEntMan.SpawnEntity("RMCDropshipFabricator", secondGovforCoords);
            var opfor = SEntMan.SpawnEntity("RMCDropshipFabricator", opforMap.GridCoords);
            Assert.That(SEntMan.GetComponent<TransformComponent>(govfor).GridUid, Is.EqualTo(govforMap.Grid.Owner));
            Assert.That(SEntMan.GetComponent<TransformComponent>(secondGovfor).GridUid, Is.EqualTo(govforMap.Grid.Owner));
            Assert.That(SEntMan.GetComponent<TransformComponent>(opfor).GridUid, Is.EqualTo(opforMap.Grid.Owner));
            var govFab = SEntMan.GetComponent<DropshipFabricatorComponent>(govfor);
            var secondGovFab = SEntMan.GetComponent<DropshipFabricatorComponent>(secondGovfor);
            var opFab = SEntMan.GetComponent<DropshipFabricatorComponent>(opfor);

            // Real carrier loading adds faction ownership after the fabricators' MapInit events.
            SEntMan.EnsureComponent<ShipFactionComponent>(govforMap.GridCoords.EntityId).Faction = "GOVFOR";
            SEntMan.EnsureComponent<ShipFactionComponent>(opforMap.GridCoords.EntityId).Faction = "opfor";
            system.Update(0);
            Assert.That(govFab.Account, Is.Not.Null);
            Assert.That(opFab.Account, Is.Not.EqualTo(govFab.Account));
            Assert.That(secondGovFab.Account, Is.EqualTo(govFab.Account));
            var govAccount = SEntMan.GetComponent<DropshipFabricatorPointsComponent>(govFab.Account!.Value);
            var opAccount = SEntMan.GetComponent<DropshipFabricatorPointsComponent>(opFab.Account!.Value);
            var govBalance = govAccount.Points;
            var opBalance = opAccount.Points;
            void AssertBalances()
            {
                Assert.That(govAccount.Points, Is.EqualTo(govBalance));
                Assert.That(opAccount.Points, Is.EqualTo(opBalance));
                Assert.That(govFab.Points, Is.EqualTo(govBalance));
                Assert.That(secondGovFab.Points, Is.EqualTo(govBalance));
                Assert.That(opFab.Points, Is.EqualTo(opBalance));
            }

            var actor = SEntMan.SpawnEntity("CMMobHuman", govforMap.GridCoords);
            for (var i = 0; i < 2; i++)
                SEntMan.EventBus.RaiseLocalEvent(govfor, new DropshipFabricatorPrintMsg("TestDropshipBudgetPart")
                    { Actor = actor, UiKey = DropshipFabricatorUi.Key });
            govBalance -= 200;
            Assert.That(govFab.Printing?.Id, Is.EqualTo("TestDropshipBudgetPart"));
            Assert.That(govFab.Queue, Has.Count.EqualTo(1));
            AssertBalances();

            SEntMan.EventBus.RaiseLocalEvent(govfor, new DropshipFabricatorCancelQueueMsg(0)
                { Actor = actor, UiKey = DropshipFabricatorUi.Key });
            govBalance += 100;
            Assert.That(govFab.Queue, Is.Empty);
            AssertBalances();

            var part = SEntMan.SpawnEntity("TestDropshipBudgetPart", opforMap.GridCoords);
            var recycle = new DropshipFabricatoreRecycleDoafterEvent();
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            recycle.DoAfter = new DoAfter(0, new DoAfterArgs(SEntMan, actor, 0, recycle, opfor, opfor, part), now);
            SEntMan.EventBus.RaiseLocalEvent(opfor, recycle);
            opBalance += 80;
            Assert.That(recycle.Handled, Is.True);
            AssertBalances();

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new TechDropshipBudgetEvent(500) { Team = "govfor" });
            govBalance += 500;
            AssertBalances();
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new TechDropshipBudgetEvent(300) { Team = "OPFOR" });
            opBalance += 300;
            AssertBalances();

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new TechDropshipBudgetEvent(25) { Team = Team.None });
            govBalance += 25;
            opBalance += 25;
            AssertBalances();

            // Different due times also catch UI updates leaking to the other account.
            govAccount.NextPointsAt = now;
            opAccount.NextPointsAt = now + TimeSpan.FromHours(1);
            system.Update(0);
            govBalance++;
            AssertBalances();
            opAccount.NextPointsAt = now;
            system.Update(0);
            opBalance++;
            AssertBalances();
        });
    }

    [Test]
    public async Task FabricatorsOnLinkedDecksUseTheCarrierFactionAccount()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var lower = maps.CreateMap(runMapInit: true);
            var upper = maps.CreateMap(runMapInit: true);
            SEntMan.EnsureComponent<MapGridComponent>(lower);
            SEntMan.EnsureComponent<MapGridComponent>(upper);
            SEntMan.EnsureComponent<ShipFactionComponent>(lower).Faction = "opfor";
            var zLevels = Server.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);
            var lowerFabricator = SEntMan.SpawnEntity("RMCDropshipFabricator", new EntityCoordinates(lower, 0, 0));
            var upperFabricator = SEntMan.SpawnEntity("RMCDropshipFabricator", new EntityCoordinates(upper, 0, 0));
            var lowerAccount = SEntMan.GetComponent<DropshipFabricatorComponent>(lowerFabricator).Account;
            var upperAccount = SEntMan.GetComponent<DropshipFabricatorComponent>(upperFabricator).Account;
            Assert.That(lowerAccount, Is.Not.Null);
            Assert.That(upperAccount, Is.EqualTo(lowerAccount));
            Assert.That(SEntMan.GetComponent<DropshipFabricatorPointsComponent>(upperAccount!.Value).Faction,
                Is.EqualTo("opfor"));
        });
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
