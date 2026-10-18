// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;

namespace Content.Shared.CMU14.ToolDamage;

// Changes the damage a thing takes by the tool that hits it: the preferred tool does more.
public sealed class PreferredToolSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PreferredToolComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<PreferredToolComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Tool is { } tool && _whitelist.IsWhitelistPass(ent.Comp.Whitelist, tool))
            return;

        var reduced = new DamageSpecifier(args.Damage);
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (amount > FixedPoint2.Zero)
                reduced.DamageDict[type] = FixedPoint2.New(amount.Float() * ent.Comp.OtherMultiplier);
        }

        args.Damage = reduced;
    }
}
