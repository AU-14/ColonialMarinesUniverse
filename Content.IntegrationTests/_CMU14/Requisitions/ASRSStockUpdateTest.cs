#pragma warning disable RA0002 // Regression setup drives authoritative stock deadlines without waiting in real time.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Requisitions;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.util;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Requisitions;

[TestFixture]
[TestOf(typeof(RequisitionsSystem))]
public sealed class ASRSStockUpdateTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseItem
          id: ASRSStockFiniteItem

        - type: entity
          parent: BaseItem
          id: ASRSStockUnlimitedItem

        - type: entity
          parent: RMCCrateBase
          id: ASRSStockRandomBundle
          components:
          - type: StorageFill
            contents:
            - id: ASRSStockFiniteItem
              prob: 0.5

        - type: entity
          parent: RMCCrateBase
          id: ASRSStockTwoItemBundle
          components:
          - type: StorageFill
            contents:
            - id: ASRSStockFiniteItem
              amount: 2

        - type: entity
          parent: RMCCrateBase
          id: ASRSStockMixedBundle
          components:
          - type: StorageFill
            contents:
            - id: ASRSStockFiniteItem
              amount: 3
            - id: ASRSStockUnlimitedItem

        - type: entity
          parent: RMCCrateBase
          id: ASRSStockUnlimitedBundle
          components:
          - type: StorageFill
            contents:
            - id: ASRSStockUnlimitedItem

        - type: entity
          id: ASRSStockComputerBase
          abstract: true
          components:
          - type: UserInterface
            interfaces:
              enum.RequisitionsUIKey.Key:
                type: RequisitionsBui

        - type: entity
          parent: ASRSStockComputerBase
          id: ASRSStockLegacyComputer
          components:
          - type: RequisitionsComputer
            faction: asrs-stock-update-test
            categories:
            - name: Test
              entries:
              - cost: 100
                crate: ASRSStockRandomBundle
                maxStock: 3
                startingStock: 2
                stockReplenishDelay: 10

        - type: entity
          parent: ASRSStockComputerBase
          id: ASRSStockItemizedComputer
          components:
          - type: RequisitionsComputer
            faction: asrs-stock-update-test
            categories:
            - name: Test
              entries:
              - cost: 200
                crate: ASRSStockTwoItemBundle
                maxStock: 2
                stockReplenishDelay: 20
              - cost: 400
                crate: ASRSStockMixedBundle
                maxStock: 3
                stockReplenishDelay: 10
              - cost: 100
                crate: ASRSStockUnlimitedBundle

        - type: platoon
          id: ASRSStockTestPlatoon
          name: Stock regression platoon
          reqlist: ASRSStockLegacyComputer

        - type: entity
          parent: ASRSStockComputerBase
          id: ASRSStockPlatoonComputer
          components:
          - type: RequisitionsComputer
            faction: govfor
            categories: []
        """;

    [Test]
    public async Task LegacyDepletionSurvivesUnchangedUpdatesAndOverdueReplenishment()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity("ASRSStockLegacyComputer", map.GridCoords);
            var computer = SEntMan.GetComponent<RequisitionsComputerComponent>(uid);
            var system = Server.System<RequisitionsSystem>();
            var stock = computer.Stock[(0, 0)];
            var deadline = stock.NextReplenish;

            Assert.That(stock.Current, Is.EqualTo(2));
            Assert.That(computer.ItemCatalog, Is.Empty,
                "random bundles must exercise the legacy stock path");
            Assert.That(system.TryReserveStock((uid, computer), 0, 0), Is.True);
            Assert.That(system.TryReserveStock((uid, computer), 0, 0), Is.True);
            Assert.That(system.TryReserveStock((uid, computer), 0, 0), Is.False);

            for (var i = 0; i < 4; i++)
                system.Update(0f);

            Assert.That(computer.Stock[(0, 0)], Is.SameAs(stock));
            Assert.That(stock.Current, Is.Zero, "ordinary updates must not restore starting stock");
            Assert.That(stock.NextReplenish, Is.EqualTo(deadline),
                "ordinary updates must not restart the replenishment timer");

            // Three replenishments are due. Catch up in one update and stop at the cap.
            stock.NextReplenish = SGameTiming.CurTime - TimeSpan.FromSeconds(20);
            system.Update(0f);

            Assert.That(stock.Current, Is.EqualTo(3));
            Assert.That(stock.NextReplenish, Is.EqualTo(TimeSpan.Zero));
            Assert.That(GetState(uid).Stock.Single().Current, Is.EqualTo(3));
            Assert.That(GetState(uid).Stock.Single().SecondsUntilNextReplenish, Is.Zero);

            Assert.That(system.TryReserveStock((uid, computer), 0, 0), Is.True);
            Assert.That(stock.Current, Is.EqualTo(2));
            Assert.That(stock.NextReplenish, Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromSeconds(10)));
        });
    }

    [Test]
    public async Task AppendingLegacyEntryInitializesNewStockWithoutResettingExistingStock()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity("ASRSStockLegacyComputer", map.GridCoords);
            var computer = SEntMan.GetComponent<RequisitionsComputerComponent>(uid);
            var system = Server.System<RequisitionsSystem>();
            Assert.That(system.TryReserveStock((uid, computer), 0, 0), Is.True);
            var original = computer.Stock[(0, 0)];
            var deadline = original.NextReplenish;

            system.AddEntryToCategory(uid, computer, "Test", new RequisitionsEntry
            {
                Crate = "ASRSStockRandomBundle",
                Cost = 100,
                MaxStock = 2,
                StartingStock = 1,
                StockReplenishDelay = TimeSpan.FromMinutes(1),
            });
            system.Update(0f);

            Assert.That(computer.Stock, Has.Count.EqualTo(2));
            Assert.That(computer.Stock[(0, 0)], Is.SameAs(original));
            Assert.That(original.Current, Is.EqualTo(1));
            Assert.That(original.NextReplenish, Is.EqualTo(deadline));
            var appended = computer.Stock[(0, 1)];
            Assert.That(appended.Current, Is.EqualTo(1));
            Assert.That(appended.NextReplenish, Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromMinutes(1)));
            Assert.That(system.TryReserveStock((uid, computer), 0, 1), Is.True);
            Assert.That(system.TryReserveStock((uid, computer), 0, 1), Is.False);
            Assert.That(original.Current, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SelectedPlatoonCatalogIsIsolatedAndReapplicationResetsStock()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<RequisitionsSystem>();
            var platoons = Server.System<PlatoonSpawnRuleSystem>();
            var previous = platoons.SelectedGovforPlatoon;
            var selected = SProtoMan.Index<PlatoonPrototype>("ASRSStockTestPlatoon");
            var prototype = SProtoMan.Index<EntityPrototype>("ASRSStockLegacyComputer");
            var catalog = (RequisitionsComputerComponent) prototype.Components["RequisitionsComputer"].Component;

            try
            {
                platoons.SelectedGovforPlatoon = selected;
                var firstUid = SEntMan.SpawnEntity("ASRSStockPlatoonComputer", map.GridCoords);
                var secondUid = SEntMan.SpawnEntity("ASRSStockPlatoonComputer", map.GridCoords);
                var first = SEntMan.GetComponent<RequisitionsComputerComponent>(firstUid);
                var second = SEntMan.GetComponent<RequisitionsComputerComponent>(secondUid);
                Assert.That(first.Categories[0].Name, Is.EqualTo("Test"));
                Assert.That(first.Categories.Single(category => category.Name == "Research").Entries,
                    Has.Count.EqualTo(1), "Faction research terminals remain in the live catalog.");
                Assert.That(system.TryReserveStock((firstUid, first), 0, 0), Is.True);

                system.AddEntryToCategory(firstUid, first, "Test", new RequisitionsEntry
                {
                    Crate = "ASRSStockRandomBundle",
                    Cost = 100,
                    MaxStock = 2,
                    StartingStock = 1,
                });
                system.Update(0f);

                Assert.That(first.Categories.Single(category => category.Name == "Test").Entries, Has.Count.EqualTo(2));
                Assert.That(first.Stock, Has.Count.EqualTo(2));
                Assert.That(first.Stock[(0, 0)].Current, Is.EqualTo(1));
                Assert.That(second.Categories.Single(category => category.Name == "Test").Entries, Has.Count.EqualTo(1),
                    "appending to one console must not alter another console's catalog");
                Assert.That(second.Stock, Has.Count.EqualTo(1));
                Assert.That(second.Stock[(0, 0)].Current, Is.EqualTo(2));
                Assert.That(catalog.Categories.Single(category => category.Name == "Test").Entries, Has.Count.EqualTo(1),
                    "runtime catalog additions must not mutate the prototype");
                Assert.That(first.Categories[0].Entries[0], Is.Not.SameAs(second.Categories[0].Entries[0]));
                Assert.That(first.Categories[0].Entries[0], Is.Not.SameAs(catalog.Categories[0].Entries[0]));

                // Selection changes rebuild stock through the same public lifecycle used by the round.
                platoons.SelectedGovforPlatoon = selected;
                Assert.That(first.Categories.Single(category => category.Name == "Test").Entries, Has.Count.EqualTo(1));
                Assert.That(first.Stock, Has.Count.EqualTo(1));
                Assert.That(first.Stock[(0, 0)].Current, Is.EqualTo(2));
                Assert.That(first.Stock[(0, 0)].NextReplenish,
                    Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromSeconds(10)));
                Assert.That(second.Categories.Single(category => category.Name == "Test").Entries, Has.Count.EqualTo(1));
                Assert.That(catalog.Categories.Single(category => category.Name == "Test").Entries, Has.Count.EqualTo(1));
            }
            finally
            {
                platoons.SelectedGovforPlatoon = previous;
            }
        });
    }

    [Test]
    public async Task CountdownRefreshOnlyReplacesDueTerminalState()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var dueUid = SEntMan.SpawnEntity("ASRSStockLegacyComputer", map.GridCoords);
            var otherUid = SEntMan.SpawnEntity("ASRSStockLegacyComputer", map.GridCoords);
            var due = SEntMan.GetComponent<RequisitionsComputerComponent>(dueUid);
            var other = SEntMan.GetComponent<RequisitionsComputerComponent>(otherUid);
            var system = Server.System<RequisitionsSystem>();

            // A reservation publishes both terminals through the existing global path.
            Assert.That(system.TryReserveStock((dueUid, due), 0, 0), Is.True);
            var dueBefore = GetState(dueUid);
            var otherBefore = GetState(otherUid);
            due.Stock[(0, 0)].NextReplenish = SGameTiming.CurTime + TimeSpan.FromSeconds(5);
            due.NextStockUiUpdate = TimeSpan.Zero;
            other.NextStockUiUpdate = SGameTiming.CurTime + TimeSpan.FromMinutes(1);

            system.Update(0f);

            var dueAfter = GetState(dueUid);
            Assert.That(dueAfter, Is.Not.SameAs(dueBefore));
            Assert.That(dueAfter.Stock.Single().Current, Is.EqualTo(1));
            Assert.That(dueAfter.Stock.Single().SecondsUntilNextReplenish, Is.EqualTo(5));
            Assert.That(GetState(otherUid), Is.SameAs(otherBefore),
                "one terminal's countdown must not rebuild another terminal's UI state");

            system.Update(0f);
            Assert.That(GetState(dueUid), Is.SameAs(dueAfter),
                "the next update before the countdown interval must not publish again");
            Assert.That(GetState(otherUid), Is.SameAs(otherBefore));
        });
    }

    [Test]
    public async Task ItemStockSumsFiniteSourcesAndOmitsItemsWithAnUnlimitedSource()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity("ASRSStockItemizedComputer", map.GridCoords);
            var computer = SEntMan.GetComponent<RequisitionsComputerComponent>(uid);
            var system = Server.System<RequisitionsSystem>();
            system.ChangeBudget(0);

            Assert.That(computer.ItemCatalog.Select(item => item.Prototype.Id),
                Is.EquivalentTo(new[] { "ASRSStockFiniteItem", "ASRSStockUnlimitedItem" }));
            var full = GetState(uid).ItemStock.Single();
            Assert.That(full.Prototype.Id, Is.EqualTo("ASRSStockFiniteItem"),
                "a finite source must not impose a limit when another source is unlimited");
            Assert.That(full.Current, Is.EqualTo(13));
            Assert.That(full.Max, Is.EqualTo(13));
            Assert.That(full.SecondsUntilNextReplenish, Is.Zero);

            Assert.That(system.TryReserveStock((uid, computer), 0, 0), Is.True);
            var firstDepleted = GetState(uid).ItemStock.Single();
            Assert.That(firstDepleted.Current, Is.EqualTo(11));
            Assert.That(firstDepleted.Max, Is.EqualTo(13));
            Assert.That(firstDepleted.SecondsUntilNextReplenish, Is.EqualTo(20),
                "the full source's zero countdown must not hide a depleted source's timer");

            Assert.That(system.TryReserveStock((uid, computer), 0, 1), Is.True);
            var bothDepleted = GetState(uid).ItemStock.Single();
            Assert.That(bothDepleted.Current, Is.EqualTo(8));
            Assert.That(bothDepleted.Max, Is.EqualTo(13));
            Assert.That(bothDepleted.SecondsUntilNextReplenish, Is.EqualTo(10),
                "the UI must use the earliest timer among depleted sources");
        });
    }

    private RequisitionsBuiState GetState(EntityUid uid)
    {
        var ui = Server.System<UserInterfaceSystem>();
        Assert.That(ui.TryGetUiState<RequisitionsBuiState>(uid, RequisitionsUIKey.Key, out var state), Is.True);
        return state!;
    }
}
