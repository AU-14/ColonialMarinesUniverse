// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.Trade;
using Content.Shared.Mobs.Components;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.CMU14.Trade;

// What a drone pays for a thing. Only the contracts in the platform buy: a thing is paid by the best price that one of them lists for it, and a thing that none of them lists is not
// bought at all. A parcel is worth what is in it, and a bag or a box is worth what it holds and its own price.
public sealed class TradePricingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private readonly Dictionary<(string Contract, string Prototype), TradeGood?> _byPrototype = new();
    private readonly Dictionary<string, HashSet<string>> _ancestors = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ =>
        {
            _byPrototype.Clear();
            _ancestors.Clear();
        });
    }

    private HashSet<string> AncestorsOf(string prototype)
    {
        if (_ancestors.TryGetValue(prototype, out var known))
            return known;

        var set = new HashSet<string>();
        CollectAncestors(prototype, set);

        _ancestors[prototype] = set;
        return set;
    }

    // The abstract prototypes cannot be indexed, so the parents are read from the data of the prototype, abstract or not.
    private void CollectAncestors(string id, HashSet<string> into)
    {
        if (!into.Add(id))
            return;

        if (!_proto.TryGetMapping(typeof(EntityPrototype), id, out var data) || !data.TryGet("parent", out var parentNode))
            return;

        if (parentNode is ValueDataNode single)
        {
            CollectAncestors(single.Value, into);
        }
        else if (parentNode is SequenceDataNode sequence)
        {
            foreach (var node in sequence)
            {
                if (node is ValueDataNode value)
                    CollectAncestors(value.Value, into);
            }
        }
    }

    // The line of the contract that the thing is, or null when the contract does not list it.
    public TradeGood? Match(TradeContractPrototype contract, EntityUid uid)
    {
        if (TryComp<StackComponent>(uid, out var stack))
        {
            foreach (var good in contract.Goods)
            {
                if (good.Stack != null && good.Stack == stack.StackTypeId)
                    return good;
            }
        }

        if (MetaData(uid).EntityPrototype is not { } proto)
            return null;

        return MatchPrototype(contract, proto);
    }

    private TradeGood? MatchPrototype(TradeContractPrototype contract, EntityPrototype proto)
    {
        var key = (contract.ID, proto.ID);
        if (_byPrototype.TryGetValue(key, out var known))
            return known;

        TradeGood? found = null;
        var ancestors = AncestorsOf(proto.ID);

        foreach (var good in contract.Goods)
        {
            if (good.Entities.Exists(ancestors.Contains) || good.Components.Exists(name => HasComponentNamed(proto, name)))
            {
                found = good;
                break;
            }
        }

        _byPrototype[key] = found;
        return found;
    }

    private bool HasComponentNamed(EntityPrototype proto, string name)
    {
        return _factory.TryGetRegistration(name, out var registration) && proto.Components.ContainsKey(registration.Name);
    }

    // What the things are worth by the contracts. credits gets, for each contract, how much of the sum it paid: the points of trade of it.
    public double GetPrice(EntityUid uid, IReadOnlyList<Entity<TradeContractComponent>> contracts, Dictionary<EntityUid, double>? credits = null)
    {
        if (HasComp<MobStateComponent>(uid))
            return 0;

        if (HasComp<TradeParcelComponent>(uid))
        {
            double inside = 0;
            if (_container.TryGetContainer(uid, TradeParcelComponent.ContainerId, out var parcel))
            {
                foreach (var thing in parcel.ContainedEntities)
                    inside += GetPrice(thing, contracts, credits);
            }

            return inside;
        }

        double total = 0;
        double best = 0;
        EntityUid? payer = null;
        var count = TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1;

        foreach (var contract in contracts)
        {
            if (!_proto.TryIndex(contract.Comp.Contract, out var proto) || Match(proto, uid) is not { Sell: > 0 } good)
                continue;

            var price = good.Sell * count;
            if (price > best)
            {
                best = price;
                payer = contract.Owner;
            }
        }

        if (payer is { } paying)
        {
            total += best;
            if (credits != null)
                credits[paying] = credits.GetValueOrDefault(paying) + best;
        }

        if (TryComp<StorageComponent>(uid, out var storage))
        {
            foreach (var thing in storage.Container.ContainedEntities)
                total += GetPrice(thing, contracts, credits);
        }

        return total;
    }
}
