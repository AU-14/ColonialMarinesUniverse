using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Emplacements;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Weapons.Ranged;

public sealed partial class CMUMountedIFFSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AttachableIFFSystem _iff = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        // Client BuckleSystem owns this pair; only the server grants operator actions.
        if (_net.IsServer)
            SubscribeLocalEvent<BuckleComponent, BuckledEvent>(OnBuckled);
        SubscribeLocalEvent<CMUMountedIFFOperatorComponent, UnbuckledEvent>(OnUnbuckled);
        SubscribeLocalEvent<CMUMountedIFFOperatorComponent, ComponentShutdown>(OnOperatorShutdown);
        SubscribeLocalEvent<CMUMountedIFFOperatorComponent, CMUToggleMountedIFFActionEvent>(OnToggle);
        SubscribeLocalEvent<CMUMountedIFFComponent, ComponentShutdown>(OnGunShutdown);
        SubscribeLocalEvent<CMUMountedIFFComponent, AttemptShootEvent>(OnAttemptShoot);
    }

    private void OnBuckled(Entity<BuckleComponent> ent, ref BuckledEvent args)
    {
        if (_net.IsClient || ent.Comp.BuckledTo is not { } mount ||
            !TryComp(mount, out WeaponMountComponent? mounted) || mounted.MountedEntity is not { } weapon ||
            !TryComp(weapon, out CMUMountedIFFComponent? iff))
            return;

        var op = EnsureComp<CMUMountedIFFOperatorComponent>(ent);
        op.Weapon = weapon;
        op.Mount = mount;
        iff.Operator = ent.Owner;
        _actions.AddAction(ent.Owner, ref op.ToggleAction, "CMUActionMountedIFF");
        _actions.SetToggled(op.ToggleAction, iff.Enabled);
        Dirty(ent.Owner, op);
    }

    private void OnUnbuckled(Entity<CMUMountedIFFOperatorComponent> ent, ref UnbuckledEvent args)
    {
        if (!_net.IsClient)
            RemComp<CMUMountedIFFOperatorComponent>(ent);
    }

    private void OnOperatorShutdown(Entity<CMUMountedIFFOperatorComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        if (TryComp(ent.Comp.Weapon, out CMUMountedIFFComponent? iff) && iff.Operator == ent.Owner)
            iff.Operator = null;

        if (ent.Comp.ToggleAction is not { } action)
            return;

        _actions.RemoveAction(ent.Owner, action);
        QueueDel(action);
        ent.Comp.ToggleAction = null;
    }

    private void OnGunShutdown(Entity<CMUMountedIFFComponent> ent, ref ComponentShutdown args)
    {
        if (!_net.IsClient && ent.Comp.Operator is { } user &&
            TryComp(user, out CMUMountedIFFOperatorComponent? op) && op.Weapon == ent.Owner)
            RemComp<CMUMountedIFFOperatorComponent>(user);
    }

    private bool IsOperator(EntityUid user, EntityUid mount, EntityUid weapon)
        => TryComp(user, out BuckleComponent? buckle) && buckle.BuckledTo == mount &&
           TryComp(mount, out WeaponMountComponent? mounted) && mounted.User == user && mounted.MountedEntity == weapon;

    private void OnToggle(Entity<CMUMountedIFFOperatorComponent> ent, ref CMUToggleMountedIFFActionEvent args)
    {
        if (args.Handled || !IsOperator(ent, ent.Comp.Mount, ent.Comp.Weapon) ||
            !TryComp(ent.Comp.Weapon, out CMUMountedIFFComponent? iff))
            return;

        args.Handled = true;
        iff.Enabled = !iff.Enabled;
        Dirty(ent.Comp.Weapon, iff);
        _actions.SetToggled(ent.Comp.ToggleAction, iff.Enabled);
        _popup.PopupClient(Loc.GetString(iff.Enabled ? "cmu-mounted-iff-on" : "cmu-mounted-iff-off"), ent, ent);
    }

    private void OnAttemptShoot(Entity<CMUMountedIFFComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !ent.Comp.Enabled ||
            !TryComp(args.User, out CMUMountedIFFOperatorComponent? op) || op.Weapon != ent.Owner ||
            !IsOperator(args.User, op.Mount, ent.Owner))
            return;

        _iff.CheckPreventFriendlyFire(ent, ref args);
    }
}
