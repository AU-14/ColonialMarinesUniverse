using Content.Shared.CMU14.Round; // for WhitelistedShuttleComponent
using Content.Shared._RMC14.Dropship.Weapon;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Dropship.Weapon;

public sealed partial class DropshipWeaponSystem : SharedDropshipWeaponSystem
{
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;

    protected override void AddPvs(Entity<DropshipTerminalWeaponsComponent> terminal, Entity<ActorComponent?> actor)
    {
        base.AddPvs(terminal, actor);
        if (terminal.Comp.CameraTarget is { } camera && Resolve(actor, ref actor.Comp, false) &&
            TryGetTargetEye(terminal, camera, out var cameraEye) && cameraEye != camera)
            _viewSubscriber.AddViewSubscriber(cameraEye, actor.Comp.PlayerSession);

        if (terminal.Comp.Target is not { } target)
            return;

        if (!Resolve(actor, ref actor.Comp, false))
            return;

        // Faction gating: if both console and target creator have faction and they differ, skip adding the view subscriber
        var consoleFaction = GetConsoleFaction(terminal.Owner);

        string? creatorFaction = null;
        if (TryComp(target, out DropshipTargetComponent? targetComp))
            creatorFaction = string.IsNullOrWhiteSpace(targetComp.CreatorFaction) ? null : targetComp.CreatorFaction;

        if (!string.IsNullOrEmpty(consoleFaction) && !string.IsNullOrEmpty(creatorFaction) &&
            !consoleFaction.Equals(creatorFaction, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _viewSubscriber.AddViewSubscriber(target, actor.Comp.PlayerSession);
        // CMU14: elevated eyes sit on another z-level map, outside the target's PVS range.
        if (TryGetTargetEye(terminal, target, out var eye) && eye != target)
            _viewSubscriber.AddViewSubscriber(eye, actor.Comp.PlayerSession);
    }

    protected override void RemovePvs(Entity<DropshipTerminalWeaponsComponent> terminal, Entity<ActorComponent?> actor)
    {
        base.RemovePvs(terminal, actor);
        if (terminal.Comp.CameraTarget is { } camera && Resolve(actor, ref actor.Comp, false) &&
            TryGetTargetEye(terminal, camera, out var cameraEye) && cameraEye != camera)
            _viewSubscriber.RemoveViewSubscriber(cameraEye, actor.Comp.PlayerSession);

        if (terminal.Comp.Target is not { } target)
            return;

        if (!Resolve(actor, ref actor.Comp, false))
            return;

        // Faction may have changed since subscribing. Cleanup must still release the old view.
        _viewSubscriber.RemoveViewSubscriber(target, actor.Comp.PlayerSession);
        if (TryGetTargetEye(terminal, target, out var eye) && eye != target)
            _viewSubscriber.RemoveViewSubscriber(eye, actor.Comp.PlayerSession);
    }

}
