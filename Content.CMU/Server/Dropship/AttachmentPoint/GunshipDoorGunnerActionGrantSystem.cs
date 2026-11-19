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

        SubscribeLocalEvent<GunshipDoorGunnerActionComponent, BuckledEvent>(OnGunnerBuckled);
        SubscribeLocalEvent<GunshipDoorGunnerActionComponent, UnbuckledEvent>(OnGunnerUnbuckled);
    }

    private void OnGunnerBuckled(Entity<GunshipDoorGunnerActionComponent> gun, ref BuckledEvent args)
    {
        // Grant hang-out action to the buckled entity (the gunner)
        _actions.AddAction(args.BuckledEntity, ref gun.Comp.HangOutActionEntity, gun.Comp.HangOutAction, gun.Owner);
        
        // Grant hang-back action to the buckled entity
        _actions.AddAction(args.BuckledEntity, ref gun.Comp.HangBackActionEntity, gun.Comp.HangBackAction, gun.Owner);

        Dirty(gun);
    }

    private void OnGunnerUnbuckled(Entity<GunshipDoorGunnerActionComponent> gun, ref UnbuckledEvent args)
    {
        // Remove actions from the unbuckled entity
        if (gun.Comp.HangOutActionEntity is { } hangOut)
            _actions.RemoveAction(args.BuckledEntity, hangOut);

        if (gun.Comp.HangBackActionEntity is { } hangBack)
            _actions.RemoveAction(args.BuckledEntity, hangBack);

        gun.Comp.HangOutActionEntity = null;
        gun.Comp.HangBackActionEntity = null;

        Dirty(gun);
    }
}
