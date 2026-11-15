using Content.Shared.CMU14.Yautja;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Yautja;

/// <summary>
/// cmss13 turns a pred cape into an armour accessory so it stops taking the back slot. Here the cape is simply
/// fastened into a slot on the armour and drawn from there.
/// </summary>
public sealed partial class YautjaCapeMountSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCapeMountComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<YautjaCapeMountComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<YautjaCapeMountComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<YautjaCapeMountComponent, ExaminedEvent>(OnMountExamined);
        SubscribeLocalEvent<YautjaCapeComponent, ExaminedEvent>(OnCapeExamined);
    }

    private void OnMapInit(Entity<YautjaCapeMountComponent> ent, ref MapInitEvent args)
    {
        EnsureContainer(ent);
    }

    private void OnInteractUsing(Entity<YautjaCapeMountComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<YautjaCapeComponent>(args.Used))
            return;

        args.Handled = true;
        var container = EnsureContainer(ent);
        if (container.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cape-mount-occupied", ("armor", ent.Owner)), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (!_containers.Insert(args.Used, container))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-cape-mount-attached", ("cape", args.Used), ("armor", ent.Owner)), args.User, args.User);
    }

    private void OnGetVerbs(Entity<YautjaCapeMountComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var container = EnsureContainer(ent);
        if (container.ContainedEntity is not { } cape)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-yautja-cape-mount-remove"),
            Priority = 2,
            Act = () =>
            {
                if (_hands.TryPickupAnyHand(user, cape))
                    _popup.PopupEntity(Loc.GetString("cmu-yautja-cape-mount-removed", ("cape", cape)), user, user);
            },
        });
    }

    private void OnMountExamined(Entity<YautjaCapeMountComponent> ent, ref ExaminedEvent args)
    {
        if (EnsureContainer(ent).ContainedEntity is { } cape)
            args.PushMarkup(Loc.GetString("cmu-yautja-cape-mount-examine", ("cape", cape)));
    }

    private void OnCapeExamined(Entity<YautjaCapeComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("cmu-yautja-cape-mount-hint"));
    }

    private ContainerSlot EnsureContainer(Entity<YautjaCapeMountComponent> ent)
    {
        ent.Comp.Container ??= _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.ContainerId);
        return ent.Comp.Container;
    }
}
