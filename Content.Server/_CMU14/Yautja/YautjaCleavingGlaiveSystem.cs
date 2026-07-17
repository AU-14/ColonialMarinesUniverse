using Content.Shared._CMU14.Yautja;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Robust.Shared.Containers;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaCleavingGlaiveSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCleavingGlaiveComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<YautjaCleavingGlaiveComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<YautjaCleavingGlaiveComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<YautjaCleavingGlaiveComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Container = _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.SkullContainerId);
        _appearance.SetData(ent.Owner, ToggleableVisuals.Enabled, ent.Comp.SkullAttached);
    }

    private void OnInteractUsing(Entity<YautjaCleavingGlaiveComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp<YautjaTrophyComponent>(args.Used, out var trophy) ||
            trophy.Kind != YautjaTrophyKind.HumanSkull)
        {
            return;
        }

        if (!CanMountSkull(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cleaving-glaive-skull-denied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var container = EnsureContainer(ent);
        if (container.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cleaving-glaive-skull-existing", ("skull", args.Used), ("glaive", ent.Owner)), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (!_containers.Insert(args.Used, container))
            return;

        ent.Comp.SkullAttached = true;
        _appearance.SetData(ent.Owner, ToggleableVisuals.Enabled, true);
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-cleaving-glaive-skull-mounted", ("skull", args.Used), ("glaive", ent.Owner)), args.User, args.User);
    }

    private void OnExamined(Entity<YautjaCleavingGlaiveComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.SkullAttached)
            args.PushMarkup(Loc.GetString("cmu-yautja-cleaving-glaive-skull-examine", ("glaive", ent.Owner)));
    }

    private ContainerSlot EnsureContainer(Entity<YautjaCleavingGlaiveComponent> ent)
    {
        ent.Comp.Container ??= _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.SkullContainerId);
        return ent.Comp.Container;
    }

    private bool CanMountSkull(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user);
    }
}
