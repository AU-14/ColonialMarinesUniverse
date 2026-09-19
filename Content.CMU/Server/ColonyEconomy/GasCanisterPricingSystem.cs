using Content.Shared._RMC14.Requisitions.Components;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Cargo;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ColonyEconomy;

// Prices canister contents by mole count so gas canisters sold on the ASRS pay by fill.
// Three price sources, checked in order: waste, valuable, then derived from the catalog.
//
// Waste: gases with a free source (station atmos loops, fire byproducts, the portable
// miners) must stay near-zero per mol. Any real value here turns the free source into
// a money printer.
//
// Valuable: only obtainable through atmos work. These pay a wage per cycle, and the
// hard rule is no passive farms: every mol must trace back to a paid canister.
// Tritium 0.4 over its ~0.28 plasma-fire input (1 plasma + ~1 O2 per mol at full
// supersaturation, plasma derived ~0.27). Phoron 0.8 refines a 1600 plasma canister
// into ~2215 of gas (~600 wage for holding the 773-1400 K window, N2 is waste).
// Frezon cannot use input-cost parity: FrezonProduction turns 1 tritium + ~50 waste-
// priced O2 into ~51 frezon, so the per-mol price is diluted 51x and any price above
// ~0.02 beats the chain's catalog cost. 0.1 is sized per run: a full 2769-mol tritium
// canister yields ~141k mol frezon (~14k) and demands ~50 canisters of O2 plus the
// cryo chain, the colony's industrial jackpot. The 51x mole gain is the number to
// respect when retuning.
//
// Derived: everything else a canister can hold is priced from the live ASRS catalog as
// ResaleMargin x (cost - shell) / fresh moles, cheapest source winning per gas. Catalog
// balance tweaks move the resale price automatically; resale can never beat purchase.
// Nitrous oxide has no production reaction and rides this rate (~0.067 from its
// 500-cr canister). A catalog canister holding a valuable gas must cost more than
// valuable-rate x fresh moles: at 0.4 a tritium canister resells for ~1108, so the
// commented-out 800-cr TritiumCanister entry would print if re-enabled.
public sealed class GasCanisterPricingSystem : EntitySystem
{
    private const float ResaleMargin = 0.5f;

    private static readonly Dictionary<Gas, float> WasteGasPrices = new()
    {
        [Gas.Oxygen] = 0.01f,
        [Gas.Nitrogen] = 0.01f,
        [Gas.CarbonDioxide] = 0.01f,
        [Gas.WaterVapor] = 0.01f,
        [Gas.Ammonia] = 0.01f,
    };

    private static readonly Dictionary<Gas, float> ValuableGasPrices = new()
    {
        [Gas.Tritium] = 0.4f,
        [Gas.Phoron] = 0.8f,
        [Gas.Frezon] = 0.1f,
    };

    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GasCanisterComponent, PriceCalculationEvent>(OnCanisterPrice);
    }

    private void OnCanisterPrice(Entity<GasCanisterComponent> canister, ref PriceCalculationEvent args)
    {
        var derived = GetDerivedRates();
        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var gas = (Gas) i;
            var moles = canister.Comp.Air.GetMoles(gas);
            if (moles <= 0)
                continue;

            if (!WasteGasPrices.TryGetValue(gas, out var rate)
                && !ValuableGasPrices.TryGetValue(gas, out rate)
                && !derived.TryGetValue(gas, out rate))
                continue;

            args.Price += moles * rate;
        }
    }

    // Rebuilt per pricing call: canister pricing is rare (elevator sells, appraisals)
    // and this skips all cache invalidation against catalog rebuilds.
    private Dictionary<Gas, float> GetDerivedRates()
    {
        var rates = new Dictionary<Gas, float>();
        foreach (var computer in EntityQuery<RequisitionsComputerComponent>())
        {
            foreach (var item in computer.ItemCatalog)
            {
                if (!_prototypes.TryIndex(item.Prototype, out var prototype)
                    || !prototype.TryComp<GasCanisterComponent>(CompName.Get<GasCanisterComponent>(_factory), out var canister))
                    continue;

                prototype.TryComp<StaticPriceComponent>(
                    CompName.Get<StaticPriceComponent>(_factory), out var shell);
                var shellPrice = shell?.Price ?? 0f;
                if (item.Cost <= shellPrice)
                    continue;

                for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
                {
                    var gas = (Gas) i;
                    var moles = canister.Air.GetMoles(gas);
                    if (moles <= 0 || WasteGasPrices.ContainsKey(gas) || ValuableGasPrices.ContainsKey(gas))
                        continue;

                    var rate = (float) ((item.Cost - shellPrice) * ResaleMargin / moles);
                    if (!rates.TryGetValue(gas, out var current) || rate < current)
                        rates[gas] = rate;
                }
            }
        }

        return rates;
    }
}
