using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Audio.Systems;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaRecallSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaPowerSystem _power = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, YautjaRecallActionEvent>(OnRecall);
        SubscribeLocalEvent<YautjaRecallableComponent, UseInHandEvent>(OnRecallableUsed,
            before: [typeof(YautjaHealingGunSystem), typeof(YautjaSmartDiscSystem)]);
    }

    private void OnRecallableUsed(Entity<YautjaRecallableComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled ||
            !HasComp<YautjaComponent>(args.User) ||
            HasComp<YautjaBadBloodComponent>(args.User))
            return;

        if (ent.Comp.YautjaOwner is { } owner && !TerminatingOrDeleted(owner))
            return;

        ent.Comp.YautjaOwner = args.User;
        Dirty(ent);
        args.Handled = true;
        _popup.PopupClient(
            Loc.GetString("cmu-yautja-recall-bound", ("item", ent.Owner)),
            ent.Owner,
            args.User,
            PopupType.Small);
    }

    private void OnRecall(Entity<YautjaBracerComponent> ent, ref YautjaRecallActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        if (!CanUseYautjaRecall(args.Performer))
            return;

        args.Handled = true;

        if (!_hands.TryGetEmptyHand(args.Performer, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-recall-hands-full"), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        var userCoords = _transform.GetMapCoordinates(args.Performer);
        EntityUid? closest = null;
        var closestDistance = float.MaxValue;
        var acidBlocked = false;
        var containerBlocked = false;

        var recallables = EntityQueryEnumerator<YautjaRecallableComponent>();
        while (recallables.MoveNext(out var uid, out var recallable))
        {
            if (recallable.YautjaOwner != args.Performer ||
                TerminatingOrDeleted(uid) ||
                _hands.IsHolding(args.Performer, uid))
                continue;

            if (HasComp<TimedCorrodingComponent>(uid) || HasComp<DamageableCorrodingComponent>(uid))
            {
                acidBlocked = true;
                continue;
            }

            if (IsEnclosed(uid))
            {
                containerBlocked = true;
                continue;
            }

            var itemCoords = _transform.GetMapCoordinates(uid);
            if (itemCoords.MapId != userCoords.MapId)
                continue;

            var distance = (itemCoords.Position - userCoords.Position).LengthSquared();
            if (distance >= closestDistance)
                continue;

            closest = uid;
            closestDistance = distance;
        }

        if (closest is not { } item)
        {
            var message = acidBlocked
                ? "cmu-yautja-recall-acid"
                : containerBlocked
                    ? "cmu-yautja-recall-contained"
                    : "cmu-yautja-recall-none";
            _popup.PopupEntity(Loc.GetString(message), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!_hands.CanPickupAnyHand(args.Performer, item, checkActionBlocker: false) ||
            !_power.HasPowerPopup(args.Performer, 70))
            return;

        if (!_hands.TryPickupAnyHand(args.Performer, item, checkActionBlocker: false))
            return;

        _power.TryRemovePower(args.Performer, 70);
        _audio.PlayPredicted(ent.Comp.RecallSound, item, args.Performer);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-recall-success", ("item", item)), args.Performer, args.Performer);
    }

    private bool IsEnclosed(EntityUid item)
    {
        if (!_containers.TryGetContainingContainer((item, null, null), out var container))
            return false;

        return !HasComp<HandsComponent>(container.Owner) || !_hands.IsHolding(container.Owner, item);
    }

    private bool CanUseYautjaRecall(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               (TryComp(user, out YautjaThrallComponent? thrall) && thrall.Blooded && thrall.TechAuthorized);
    }
}
