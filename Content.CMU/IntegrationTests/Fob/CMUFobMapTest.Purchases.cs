#pragma warning disable RA0002 // Drive catalog and checkout events against the actual faction account and elevator.

using System.Linq;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.Requisitions;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobMapTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task AdditionalCratePurchasesShareRoundLimitsAcrossReplacementConsoles(bool enabled)
    {
        Entity<RequisitionsElevatorComponent> lift = default;
        await Server.WaitAssertion(() =>
        {
            LoadSurface();
            StartRound(enabled);
            lift = CreateSupplyLift();
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(lift).Coordinates;
            var user = SEntMan.SpawnAtPosition("CMMobHuman", coords);
            var first = SEntMan.SpawnAtPosition("CMASRSConsoleGovfor", coords);
            var computer = SEntMan.GetComponent<RequisitionsComputerComponent>(first);
            var category = computer.Categories.FindIndex(c => c.Entries.Any(e => e.Crate.Id == "CMUFobWeaponryCrate"));
            if (!enabled)
            {
                Assert.That(category, Is.EqualTo(-1), "Default-off rounds cannot buy FOB hardware.");
                return;
            }
            Assert.That(category, Is.GreaterThanOrEqualTo(0));
            Assert.That(computer.ItemCatalog.Any(e => e.Prototype.Id.StartsWith("CMUFob")), Is.False,
                "Itemized checkout cannot bypass the whole-crate limit.");
            var account = SEntMan.GetComponent<RequisitionsAccountComponent>(computer.Account!.Value);
            var initialBalance = account.Balance;
            var weapon = computer.Categories[category].Entries.FindIndex(e => e.Crate.Id == "CMUFobWeaponryCrate");
            void Buy(EntityUid terminal, int cat, int entry)
            {
                var before = account.Balance;
                var orders = lift.Comp.Orders.Count;
                var cost = SEntMan.GetComponent<RequisitionsComputerComponent>(terminal).Categories[cat].Entries[entry].Cost;
                SEntMan.EventBus.RaiseLocalEvent(terminal, new RequisitionsBuyMsg(cat, entry) { Actor = user, UiKey = RequisitionsUIKey.Key });
                Assert.That(account.Balance, Is.EqualTo(before - (lift.Comp.Orders.Count - orders) * cost),
                    "Only accepted orders are charged, including after the allowance is exhausted.");
            }

            account.Balance = 0;
            Buy(first, category, weapon);
            Assert.That(lift.Comp.Orders, Is.Empty, "Rejected payment cannot consume an allowance.");
            account.Balance = initialBalance;
            Buy(first, category, weapon);
            Assert.That(lift.Comp.Orders.Count, Is.EqualTo(1), "The free starter shipment must not consume paid stock.");
            SEntMan.DeleteEntity(first);
            var second = SEntMan.SpawnAtPosition("CMASRSConsoleGovfor", coords);
            var replacement = SEntMan.GetComponent<RequisitionsComputerComponent>(second);
            var cat2 = replacement.Categories.FindIndex(c => c.Entries.Any(e => e.Crate.Id == "CMUFobWeaponryCrate"));
            var gun2 = replacement.Categories[cat2].Entries.FindIndex(e => e.Crate.Id == "CMUFobWeaponryCrate");
            var sup2 = replacement.Categories[cat2].Entries.FindIndex(e => e.Crate.Id == "CMUFobSupportCrate");
            Buy(second, cat2, gun2);
            Buy(second, cat2, gun2);
            for (var i = 0; i < 4; i++) Buy(second, cat2, sup2);
            Assert.That(lift.Comp.Orders.Count(e => e.Crate.Id == "CMUFobWeaponryCrate"), Is.EqualTo(2));
            Assert.That(lift.Comp.Orders.Count(e => e.Crate.Id == "CMUFobSupportCrate"), Is.EqualTo(3));
            var reqs = Server.System<RequisitionsSystem>();
            reqs.ReapplyPlatoonCatalogs();
            cat2 = replacement.Categories.FindIndex(c => c.Entries.Any(e => e.Crate.Id == "CMUFobWeaponryCrate"));
            gun2 = replacement.Categories[cat2].Entries.FindIndex(e => e.Crate.Id == "CMUFobWeaponryCrate");
            Buy(second, cat2, gun2);
            Assert.That(lift.Comp.Orders.Count, Is.EqualTo(5), "Catalog rebuilds cannot replenish a spent round allowance.");
        });
    }
}
