using System.Numerics;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Emplacements;
using Content.Shared.Buckle.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.CMU14.Dropship.AttachmentPoint;

/// <summary>
/// Handles door gunner hang-out/hang-back actions for side-mounted gunship door guns.
/// When hanging out, the gunner is positioned forward and the gun rotates outward (+90/-90 degrees).
/// The gunner cannot get off until they hang back inside.
/// </summary>
public sealed class GunshipDoorGunnerHangOutSystem : EntitySystem
{
    [Dependency] private SharedWeaponMountSystem _mounts = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActiveGunshipDoorGunnerComponent, GunshipDoorGunnerHangOutActionEvent>(OnHangOut);
        SubscribeLocalEvent<ActiveGunshipDoorGunnerComponent, GunshipDoorGunnerHangBackActionEvent>(OnHangBack);
        SubscribeLocalEvent<GunshipDoorGunnerHangingOutComponent, UnbuckleAttemptEvent>(OnUnbuckleAttempt);
    }

    private void OnHangOut(Entity<ActiveGunshipDoorGunnerComponent> gunner, ref GunshipDoorGunnerHangOutActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(gunner, out GunshipDoorGunnerHangingOutComponent? hangingComp))
            hangingComp = EnsureComp<GunshipDoorGunnerHangingOutComponent>(gunner);

        if (hangingComp.HangingOut)
            return;

        // Get the gun entity (should be this entity itself as a weapon mount)
        if (!TryComp(gunner, out WeaponMountComponent? mount))
            return;

        // Store current rotation for restoration
        var gunXform = Transform(gunner);
        hangingComp.StoredRotation = gunXform.LocalRotation;

        // Position the gunner forward (1 tile) from current position
        var forward = gunXform.LocalRotation.ToWorldVec();
        var newPos = gunXform.LocalPosition + forward * 1f;
        _transform.SetLocalPosition(gunner, newPos);

        // Rotate the gun outward (+90 degrees for right, -90 for left)
        // Determine which side based on current rotation or gun orientation
        var rotationOffset = DetermineHangOutRotation(gunXform.LocalRotation);
        var newRotation = gunXform.LocalRotation + rotationOffset;
        _transform.SetLocalRotation(gunner, newRotation);

        hangingComp.HangingOut = true;
        Dirty(gunner, hangingComp);

        args.Handled = true;
    }

    private void OnHangBack(Entity<ActiveGunshipDoorGunnerComponent> gunner, ref GunshipDoorGunnerHangBackActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(gunner, out GunshipDoorGunnerHangingOutComponent? hangingComp) || !hangingComp.HangingOut)
            return;

        // Restore original position and rotation
        var gunXform = Transform(gunner);
        
        // Move back to the mount/door position (should be at the door)
        if (gunner.Comp.AttachmentPoint is { } point && TryComp(point, out var pointXform))
        {
            var pointTransform = Transform(point);
            if (pointTransform.GridUid is { } grid)
            {
                // Position back at the door (similar to original door gunner positioning)
                var pointPosition = pointTransform.LocalPosition;
                _transform.SetCoordinates(gunner, new EntityCoordinates(grid, pointPosition));
            }
        }

        // Restore rotation
        if (hangingComp.StoredRotation.HasValue)
        {
            _transform.SetLocalRotation(gunner, hangingComp.StoredRotation.Value);
        }

        hangingComp.HangingOut = false;
        Dirty(gunner, hangingComp);

        args.Handled = true;
    }

    private void OnUnbuckleAttempt(Entity<GunshipDoorGunnerHangingOutComponent> hangingGunner, ref UnbuckleAttemptEvent args)
    {
        // Prevent unbuckling while hanging out
        if (hangingGunner.Comp.HangingOut)
            args.Cancel();
    }

    /// <summary>
    /// Determines the rotation offset for hanging out based on the current gun rotation.
    /// For now, returns +90 degrees (can be refined based on gun orientation).
    /// </summary>
    private Angle DetermineHangOutRotation(Angle currentRotation)
    {
        // Simple implementation: rotate +90 degrees (right side)
        // This can be enhanced to detect left vs right side based on hardpoint location
        return Angle.FromDegrees(90);
    }
}
