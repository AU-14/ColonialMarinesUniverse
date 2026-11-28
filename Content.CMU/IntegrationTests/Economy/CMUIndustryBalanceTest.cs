using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Requisitions;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.CCVar;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Forge;
using Content.Shared.CMU14.Grinding;
using Content.Shared.CMU14.Mining;
using Content.Shared.CMU14.Smelting;
using Content.Shared.CMU14.Steam;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Economy;

// the industry economy can't print money. prices: nothing sells for more than its inputs plus a fixed rate per
// second of work. buying: nothing from any catalog sells back for what it cost. flooding: the faction market caps
// what spam can earn, and never touches crate refunds or invoices
[TestFixture]
public sealed class CMUIndustryBalanceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    // a busy $1000 machine pays itself back in 30 minutes. hand work (forge, molds, anvil) earns half that.
    // every tier deeper in the chain pays 20% more per second, up to double, so end products are worth making
    private const double WorkPerSecond = 1000.0 / 1800.0;
    private const double HandWorkPerSecond = WorkPerSecond / 2;
    private const double TierBonus = 0.2;
    private const double TierCap = 2.0;

    // OreDrill: 1-10 units every 8 s on a deposit; oil pump: 10 crude a second
    private const double DrillUnitsPerSecond = 5.5 / 8.0;
    private const double PumpUnitsPerSecond = 10.0;

    private sealed record Edge(string Id, Dictionary<string, double> In, Dictionary<string, double> Out, double Seconds, bool Hand)
    {
        public double Work;
    }

    [Test]
    public async Task NothingSellsForMoreThanItsInputsAndWork()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var factory = Server.ResolveDependency<IComponentFactory>();
            var prices = RegistryPrices(protos);
            var edges = Edges(protos, factory);

            // things that can't be sold (slag, mercury, acids) are worth nothing if no recipe makes them, otherwise
            // their share of what a recipe has left once its sellable outputs are paid. that has one answer whatever
            // order the recipes come in, unlike splitting cost between outputs greedily
            var produced = edges.SelectMany(e => e.Out.Keys).ToHashSet();
            var unpriced = new Dictionary<string, double>();
            foreach (var key in edges.SelectMany(e => e.In.Keys.Concat(e.Out.Keys)))
            {
                if (!prices.ContainsKey(key))
                    unpriced.TryAdd(key, produced.Contains(key) ? double.PositiveInfinity : 0);
            }

            double Value(string key) => prices.TryGetValue(key, out var p) ? p : unpriced[key];

            for (var pass = 0; pass < 2000; pass++)
            {
                var changed = false;
                foreach (var edge in edges)
                {
                    var cost = edge.In.Sum(i => Value(i.Key) * i.Value);
                    if (double.IsInfinity(cost))
                        continue;

                    cost += edge.Work;
                    var byproducts = edge.Out.Keys.Where(k => !prices.ContainsKey(k)).ToList();
                    if (byproducts.Count == 0)
                        continue;

                    var left = Math.Max(0, cost - edge.Out.Where(o => prices.ContainsKey(o.Key)).Sum(o => prices[o.Key] * o.Value));
                    foreach (var key in byproducts)
                    {
                        var share = left / (byproducts.Count * edge.Out[key]);
                        if (share < unpriced[key] - 1e-9)
                        {
                            unpriced[key] = share;
                            changed = true;
                        }
                    }
                }

                if (!changed)
                    break;
            }

            var overpriced = new List<string>();
            foreach (var edge in edges)
            {
                var cost = edge.In.Sum(i => Value(i.Key) * i.Value);
                if (double.IsInfinity(cost))
                    continue;

                cost += edge.Work;
                var sold = edge.Out.Where(o => prices.ContainsKey(o.Key)).Sum(o => prices[o.Key] * o.Value);
                if (sold > cost + 0.011 + sold * 0.001)
                    overpriced.Add($"{edge.Id}: sells for {sold:0.###}, inputs and work cost {cost:0.###}");
            }

            Assert.That(overpriced, Is.Empty, "these recipes make more than their inputs and work are worth:\n" + string.Join("\n", overpriced));
        });
    }

    [Test]
    public async Task DeeperProductionPaysABetterRate()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var factory = Server.ResolveDependency<IComponentFactory>();
            var prices = RegistryPrices(protos);

            // best credits per machine-second actually earned at each tier, counting only fully priced recipes
            var best = new SortedDictionary<int, double>();
            foreach (var edge in Edges(protos, factory))
            {
                if (edge.In.ContainsKey("stack:RUMCRawRubber") && edge.Out.ContainsKey("stack:RUMCRubberSheet")
                    || edge.Id.StartsWith("RUMCMRAutoclaveGrow", StringComparison.Ordinal))
                {
                    var inputs = edge.In.Sum(x => prices.GetValueOrDefault(x.Key) * x.Value);
                    var finished = edge.Out.Sum(x => prices.GetValueOrDefault(x.Key) * x.Value);
                    Assert.That(finished, Is.GreaterThan(inputs), $"{edge.Id} should pay for finishing its feedstock");
                }

                if (edge.Hand || edge.In.Count == 0 || edge.Seconds <= 0
                    || !edge.In.Keys.Concat(edge.Out.Keys).All(prices.ContainsKey))
                    continue;

                var tier = (int) Math.Round((edge.Work / edge.Seconds / WorkPerSecond - 1) / TierBonus);
                var earned = (edge.Out.Sum(o => prices[o.Key] * o.Value) - edge.In.Sum(i => prices[i.Key] * i.Value)) / edge.Seconds;
                best[tier] = Math.Max(best.GetValueOrDefault(tier), earned);
            }

            var tiers = best.Where(b => b.Key is >= 1 and <= 5).ToList();
            Assert.That(tiers, Has.Count.EqualTo(5));
            for (var i = 1; i < tiers.Count; i++)
                Assert.That(tiers[i].Value, Is.GreaterThan(tiers[i - 1].Value), $"tier {tiers[i].Key} should pay better than tier {tiers[i - 1].Key}");
            Assert.That(tiers[^1].Value, Is.GreaterThanOrEqualTo(WorkPerSecond * 1.9), "end products should pay close to double raw work");
        });
    }

    [Test]
    public async Task DrillsAndPumpsEarnAboutOneMachineOfWork()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var prices = RegistryPrices(protos);
            foreach (var table in protos.EnumeratePrototypes<OreDepositTablePrototype>())
            {
                var weight = table.Ores.Values.Sum();
                var perUnit = table.Ores.Sum(o => o.Value * prices.GetValueOrDefault("stack:" + o.Key)) / weight;
                Assert.That(perUnit * DrillUnitsPerSecond, Is.LessThanOrEqualTo(WorkPerSecond * 1.05),
                    $"a drill on {table.ID} earns more than one machine of work");
            }

            Assert.That(prices["fluid:RUMCCrudeOil"] * PumpUnitsPerSecond, Is.LessThanOrEqualTo(WorkPerSecond * 1.05),
                "an oil pump earns more than one machine of work");
        });
    }

    [Test]
    public async Task NothingBoughtSellsBackForWhatItCost()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() => Server.ResolveDependency<IConfigurationManager>().SetCVar(AU14CCVars.SellCargoRewards, true));
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var factory = Server.ResolveDependency<IComponentFactory>();
            var requisitions = Server.System<RequisitionsSystem>();
            var seen = new HashSet<(string, int)>();
            var profitable = new List<string>();
            var checkedCount = 0;

            foreach (var catalog in protos.EnumeratePrototypes<EntityPrototype>())
            {
                if (catalog.Abstract || !catalog.TryGetComponent<RequisitionsComputerComponent>(out var computer, factory))
                    continue;

                foreach (var entry in computer.Categories.SelectMany(c => c.Entries))
                {
                    if (!seen.Add((entry.Crate.Id, entry.Cost)) || !protos.HasIndex<EntityPrototype>(entry.Crate))
                        continue;

                    var crate = SEntMan.SpawnEntity(entry.Crate, map.GridCoords);
                    foreach (var faction in new[] { "colony", "govfor" })
                    {
                        if (requisitions.TryAppraiseAsrsCargo(crate, out var value, faction) && value >= entry.Cost && entry.Cost > 0)
                            profitable.Add($"{catalog.ID}: {entry.Crate} costs {entry.Cost}, {faction} sells it back for {value:0.##}");
                    }

                    SEntMan.DeleteEntity(crate);
                    checkedCount++;
                }
            }

            Assert.That(checkedCount, Is.GreaterThan(100));
            Assert.That(profitable, Is.Empty, string.Join("\n", profitable));
        });
    }

    [Test]
    public async Task FloodingOneProductHitsACeiling()
    {
        await Server.WaitPost(() => Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUIndustryMarketEnabled, true));
        await Server.WaitAssertion(() =>
        {
            var market = Server.System<CMUIndustryMarketSystem>();
            var config = Server.ResolveDependency<IConfigurationManager>();

            // a little sells at full price
            var small = new List<IndustryExport> { new("stack:Test", 100) };
            Assert.That(market.Quote("colony", small), Is.EqualTo(100).Within(1e-6));

            // a mountain of one thing pays a fraction, and never more than free + slope stacked up over the curve
            var flood = new List<IndustryExport> { new("stack:Test", 1_000_000) };
            var colony = market.Quote("colony", flood);
            var military = market.Quote("govfor", flood);
            Assert.That(colony, Is.LessThan(30_000));
            Assert.That(military, Is.LessThan(colony), "military industry has to cap lower than the colony's");

            // the item curve alone caps a single product below the total
            var itemCap = config.GetCVar(CCVars.CMUIndustryMarketItemFree)
                          + config.GetCVar(CCVars.CMUIndustryMarketItemSlope) * Math.Log(1 + 1_000_000 / config.GetCVar(CCVars.CMUIndustryMarketItemSlope));
            Assert.That(colony, Is.LessThan(itemCap));

            // spreading over many products still runs into the faction total
            var spread = Enumerable.Range(0, 50).Select(i => new IndustryExport($"stack:Test{i}", 20_000)).ToList();
            var spreadPaid = market.Quote("colony", spread);
            Assert.That(spreadPaid, Is.LessThan(spread.Sum(e => e.Value) * 0.2));
        });
    }

    [Test]
    public async Task SplittingALoadDoesNotPayMore()
    {
        await Server.WaitPost(() => Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUIndustryMarketEnabled, true));
        await Server.WaitAssertion(() =>
        {
            var market = Server.System<CMUIndustryMarketSystem>();
            var whole = market.Sell("corporate", new List<IndustryExport> { new("stack:Split", 20_000), new("stack:Other", 5_000) });

            // same goods in 100 trips at the same moment, on a fresh faction
            var pieces = 0.0;
            for (var i = 0; i < 100; i++)
                pieces += market.Sell("splitter", new List<IndustryExport> { new("stack:Split", 200), new("stack:Other", 50) });

            // same goods, same moment: the same money however the load is cut or mixed
            Assert.That(pieces, Is.EqualTo(whole).Within(whole * 1e-6));

            // and selling them in the other order doesn't change it either
            var reversed = market.Sell("reverser", new List<IndustryExport> { new("stack:Other", 5_000), new("stack:Split", 20_000) });
            Assert.That(reversed, Is.EqualTo(whole).Within(whole * 1e-6));
        });
    }

    [Test]
    public async Task RefundsAndInvoicesIgnoreTheIndustryMarket()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var config = Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(AU14CCVars.SellCargoRewards, true);
            config.SetCVar(CCVars.CMUIndustryMarketEnabled, true);
        });

        await Server.WaitAssertion(() =>
        {
            var requisitions = Server.System<RequisitionsSystem>();
            var market = Server.System<CMUIndustryMarketSystem>();

            foreach (var faction in new[] { "govfor", "colony" })
            {
                // saturate the faction's industry market completely
                market.Sell(faction, new List<IndustryExport> { new("stack:RUMCRawRubber", 5_000_000) });
                Assert.That(market.CurrentMultiplier(faction, "stack:RUMCRawRubber"), Is.LessThan(0.01));

                var crate = SEntMan.SpawnEntity("RMCCrateConstruction", map.GridCoords);
                var reward = SComp<RequisitionsCrateComponent>(crate).Reward;
                Assert.That(requisitions.TryAppraiseAsrsCargo(crate, out var value, faction), Is.True);
                Assert.That(value, Is.EqualTo(reward), $"a plain crate return for {faction} must pay its full refund");
                SEntMan.DeleteEntity(crate);
            }
        });
    }

    [Test]
    public async Task OnlyListedLiquidsPay()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var exports = Server.System<CMUIndustryExportSystem>();
            var tank = SSpawnAtPosition("RUMCOilBarrel", map.GridCoords);
            var fluids = SComp<FluidTankComponent>(tank);
            SEntMan.EnsureComponent<ASRSSellableComponent>(tank);

            // food reagents are everywhere for free; poured into a tank they're worth nothing
            fluids.Contents["Nutriment"] = 100;
            var collected = new List<IndustryExport>();
            exports.CollectExports(tank, collected);
            Assert.That(collected, Is.Empty);

            fluids.Contents.Clear();
            fluids.Contents["RUMCDiesel"] = 100;
            collected.Clear();
            exports.CollectExports(tank, collected);
            var fuel = collected.Single(entry => entry.Key == "fluid:RUMCDiesel");
            Assert.That(fuel.Value, Is.EqualTo(100 * exports.GetFluidUnitPrice("RUMCDiesel")).Within(1e-4));

            fluids.Contents["RUMCEthylene"] = 1;
            collected.Clear();
            exports.CollectExports(tank, collected);
            Assert.That(collected, Is.Empty, "A mixed tank must not export even its allowed liquid.");
        });
    }

    [Test]
    public async Task SellableFluidPricesMatchTheCanonicalRegistry()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var exports = Server.System<CMUIndustryExportSystem>();
            foreach (var price in protos.EnumeratePrototypes<CMUIndustryPricePrototype>())
            {
                if (price.Reagent is not { } reagent)
                    continue;

                Assert.That(exports.GetFluidUnitPrice(reagent.Id),
                    Is.EqualTo(price.Sellable ? price.Price : 0).Within(0.0001), price.Product);
                if (price.Sellable)
                    Assert.That(protos.Index(reagent).PricePerUnit, Is.EqualTo(price.Price).Within(0.0001), price.Product);
            }
        });
    }

    [Test]
    public async Task BoughtStacksNeverExport()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var exports = Server.System<CMUIndustryExportSystem>();
            // a kit that wasn't made by a machine used to sell at its world-market price through the ASRS
            var kit = SSpawnAtPosition("RUMCKitDriveItem", map.GridCoords);
            Assert.That(exports.CanExport(kit), Is.False);
            var collected = new List<IndustryExport>();
            exports.CollectExports(kit, collected);
            Assert.That(collected, Is.Empty);

            SEntMan.EnsureComponent<ASRSSellableComponent>(kit);
            exports.CollectExports(kit, collected);
            Assert.That(collected, Has.Count.EqualTo(1));

            var machine = SSpawnAtPosition("RUMCMachineSifter", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(machine);
            Assert.That(exports.CanExport(machine), Is.False, "Capital machinery is not an export product.");

            var casing = SSpawnAtPosition("RUMCMachineHullItem", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(casing);
            Assert.That(exports.CanExport(casing), Is.True, "A priced machine component is not capital machinery.");
            collected.Clear();
            exports.CollectExports(casing, collected);
            Assert.That(collected, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task FreeIndustrySuppliesCannotBeSoldUnprocessed()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var exports = Server.System<CMUIndustryExportSystem>();
            var requisitions = Server.System<RequisitionsSystem>();
            var ore = SSpawnAtPosition("RUMCAluminumOre1", map.GridCoords);
            var oil = SSpawnAtPosition("CMUASRSCrudeOilBarrel", map.GridCoords);
            Assert.That(exports.CanExport(ore), Is.False);
            Assert.That(exports.CanExport(oil), Is.False);
            Assert.That(requisitions.TryAppraiseAsrsCargo(ore, out _), Is.False);
            Assert.That(requisitions.TryAppraiseAsrsCargo(oil, out _), Is.False);
            var crate = SSpawnAtPosition("CMUASRSRandomOreShipment", map.GridCoords);
            Assert.That(SComp<RequisitionsCrateComponent>(crate).Reward, Is.Zero);
        });
    }

    private static Dictionary<string, double> RegistryPrices(IPrototypeManager protos)
    {
        var prices = new Dictionary<string, double>();
        foreach (var price in protos.EnumeratePrototypes<CMUIndustryPricePrototype>())
        {
            if (price.Stack is { } stack)
                prices["stack:" + stack] = price.Price;
            if (price.Entity is { } entity)
                prices["entity:" + entity] = price.Price;
            if (price.Reagent is { } reagent)
                prices["fluid:" + reagent] = price.Price;
        }

        return prices;
    }

    // every way the industry turns things into sellable things, with what each second of it is worth
    private static List<Edge> Edges(IPrototypeManager protos, IComponentFactory factory)
    {
        var edges = new List<Edge>();
        string Ent(EntProtoId id)
        {
            return protos.TryIndex(id, out var proto) && proto.TryGetComponent<StackComponent>(out var stack, factory)
                ? "stack:" + stack.StackTypeId
                : "entity:" + id;
        }

        static void Add(Dictionary<string, double> into, string key, double amount)
            => into[key] = into.GetValueOrDefault(key) + amount;

        foreach (var r in protos.EnumeratePrototypes<MachineRecipePrototype>())
        {
            var ins = new Dictionary<string, double>();
            var outs = new Dictionary<string, double>();
            foreach (var (k, v) in r.Inputs) Add(ins, "stack:" + k, v);
            foreach (var (k, v) in r.ItemInputs) Add(ins, Ent(k), v);
            foreach (var (k, v) in r.FluidInputs) Add(ins, "fluid:" + k, v);
            foreach (var (k, v) in r.Outputs) Add(outs, "stack:" + k, v);
            foreach (var (id, count) in r.ItemOutputs)
            {
                var units = protos.Index(id).TryGetComponent<StackComponent>(out var stack, factory) ? stack.Count : 1;
                Add(outs, Ent(id), count * units);
            }
            foreach (var (k, v) in r.FluidOutputs) Add(outs, "fluid:" + k, v);
            edges.Add(new Edge(r.ID, ins, outs, r.Duration, false));
        }

        foreach (var r in protos.EnumeratePrototypes<GrinderRecipePrototype>())
        {
            var outs = new Dictionary<string, double>();
            Add(outs, "stack:" + r.Result, r.ResultAmount);
            foreach (var extra in r.Extras)
                Add(outs, "stack:" + extra.Result, extra.Amount);
            edges.Add(new Edge(r.ID, new() { ["stack:" + r.Input] = r.InputAmount }, outs, 3, false));
        }

        foreach (var r in protos.EnumeratePrototypes<SteamSmeltRecipePrototype>())
        {
            var outs = new Dictionary<string, double> { ["stack:" + r.Result] = r.ResultAmount };
            if (r.Slag > 0)
                Add(outs, "fluid:RUMCSlag", r.Slag);
            edges.Add(new Edge(r.ID, r.Inputs.ToDictionary(i => "stack:" + i.Key, i => (double) i.Value), outs, r.Duration, false));
        }

        foreach (var r in protos.EnumeratePrototypes<PressRecipePrototype>())
            edges.Add(new Edge(r.ID, r.Inputs.ToDictionary(i => "stack:" + i.Key, i => (double) i.Value),
                new() { ["stack:" + r.Result] = r.ResultAmount }, r.Duration, false));

        foreach (var r in protos.EnumeratePrototypes<AlloyRecipePrototype>())
            edges.Add(new Edge(r.ID, r.Materials.ToDictionary(i => "stack:" + i.Key, i => (double) i.Value),
                new() { ["stack:" + r.Result] = r.ResultAmount }, r.Duration.TotalSeconds, false));

        foreach (var r in protos.EnumeratePrototypes<CondenserRecipePrototype>())
        {
            var ins = new Dictionary<string, double> { ["fluid:" + r.Liquid] = r.LiquidAmount };
            Add(ins, "fluid:" + r.Coolant, r.CoolantAmount);
            if (r.Material is { } material)
                Add(ins, "stack:" + material, r.MaterialAmount);
            edges.Add(new Edge(r.ID, ins, new() { ["stack:" + r.Result] = r.ResultAmount }, r.Duration, false));
        }

        foreach (var r in protos.EnumeratePrototypes<AnvilRecipePrototype>())
        {
            var ins = new Dictionary<string, double>();
            if (r.Ingot is { } ingot)
                ins["stack:" + ingot] = 1;
            edges.Add(new Edge(r.ID, ins, new() { [Ent(r.Output)] = r.OutputCount }, 2 * r.Steps.Count, true));
        }

        foreach (var proto in protos.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (proto.TryGetComponent<ForgeComponent>(out var forge, factory))
            {
                foreach (var ing in forge.Ingredients)
                    edges.Add(new Edge($"{proto.ID}:{ing.Input}", new() { ["stack:" + ing.Input] = 1 },
                        new() { ["fluid:" + ing.Reagent] = ing.ReagentPerUnit.Double() }, 3, true));
            }

            if (proto.TryGetComponent<MoldComponent>(out var mold, factory))
            {
                foreach (var recipe in mold.Recipes)
                    edges.Add(new Edge($"{proto.ID}:{recipe.Output}", recipe.Ingredients.ToDictionary(i => "fluid:" + i.Key, i => i.Value.Double()),
                        new() { [Ent(recipe.Output)] = recipe.OutputCount }, recipe.CoolTime.TotalSeconds, true));
            }
        }

        // tier of a thing: 0 if no recipe makes it, else the lowest tier a recipe makes it at.
        // tier of a recipe: one past its deepest input
        var made = edges.SelectMany(e => e.Out.Keys).ToHashSet();
        var tier = new Dictionary<string, double>();
        foreach (var key in edges.SelectMany(e => e.In.Keys.Concat(e.Out.Keys)))
            tier.TryAdd(key, made.Contains(key) ? double.PositiveInfinity : 0);

        double EdgeTier(Edge e) => 1 + (e.In.Count == 0 ? 0 : e.In.Keys.Max(k => tier[k]));
        for (var pass = 0; pass < 100; pass++)
        {
            var changed = false;
            foreach (var edge in edges)
            {
                var t = EdgeTier(edge);
                foreach (var key in edge.Out.Keys)
                {
                    if (t < tier[key])
                    {
                        tier[key] = t;
                        changed = true;
                    }
                }
            }

            if (!changed)
                break;
        }

        foreach (var edge in edges)
        {
            var rate = (edge.Hand ? HandWorkPerSecond : WorkPerSecond) * Math.Min(TierCap, 1 + TierBonus * EdgeTier(edge));
            edge.Work = edge.Seconds * rate;
        }

        return edges;
    }
}
