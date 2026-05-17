using Content.Shared._CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaMaskAccessorySystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaMaskAccessoryHolderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<YautjaMaskAccessoryHolderComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<YautjaMaskAccessoryHolderComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnMapInit(Entity<YautjaMaskAccessoryHolderComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Container = _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.ContainerId);
    }

    private void OnInteractUsing(Entity<YautjaMaskAccessoryHolderComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<YautjaMaskOrnamentComponent>(args.Used))
            return;

        if (!HasComp<YautjaComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-mask-ornament-denied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var container = EnsureContainer(ent);
        if (container.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-mask-ornament-occupied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (!_containers.Insert(args.Used, container))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cmu-yautja-mask-ornament-attached", ("item", args.Used)), args.User, args.User);
    }

    private void OnGetVerbs(Entity<YautjaMaskAccessoryHolderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract ||
            !args.CanAccess ||
            !HasComp<YautjaComponent>(args.User))
        {
            return;
        }

        var container = EnsureContainer(ent);
        if (container.ContainedEntity is not { } accessory)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-yautja-mask-ornament-remove"),
            Priority = 2,
            Act = () =>
            {
                if (_hands.TryPickupAnyHand(user, accessory))
                    _popup.PopupEntity(Loc.GetString("cmu-yautja-mask-ornament-removed", ("item", accessory)), user, user);
            },
        });
    }

    private ContainerSlot EnsureContainer(Entity<YautjaMaskAccessoryHolderComponent> ent)
    {
        ent.Comp.Container ??= _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.ContainerId);
        return ent.Comp.Container;
    }
}
