using Content.IntegrationTests.Fixtures;
using Content.Server.Cargo.Systems;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Requisitions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Requisitions;

[TestFixture]
public sealed class ASRSCanisterResaleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    // The no-duplication invariant: anything the ASRS sells as a canister must price
    // back strictly below its catalog cost when lowered on the elevator, and anything
    // the ASRS sells as a gas miner may only mine waste-tier gas. New catalog entries
    // fail here until their pricing is considered.
    [Test]
    public async Task CatalogCanistersSellBelowCostAndPurchasableMinersOnlyMineWasteGas()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var protoMan = Server.ResolveDependency<IPrototypeManager>();
            var compFactory = Server.ResolveDependency<IComponentFactory>();
            var computerName = compFactory.GetComponentName(typeof(RequisitionsComputerComponent));
            var canisterName = compFactory.GetComponentName(typeof(GasCanisterComponent));
            var minerName = compFactory.GetComponentName(typeof(GasMinerComponent));
            var pricing = Server.System<PricingSystem>();

            var computers = new List<EntityUid>();
            var seenCanisters = new HashSet<string>();
            var seenMiners = new HashSet<string>();
            var canistersChecked = 0;

            foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.Components.ContainsKey(computerName))
                    continue;

                var computer = SEntMan.SpawnEntity(proto.ID, map.GridCoords);
                computers.Add(computer);
                var catalog = SEntMan.GetComponent<RequisitionsComputerComponent>(computer);

                foreach (var item in catalog.ItemCatalog)
                {
                    if (!protoMan.TryIndex(item.Prototype, out EntityPrototype? entryProto))
                        continue;

                    if (entryProto.Components.ContainsKey(canisterName) && seenCanisters.Add(item.Prototype))
                    {
                        var canister = SEntMan.SpawnEntity(item.Prototype, map.GridCoords);
                        var price = pricing.GetPrice(canister);
                        SEntMan.DeleteEntity(canister);
                        canistersChecked++;
                        Assert.That(price, Is.LessThan(item.Cost),
                            $"{item.Prototype} sells on the ASRS for {price:0} but costs {item.Cost}");
                    }

                    if (entryProto.Components.ContainsKey(minerName) && seenMiners.Add(item.Prototype))
                    {
                        var miner = SEntMan.SpawnEntity(item.Prototype, map.GridCoords);
                        var gas = SEntMan.GetComponent<GasMinerComponent>(miner).SpawnGas;
                        SEntMan.DeleteEntity(miner);
                        Assert.That(gas == Gas.Oxygen || gas == Gas.Nitrogen, Is.True,
                            $"{item.Prototype} mines {gas}, which must be priced as waste before it is sold");
                    }
                }
            }

            foreach (var computer in computers)
                SEntMan.DeleteEntity(computer);

            Assert.That(canistersChecked, Is.GreaterThanOrEqualTo(10),
                "expected the ASRS catalogs to sell gas canisters");
        });
    }

    [Test]
    public async Task LoweringTheElevatorPaysTheColonyAccountLessThanCatalogCost()
    {
        var map = await Pair.CreateTestMap();
        EntityUid canister = default;
        await Server.WaitPost(() =>
        {
            var timing = Server.ResolveDependency<IGameTiming>();
            var elevator = SEntMan.SpawnEntity("CMCargoElevator", map.GridCoords);
            var comp = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
            comp.Faction = "colony";
            comp.RoundStartFreeCrateGiven = true;
            canister = SEntMan.SpawnEntity("CMUCanisterOxygen", map.GridCoords);
            comp.Mode = RequisitionsElevatorMode.Lowering;
            comp.NextMode = null;
            comp.Busy = true;
            comp.RaiseDelay = TimeSpan.Zero;
            comp.LowerDelay = TimeSpan.Zero;
            comp.ToggledAt = timing.CurTime - TimeSpan.FromSeconds(1);
        });

        // Run the sale and its queued deletion in the normal game loop. Calling Update manually
        // would let the next tick sell the canister again before the deletion queue is processed.
        await Server.WaitRunTicks(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(canister), Is.False,
                "the canister should have been sold with the lowered elevator");

            // A fresh colony account starts at 450. The oxygen canister must pay
            // something, but strictly less than its 200 catalog price.
            var balance = 0;
            var accounts = SEntMan.EntityQueryEnumerator<RequisitionsAccountComponent>();
            while (accounts.MoveNext(out var account))
            {
                if (account.Faction == "colony")
                    balance += account.Balance;
            }

            Assert.That(balance, Is.GreaterThan(450), "selling a fresh oxygen canister must pay the faction account");
            Assert.That(balance, Is.LessThan(650), "selling a fresh oxygen canister must not return its 200 catalog price");
        });
    }
}
