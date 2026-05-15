using Content.Shared._RMC14.Actions;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Audio.Systems;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaRecallSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly YautjaPowerSystem _power = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, YautjaRecallActionEvent>(OnRecall);
        SubscribeLocalEvent<YautjaRecallableComponent, GotEquippedEvent>(OnRecallableEquipped);
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

        var userCoords = _transform.GetMapCoordinates(args.Performer);
        EntityUid? closest = null;
        var closestDistance = float.MaxValue;

        foreach (var uid in _lookup.GetEntitiesInRange(userCoords, 12f))
        {
            if (!TryComp(uid, out YautjaRecallableComponent? recallable) ||
                recallable.YautjaOwner != args.Performer)
            {
                continue;
            }

            var itemCoords = _transform.GetMapCoordinates(uid);
            if (itemCoords.MapId != userCoords.MapId)
                continue;

            var distance = (itemCoords.Position - userCoords.Position).LengthSquared();
            if (distance > recallable.Range * recallable.Range || distance >= closestDistance)
                continue;

            closest = uid;
            closestDistance = distance;
        }

        if (closest is not { } item)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-recall-none"), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!_power.TryRemovePower(args.Performer, 70))
            return;

        _transform.SetCoordinates(item, Transform(args.Performer).Coordinates);
        _hands.TryPickupAnyHand(args.Performer, item, checkActionBlocker: false);
        _audio.PlayPredicted(ent.Comp.RecallSound, item, args.Performer);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-recall-success", ("item", item)), args.Performer, args.Performer);
    }

    private void OnRecallableEquipped(Entity<YautjaRecallableComponent> ent, ref GotEquippedEvent args)
    {
        if (ent.Comp.YautjaOwner == null && CanUseYautjaRecall(args.Equipee))
        {
            ent.Comp.YautjaOwner = args.Equipee;
            Dirty(ent);
        }
    }

    private bool CanUseYautjaRecall(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               (TryComp(user, out YautjaThrallComponent? thrall) && thrall.Blooded && thrall.TechAuthorized);
    }
}
