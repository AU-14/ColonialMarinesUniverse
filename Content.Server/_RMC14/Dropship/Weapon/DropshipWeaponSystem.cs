using Content.Shared.AU14.Round; // for WhitelistedShuttleComponent
using Content.Shared._RMC14.Dropship.Weapon;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Dropship.Weapon;

public sealed class DropshipWeaponSystem : SharedDropshipWeaponSystem
{
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;

    protected override void AddPvs(Entity<DropshipTerminalWeaponsComponent> terminal, Entity<ActorComponent?> actor)
    {
        base.AddPvs(terminal, actor);

        if (terminal.Comp.Target is not { } target)
            return;

        if (!Resolve(actor, ref actor.Comp, false))
            return;

        // Faction gating: if both console and target creator have faction and they differ, skip adding the view subscriber
        string? consoleFaction = null;
        if (TryComp(terminal.Owner, out WhitelistedShuttleComponent? whitelist))
            consoleFaction = string.IsNullOrWhiteSpace(whitelist.Faction) ? null : whitelist.Faction;

        string? creatorFaction = null;
        if (TryComp(target, out DropshipTargetComponent? targetComp))
            creatorFaction = string.IsNullOrWhiteSpace(targetComp.CreatorFaction) ? null : targetComp.CreatorFaction;

        if (!string.IsNullOrEmpty(consoleFaction) && !string.IsNullOrEmpty(creatorFaction) &&
            !consoleFaction.Equals(creatorFaction, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _viewSubscriber.AddViewSubscriber(target, actor.Comp.PlayerSession);
    }

    protected override void RemovePvs(Entity<DropshipTerminalWeaponsComponent> terminal, Entity<ActorComponent?> actor)
    {
        base.AddPvs(terminal, actor);

        if (terminal.Comp.Target is not { } target)
            return;

        if (!Resolve(actor, ref actor.Comp, false))
            return;

        // Faction gating: only remove subscriber if it would have been added
        string? consoleFaction = null;
        if (TryComp(terminal.Owner, out WhitelistedShuttleComponent? whitelist))
            consoleFaction = string.IsNullOrWhiteSpace(whitelist.Faction) ? null : whitelist.Faction;

        string? creatorFaction = null;
        if (TryComp(target, out DropshipTargetComponent? targetComp))
            creatorFaction = string.IsNullOrWhiteSpace(targetComp.CreatorFaction) ? null : targetComp.CreatorFaction;

        if (!string.IsNullOrEmpty(consoleFaction) && !string.IsNullOrEmpty(creatorFaction) &&
            !consoleFaction.Equals(creatorFaction, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _viewSubscriber.RemoveViewSubscriber(target, actor.Comp.PlayerSession);
    }

}
