// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Mining;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Mining;

// Makes what a deposit holds when the deposit appears: from one to five kinds of ore, picked by the weights of its table, each with
// its own share of the total amount.
public sealed class OreDepositSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OreDepositComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OreDepositComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<OreDepositComponent> deposit, ref MapInitEvent args)
    {
        Generate(deposit);
    }

    public void Generate(Entity<OreDepositComponent> deposit)
    {
        var comp = deposit.Comp;
        if (comp.Generated)
            return;

        comp.Generated = true;
        comp.Ores.Clear();

        if (!_proto.TryIndex(comp.Table, out var table) || table.Ores.Count == 0)
            return;

        var kinds = Math.Min(_random.Next(1, comp.MaxKinds + 1), table.Ores.Count);
        var left = new Dictionary<ProtoId<StackPrototype>, float>(table.Ores);
        var picked = new List<ProtoId<StackPrototype>>();

        // The kinds are drawn one after another, each by weight among those not drawn yet.
        for (var i = 0; i < kinds; i++)
        {
            var total = 0f;
            foreach (var weight in left.Values)
                total += weight;

            var roll = _random.NextFloat() * total;
            ProtoId<StackPrototype>? chosen = null;
            foreach (var (ore, weight) in left)
            {
                chosen = ore;
                roll -= weight;
                if (roll <= 0f)
                    break;
            }

            if (chosen is not { } found)
                break;

            picked.Add(found);
            left.Remove(found);
        }

        // Each kind gets a share of the total, the shares drawn at random; every kind has at least one piece.
        var amount = _random.Next(comp.MinTotal, comp.MaxTotal + 1);
        var shares = new List<float>();
        var sum = 0f;
        foreach (var _ in picked)
        {
            var share = 0.5f + _random.NextFloat();
            shares.Add(share);
            sum += share;
        }

        var given = 0;
        for (var i = 0; i < picked.Count; i++)
        {
            var count = Math.Max(1, (int) (amount * shares[i] / sum));
            comp.Ores[picked[i]] = count;
            given += count;
        }

        if (picked.Count > 0 && given < amount)
            comp.Ores[picked[0]] += amount - given;
    }

    private void OnExamined(Entity<OreDepositComponent> deposit, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var names = new List<string>();
        foreach (var ore in deposit.Comp.Ores.Keys)
        {
            names.Add(_proto.TryIndex(ore, out var stack) ? Loc.GetString(stack.Name) : ore.ToString());
        }

        args.PushMarkup(names.Count == 0
            ? Loc.GetString("au14-deposit-examine-empty")
            : Loc.GetString("au14-deposit-examine", ("ores", string.Join(", ", names))));
    }
}
