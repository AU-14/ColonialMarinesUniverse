using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;

namespace Content.Server.CMU14.Dropship.AttachmentPoint;

/// <summary>
/// Grants door gunner hang-out/hang-back actions to operators when they buckle into the mount.
/// </summary>
public sealed class GunshipDoorGunnerActionGrantSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunshipDoorGunnerActionComponent, StrappedEvent>(OnGunnerStrapped);
        SubscribeLocalEvent<GunshipDoorGunnerActionComponent, UnstrappedEvent>(OnGunnerUnstrapped);
    }

    private void OnGunnerStrapped(Entity<GunshipDoorGunnerActionComponent> gun, ref StrappedEvent args)
    {
        // Grant hang-out action to the buckled entity (the gunner)
        _actions.AddAction(args.Buckle.Owner, ref gun.Comp.HangOutActionEntity, gun.Comp.HangOutAction, gun.Owner);
        
        // Grant hang-back action to the buckled entity
        _actions.AddAction(args.Buckle.Owner, ref gun.Comp.HangBackActionEntity, gun.Comp.HangBackAction, gun.Owner);

        Dirty(gun);
    }

    private void OnGunnerUnstrapped(Entity<GunshipDoorGunnerActionComponent> gun, ref UnstrappedEvent args)
    {
        // Remove actions from the unbuckled entity
        if (gun.Comp.HangOutActionEntity is { } hangOut)
            _actions.RemoveAction(args.Buckle.Owner, hangOut);

        if (gun.Comp.HangBackActionEntity is { } hangBack)
            _actions.RemoveAction(args.Buckle.Owner, hangBack);

        gun.Comp.HangOutActionEntity = null;
        gun.Comp.HangBackActionEntity = null;

        Dirty(gun);
    }
}
