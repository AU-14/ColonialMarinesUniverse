using Content.Shared._CMU14.Intel;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Intel.Tech;

public sealed partial class TechSystem
{
    partial void TryOpenFactionConsole(Entity<TechControlConsoleComponent> ent, ref bool handled)
    {
        if (!CMUFactionTech.TryNormalizeFaction(ent.Comp.Team, out var faction))
            return;

        handled = true;
        if (_net.IsClient)
            return;

        _intel.UpdateTree(_intel.EnsureTechTree(faction));
    }

    partial void TryPurchaseFactionOption(
        Entity<TechControlConsoleComponent> ent,
        TechPurchaseOptionBuiMsg args,
        ref bool handled)
    {
        if (!CMUFactionTech.TryNormalizeFaction(ent.Comp.Team, out var faction))
            return;

        handled = true;
        if (_net.IsClient)
            return;

        var tree = _intel.EnsureTechTree(faction);
        if (tree.Comp.Tree.Tier < args.Tier ||
            !tree.Comp.Tree.Options.TryGetValue(args.Tier, out var tier))
        {
            Log.Warning($"{ToPrettyString(args.Actor)} tried to buy faction tech option with invalid tier {args.Tier}");
            return;
        }

        if (args.Index < 0 || !tier.TryGetValue(args.Index, out var option))
        {
            Log.Warning($"{ToPrettyString(args.Actor)} tried to buy faction tech option with invalid index {args.Index}");
            return;
        }

        if (option.TimeLock > _ticker.RoundDuration() ||
            option.Purchased && !option.Repurchasable ||
            option.Disabled ||
            !_intel.TrySpendFactionWinPoints(faction, option.CurrentCost))
        {
            return;
        }

        tier[args.Index] = option with
        {
            CurrentCost = option.CurrentCost + option.Increase,
            Purchased = true,
        };
        Dirty(tree);

        foreach (var techEvent in option.Events)
            RaiseFactionTechEvent(faction, tree, techEvent);

        _intel.UpdateTree(tree);

        if (_idCard.TryFindIdCard(args.Actor, out var idCard) &&
            TryComp(idCard, out ItemIFFComponent? idCardIFF))
        {
            foreach (var iffFaction in idCardIFF.Factions)
            {
                _core.CreateARESLog(
                    iffFaction,
                    LogCat,
                    (string) $"{Name(args.Actor)} purchased faction intel node: {Localize(option.Name)}");
            }
        }
        else
        {
            _core.CreateARESLog(
                ent,
                LogCat,
                (string) $"{Name(args.Actor)} purchased faction intel node: {Localize(option.Name)}");
        }
    }

    private void RaiseFactionTechEvent(
        string faction,
        Entity<IntelTechTreeComponent> tree,
        object techEvent)
    {
        switch (techEvent)
        {
            case TechUnlockTierEvent unlockTier:
                tree.Comp.Tree.Tier = unlockTier.Tier;
                Dirty(tree);
                break;
            case TechRequisitionsBudgetEvent requisitions:
                var scaling = Math.Max(1, _scaling.GetAliveHumanoids() / 50);
                _requisitions.ChangeBudget(requisitions.Amount * scaling, faction);
                break;
            case TechPartySpawnEvent party:
                RaiseLocalEvent(party with { Team = faction });
                break;
            default:
                RaiseLocalEvent(techEvent);
                break;
        }
    }
}
