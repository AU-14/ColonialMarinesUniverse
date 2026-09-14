using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.Threats.Mobs.Ape;

public sealed partial class ApeLimbRipSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ApeLimbRipComponent, ApeLimbRipActionEvent>(OnAction);
        SubscribeLocalEvent<ApeLimbRipComponent, ApeLimbRipDoAfterEvent>(OnDoAfter);
    }

    private void OnAction(Entity<ApeLimbRipComponent> ape, ref ApeLimbRipActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanRip(ape, args.Target))
            return;

        args.Handled = true;

        var ev = new ApeLimbRipDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, ape, ape.Comp.Delay, ev, ape, args.Target)
        {
            BreakOnDamage = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<ApeLimbRipComponent> ape, ref ApeLimbRipDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!CanRip(ape, target))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        if (!TryComp<BodyComponent>(target, out var body))
            return;

        foreach (var type in new[] { BodyPartType.Arm, BodyPartType.Leg })
        {
            foreach (var limb in _body.GetBodyChildrenOfType(target, type, body))
            {
                if (!_containers.TryGetContainingContainer((limb.Id, null, null), out var container) ||
                    !_containers.Remove(limb.Id, container))
                    continue;

                _transform.SetCoordinates(limb.Id, ape.Owner.ToCoordinates());
                _transform.AttachToGridOrMap(limb.Id);
                _hands.TryPickupAnyHand(ape, limb.Id);
                _popup.PopupClient(Loc.GetString(ape.Comp.FinishedPopup), ape, ape);
                _audio.PlayPvs(ape.Comp.Sound, ape);
                return;
            }
        }
    }

    private bool CanRip(Entity<ApeLimbRipComponent> ape, EntityUid target)
    {
        if (target == ape.Owner ||
            !_mobState.IsDead(target) ||
            !TryComp<BodyComponent>(target, out var body))
        {
            _popup.PopupClient(Loc.GetString(ape.Comp.InvalidTargetPopup), ape, ape, PopupType.SmallCaution);
            return false;
        }

        foreach (var type in new[] { BodyPartType.Arm, BodyPartType.Leg })
        {
            if (_body.GetBodyChildrenOfType(target, type, body).GetEnumerator().MoveNext())
                return true;
        }

        _popup.PopupClient(Loc.GetString(ape.Comp.InvalidTargetPopup), ape, ape, PopupType.SmallCaution);
        return false;
    }
}
