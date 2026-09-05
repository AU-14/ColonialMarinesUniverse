using System.Linq;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.Crate;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Containers;
using Content.Shared.Database;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.EntityTable.ValueSelector;
using Content.Shared.Item;
using Content.Shared.Storage.Components;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    [Dependency] private UserInterfaceSystem _itemizedUi = default!;

    private readonly Dictionary<EntityUid, ItemizedCatalogRuntime> _itemizedCatalogs = new();

    private void OnComputerShutdown(Entity<RequisitionsComputerComponent> computer, ref ComponentShutdown args)
    {
        _itemizedCatalogs.Remove(computer.Owner);
    }

    private void RebuildItemizedCatalog(Entity<RequisitionsComputerComponent> computer)
    {
        var runtime = new ItemizedCatalogRuntime();
        var priceSources = new List<RequisitionsPriceSource>();
        var categories = new Dictionary<EntProtoId, HashSet<string>>();

        for (var categoryIndex = 0; categoryIndex < computer.Comp.Categories.Count; categoryIndex++)
        {
            var category = computer.Comp.Categories[categoryIndex];
            for (var orderIndex = 0; orderIndex < category.Entries.Count; orderIndex++)
            {
                var entry = category.Entries[orderIndex];
                if (!TryGetDeterministicManifest(entry, out var manifest) || manifest.Count == 0)
                    continue;

                var key = (categoryIndex, orderIndex);
                var source = new ItemizedSource(entry, manifest, _timing.CurTime);
                runtime.Sources[key] = source;
                priceSources.Add(new RequisitionsPriceSource(entry.Cost, manifest));

                foreach (var prototype in manifest.Keys)
                {
                    if (!runtime.SourcesByItem.TryGetValue(prototype, out var sources))
                    {
                        sources = new List<(int Category, int Order)>();
                        runtime.SourcesByItem[prototype] = sources;
                    }

                    sources.Add(key);

                    if (!categories.TryGetValue(prototype, out var itemCategories))
                    {
                        itemCategories = new HashSet<string>();
                        categories[prototype] = itemCategories;
                    }

                    itemCategories.Add(category.Name);
                }
            }
        }

        var prices = RequisitionsPriceCalculator.Calculate(priceSources);

        var overrides = new Dictionary<EntProtoId, RequisitionsItemOverride>();
        foreach (var itemOverride in computer.Comp.ItemOverrides)
        {
            overrides[itemOverride.Prototype] = itemOverride;
        }

        computer.Comp.ItemCatalog.Clear();
        foreach (var prototypeId in prices.Keys.OrderBy(id => id.Id))
        {
            if (!_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var prototype))
                continue;

            var price = prices[prototypeId];
            var weight = GetItemWeight(prototypeId, out var packable);
            if (overrides.TryGetValue(prototypeId, out var itemOverride))
            {
                price = itemOverride.Cost ?? price;
                weight = itemOverride.Weight ?? weight;
                packable |= itemOverride.Weight != null;
            }

            packable &= weight <= computer.Comp.ItemShipmentWeightLimit;

            computer.Comp.ItemCatalog.Add(new RequisitionsItemEntry
            {
                Prototype = prototypeId,
                Name = prototype.Name,
                Description = prototype.Description,
                Categories = categories[prototypeId].Order().ToList(),
                Cost = Math.Max(1, price),
                Weight = Math.Max(1, weight),
                Units = GetItemUnits(prototype),
                Packable = packable,
            });
        }

        _itemizedCatalogs[computer.Owner] = runtime;
        Dirty(computer);
    }

    private bool TryGetDeterministicManifest(RequisitionsEntry entry, out Dictionary<EntProtoId, int> manifest)
    {
        manifest = new Dictionary<EntProtoId, int>();
        if (!_prototypeManager.TryIndex<EntityPrototype>(entry.Crate, out var cratePrototype))
            return false;

        var componentFactory = EntityManager.ComponentFactory;
        // Keep deployment crates intact when their contents cannot be carried as items.
        // Spawning the machine directly can leave it anchored on the delivery pad.
        if (cratePrototype.TryComp<SpawnOnTerminateComponent>(
                CompName.Get<SpawnOnTerminateComponent>(componentFactory), out var deployment) &&
            _prototypeManager.TryIndex<EntityPrototype>(deployment.Spawn, out var deployedPrototype) &&
            !deployedPrototype.TryComp<ItemComponent>(CompName.Get<ItemComponent>(componentFactory), out _))
        {
            AddManifestItem(manifest, entry.Crate, 1);
            foreach (var prototype in entry.Entities)
                AddManifestItem(manifest, prototype, 1);

            return true;
        }

        var hasFill = false;
        if (cratePrototype.TryComp<StorageFillComponent>(CompName.Get<StorageFillComponent>(componentFactory), out var storageFill))
        {
            hasFill = true;
            foreach (var spawn in storageFill.Contents)
            {
                if (spawn.PrototypeId == null ||
                    spawn.SpawnProbability != 1f ||
                    !string.IsNullOrEmpty(spawn.GroupId) ||
                    spawn.MaxAmount > spawn.Amount)
                {
                    return false;
                }

                AddManifestItem(manifest, spawn.PrototypeId.Value, Math.Max(0, spawn.Amount));
            }
        }

        if (cratePrototype.TryComp<CrateOpenableComponent>(CompName.Get<CrateOpenableComponent>(componentFactory), out var crateOpenable))
        {
            hasFill = true;
            foreach (var spawn in crateOpenable.Spawn)
            {
                if (spawn.PrototypeId == null ||
                    spawn.SpawnProbability != 1f ||
                    !string.IsNullOrEmpty(spawn.GroupId) ||
                    spawn.MaxAmount > spawn.Amount)
                {
                    return false;
                }

                AddManifestItem(manifest, spawn.PrototypeId.Value, Math.Max(0, spawn.Amount));
            }
        }

        if (cratePrototype.TryComp<ContainerFillComponent>(CompName.Get<ContainerFillComponent>(componentFactory), out var containerFill))
        {
            hasFill = true;
            foreach (var contents in containerFill.Containers.Values)
            {
                foreach (var prototype in contents)
                    AddManifestItem(manifest, prototype, 1);
            }
        }

        if (cratePrototype.TryComp<EntityTableContainerFillComponent>(
                CompName.Get<EntityTableContainerFillComponent>(componentFactory), out var tableFill))
        {
            hasFill = true;
            foreach (var selector in tableFill.Containers.Values)
            {
                if (!TryAddDeterministicSelector(selector, manifest, new HashSet<string>(), 0))
                    return false;
            }
        }

        if (cratePrototype.TryComp<SpawnOnTerminateComponent>(
                CompName.Get<SpawnOnTerminateComponent>(componentFactory), out var spawnOnTerminate))
        {
            hasFill = true;
            AddManifestItem(manifest, spawnOnTerminate.Spawn, 1);
        }

        foreach (var prototype in entry.Entities)
            AddManifestItem(manifest, prototype, 1);

        if (!hasFill && entry.Entities.Count == 0)
            AddManifestItem(manifest, entry.Crate, 1);

        return true;
    }

    private bool TryAddDeterministicSelector(
        EntityTableSelector selector,
        Dictionary<EntProtoId, int> manifest,
        HashSet<string> visitedTables,
        int depth)
    {
        if (depth > 16 ||
            selector.Prob != 1 ||
            selector.Conditions.Count != 0 ||
            selector.Rolls is not ConstantNumberSelector rolls ||
            rolls.Value < 0)
        {
            return false;
        }

        switch (selector)
        {
            case EntSelector entity when entity.Amount is ConstantNumberSelector amount && amount.Value >= 0:
                AddManifestItem(manifest, entity.Id, rolls.Value * amount.Value);
                return true;
            case AllSelector all:
                for (var i = 0; i < rolls.Value; i++)
                {
                    foreach (var child in all.Children)
                    {
                        if (!TryAddDeterministicSelector(child, manifest, visitedTables, depth + 1))
                            return false;
                    }
                }
                return true;
            case NestedSelector nested:
                if (!visitedTables.Add(nested.TableId.Id) ||
                    !_prototypeManager.TryIndex(nested.TableId, out EntityTablePrototype? table))
                {
                    return false;
                }

                for (var i = 0; i < rolls.Value; i++)
                {
                    if (!TryAddDeterministicSelector(table.Table, manifest, visitedTables, depth + 1))
                        return false;
                }

                visitedTables.Remove(nested.TableId.Id);
                return true;
            default:
                return false;
        }
    }

    private static void AddManifestItem(Dictionary<EntProtoId, int> manifest, EntProtoId prototype, int amount)
    {
        if (amount <= 0)
            return;

        manifest.TryGetValue(prototype, out var current);
        manifest[prototype] = current + amount;
    }

    private int GetItemWeight(EntProtoId prototypeId, out bool packable)
    {
        packable = false;
        if (!_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var prototype) ||
            !prototype.TryComp<ItemComponent>(CompName.Get<ItemComponent>(EntityManager.ComponentFactory), out var item) ||
            !_prototypeManager.TryIndex(item.Size, out ItemSizePrototype? size))
        {
            return 64;
        }

        packable = true;
        return Math.Max(1, size.Weight);
    }

    private int GetItemUnits(EntityPrototype prototype)
    {
        return prototype.TryComp<StackComponent>(
            CompName.Get<StackComponent>(EntityManager.ComponentFactory), out var stack)
            ? Math.Max(1, stack.Count)
            : 1;
    }

    private bool HasItemizedSource(EntityUid computer, (int Category, int Order) key)
    {
        return _itemizedCatalogs.TryGetValue(computer, out var runtime) && runtime.Sources.ContainsKey(key);
    }

    private bool TryTakeItemizedBundle(Entity<RequisitionsComputerComponent> computer, (int Category, int Order) key)
    {
        if (!_itemizedCatalogs.TryGetValue(computer.Owner, out var runtime) ||
            !runtime.Sources.TryGetValue(key, out var source))
        {
            return false;
        }

        if (source.Unlimited)
            return true;

        foreach (var (prototype, amount) in source.Manifest)
        {
            if (source.Current[prototype] < amount)
                return false;
        }

        foreach (var (prototype, amount) in source.Manifest)
            source.Current[prototype] -= amount;

        StartSourceReplenish(source, _timing.CurTime);
        return true;
    }

    private bool TryGetItemizedBundleStock(
        EntityUid computer,
        (int Category, int Order) key,
        TimeSpan time,
        out RequisitionsStockInfo stock)
    {
        stock = default!;
        if (!_itemizedCatalogs.TryGetValue(computer, out var runtime) ||
            !runtime.Sources.TryGetValue(key, out var source) ||
            source.Unlimited)
        {
            return false;
        }

        var current = source.Manifest.Min(pair => source.Current[pair.Key] / pair.Value);
        stock = new RequisitionsStockInfo(key.Category, key.Order, current, source.MaxBundles,
            SecondsUntilReplenish(source, time));
        return true;
    }

    private bool ProcessItemizedStock(Entity<RequisitionsComputerComponent> computer, TimeSpan time)
    {
        if (!_itemizedCatalogs.TryGetValue(computer.Owner, out var runtime))
            return false;

        var changed = false;
        var waitingForStock = false;
        foreach (var source in runtime.Sources.Values)
        {
            if (source.Unlimited || source.IsFull)
            {
                source.NextReplenish = TimeSpan.Zero;
                continue;
            }

            waitingForStock = true;
            StartSourceReplenish(source, time);
            if (source.Entry.StockReplenishDelay <= TimeSpan.Zero)
            {
                source.RefillToMaximum();
                changed = true;
                continue;
            }

            while (!source.IsFull && time >= source.NextReplenish)
            {
                source.AddBundles(Math.Max(1, source.Entry.StockReplenishAmount));
                changed = true;
                if (!source.IsFull)
                    source.NextReplenish += source.Entry.StockReplenishDelay;
            }
        }

        if (waitingForStock && time >= computer.Comp.NextStockUiUpdate)
        {
            computer.Comp.NextStockUiUpdate = time + TimeSpan.FromSeconds(1);
            changed = true;
        }

        return changed;
    }

    protected override List<RequisitionsItemStockInfo> GetItemStockInfo(Entity<RequisitionsComputerComponent> computer)
    {
        var result = new List<RequisitionsItemStockInfo>();
        if (!_itemizedCatalogs.TryGetValue(computer.Owner, out var runtime))
            return result;

        var time = _timing.CurTime;
        foreach (var item in computer.Comp.ItemCatalog)
        {
            if (!runtime.SourcesByItem.TryGetValue(item.Prototype, out var sourceKeys))
                continue;

            var sources = sourceKeys.Select(key => runtime.Sources[key]).ToList();
            if (sources.Any(source => source.Unlimited))
                continue;

            var current = sources.Sum(source => source.Current[item.Prototype]);
            var max = sources.Sum(source => source.Maximum[item.Prototype]);
            var seconds = sources.Where(source => !source.IsFull)
                .Select(source => SecondsUntilReplenish(source, time))
                .DefaultIfEmpty(0)
                .Min();
            result.Add(new RequisitionsItemStockInfo(item.Prototype, current, max, seconds));
        }

        return result;
    }

    private void OnItemizedCheckout(Entity<RequisitionsComputerComponent> computer, ref RequisitionsCheckoutMsg args)
    {
        var actor = args.Actor;
        if (!_itemizedCatalogs.TryGetValue(computer.Owner, out var runtime) ||
            args.Lines.Count == 0 ||
            args.Lines.Count > computer.Comp.ItemCatalog.Count)
        {
            SendCheckoutResult(computer, actor, args.RequestId, RequisitionsCheckoutResult.InvalidOrder);
            return;
        }

        var requested = new Dictionary<EntProtoId, (RequisitionsItemEntry Item, int Amount)>();
        long totalCost = 0;
        foreach (var line in args.Lines)
        {
            var item = computer.Comp.ItemCatalog.FirstOrDefault(item => item.Prototype == line.Prototype);
            if (item == null ||
                line.Amount <= 0 ||
                line.Amount > 99)
            {
                SendCheckoutResult(computer, actor, args.RequestId, RequisitionsCheckoutResult.InvalidOrder);
                return;
            }

            if (requested.ContainsKey(item.Prototype))
            {
                SendCheckoutResult(computer, actor, args.RequestId, RequisitionsCheckoutResult.InvalidOrder);
                return;
            }

            requested[item.Prototype] = (item, line.Amount);
            totalCost += (long) item.Cost * line.Amount;
            if (totalCost > int.MaxValue)
            {
                SendCheckoutResult(computer, actor, args.RequestId, RequisitionsCheckoutResult.InvalidOrder);
                return;
            }
        }

        computer.Comp.Account ??= GetAccount(computer.Comp.Faction);
        if (!TryComp(computer.Comp.Account, out RequisitionsAccountComponent? account) || account.Balance < totalCost)
        {
            SendCheckoutResult(computer, actor, args.RequestId, RequisitionsCheckoutResult.InsufficientFunds);
            return;
        }

        if (GetElevator(computer) is not { } elevator)
        {
            SendCheckoutResult(computer, actor, args.RequestId, RequisitionsCheckoutResult.NoPlatform);
            return;
        }

        var shipments = PackItemizedOrder(computer.Comp, requested.Values);
        if (shipments.Count > GetElevatorCapacity(elevator) - elevator.Comp.Orders.Count)
        {
            SendCheckoutResult(computer, actor, args.RequestId, RequisitionsCheckoutResult.PlatformFull);
            return;
        }

        var simulated = runtime.Sources.Values
            .Where(source => !source.Unlimited)
            .ToDictionary(source => source, source => new Dictionary<EntProtoId, int>(source.Current));
        foreach (var (prototype, request) in requested)
        {
            if (!TryConsumeItem(runtime, prototype, request.Amount, simulated))
            {
                SendCheckoutResult(computer, actor, args.RequestId, RequisitionsCheckoutResult.InsufficientStock);
                return;
            }
        }

        foreach (var (source, current) in simulated)
        {
            source.Current = current;
            if (!source.IsFull)
                StartSourceReplenish(source, _timing.CurTime);
        }

        account.Balance -= (int) totalCost;
        Dirty(computer.Comp.Account.Value, account);
        elevator.Comp.Orders.AddRange(shipments);
        Dirty(elevator);
        SendCheckoutResult(computer, actor, args.RequestId, RequisitionsCheckoutResult.Success);
        SendUIStateAll();

        var itemCount = requested.Values.Sum(request => request.Amount);
        _adminLogs.Add(LogType.RMCRequisitionsBuy,
            $"{ToPrettyString(actor):actor} bought {itemCount} individual requisitions items in {shipments.Count} shipments for {totalCost}");
        _core.CreateARESLog(computer.Owner, LogCat,
            (string) $"{Name(actor)} bought {itemCount} individual ASRS items for {totalCost}$");
    }

    private void SendCheckoutResult(
        Entity<RequisitionsComputerComponent> computer,
        EntityUid actor,
        int requestId,
        RequisitionsCheckoutResult result)
    {
        _itemizedUi.ServerSendUiMessage(computer.Owner, RequisitionsUIKey.Key,
            new RequisitionsCheckoutResultMsg(requestId, result), actor);
    }

    private List<RequisitionsEntry> PackItemizedOrder(
        RequisitionsComputerComponent computer,
        IEnumerable<(RequisitionsItemEntry Item, int Amount)> requests)
    {
        var plan = RequisitionsPackingPlan.Build(requests, computer.ItemShipmentWeightLimit);
        var shipments = plan.Crates.Select(crate => new RequisitionsEntry
        {
            Crate = computer.ItemShipmentCrate,
            Entities = new List<EntProtoId>(crate.Items),
            PackedWeight = crate.Weight,
        }).ToList();

        shipments.AddRange(plan.Loose.Select(item => new RequisitionsEntry
        {
            Crate = item.Prototype,
            PackedWeight = item.Weight,
        }));
        return shipments;
    }

    private bool TryConsumeItem(
        ItemizedCatalogRuntime runtime,
        EntProtoId prototype,
        int amount,
        Dictionary<ItemizedSource, Dictionary<EntProtoId, int>> simulated)
    {
        if (!runtime.SourcesByItem.TryGetValue(prototype, out var sourceKeys))
            return false;

        if (sourceKeys.Any(key => runtime.Sources[key].Unlimited))
            return true;

        var sources = sourceKeys.Select(key => runtime.Sources[key])
            .OrderBy(source => CompleteBundles(source, simulated[source]))
            .ThenByDescending(source => simulated[source][prototype])
            .ToList();

        var remaining = amount;
        foreach (var source in sources)
        {
            var take = Math.Min(remaining, simulated[source][prototype]);
            simulated[source][prototype] -= take;
            remaining -= take;
            if (remaining == 0)
                return true;
        }

        return false;
    }

    private static int CompleteBundles(ItemizedSource source, Dictionary<EntProtoId, int> current)
    {
        return source.Manifest.Min(pair => current[pair.Key] / pair.Value);
    }

    private static int SecondsUntilReplenish(ItemizedSource source, TimeSpan time)
    {
        return source.NextReplenish > time
            ? (int) Math.Ceiling((source.NextReplenish - time).TotalSeconds)
            : 0;
    }

    private static void StartSourceReplenish(ItemizedSource source, TimeSpan time)
    {
        if (source.NextReplenish != TimeSpan.Zero)
            return;

        source.NextReplenish = time + (source.Entry.StockReplenishDelay > TimeSpan.Zero
            ? source.Entry.StockReplenishDelay
            : TimeSpan.Zero);
    }

    private sealed class ItemizedCatalogRuntime
    {
        public readonly Dictionary<(int Category, int Order), ItemizedSource> Sources = new();
        public readonly Dictionary<EntProtoId, List<(int Category, int Order)>> SourcesByItem = new();
    }

    private sealed class ItemizedSource
    {
        public readonly RequisitionsEntry Entry;
        public readonly Dictionary<EntProtoId, int> Manifest;
        public readonly Dictionary<EntProtoId, int> Maximum = new();
        public Dictionary<EntProtoId, int> Current = new();
        public readonly bool Unlimited;
        public readonly int MaxBundles;
        public TimeSpan NextReplenish;

        public bool IsFull => Unlimited || Current.All(pair => pair.Value >= Maximum[pair.Key]);

        public ItemizedSource(RequisitionsEntry entry, Dictionary<EntProtoId, int> manifest, TimeSpan time)
        {
            Entry = entry;
            Manifest = manifest;
            Unlimited = entry.MaxStock <= 0;
            MaxBundles = Math.Max(0, entry.MaxStock);
            var startingBundles = entry.StartingStock < 0
                ? MaxBundles
                : Math.Clamp(entry.StartingStock, 0, MaxBundles);

            foreach (var (prototype, amount) in manifest)
            {
                Maximum[prototype] = amount * MaxBundles;
                Current[prototype] = amount * startingBundles;
            }

            if (!Unlimited && startingBundles < MaxBundles)
            {
                NextReplenish = time + (entry.StockReplenishDelay > TimeSpan.Zero
                    ? entry.StockReplenishDelay
                    : TimeSpan.Zero);
            }
        }

        public void AddBundles(int amount)
        {
            foreach (var (prototype, perBundle) in Manifest)
                Current[prototype] = Math.Min(Maximum[prototype], Current[prototype] + perBundle * amount);

            if (IsFull)
                NextReplenish = TimeSpan.Zero;
        }

        public void RefillToMaximum()
        {
            Current = new Dictionary<EntProtoId, int>(Maximum);
            NextReplenish = TimeSpan.Zero;
        }
    }
}
