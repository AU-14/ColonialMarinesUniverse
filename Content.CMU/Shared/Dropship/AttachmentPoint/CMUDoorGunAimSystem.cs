using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Emplacements;
using Content.Shared.Buckle.Components;
using Content.Shared.CombatMode;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

public sealed partial class CMUDoorGunAimSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;

    public bool TryGetMount(EntityUid user, out EntityUid mount,
        [NotNullWhen(true)] out CMUDoorGunVisualComponent? visual)
    {
        mount = default;
        visual = null;
        if (!_combat.IsInCombatMode(user) ||
            !TryComp(user, out BuckleComponent? buckle) || buckle.BuckledTo is not { } strapped ||
            !TryComp(strapped, out WeaponMountComponent? weaponMount) ||
            !TryComp(strapped, out CMUDoorGunVisualComponent? gunVisual) ||
            weaponMount.User != user && gunVisual.Operator != user)
            return false;

        mount = strapped;
        visual = gunVisual;
        return true;
    }

    public bool TryGetAimOffset(EntityUid mount, EntityCoordinates target, out Angle offset)
    {
        offset = Angle.Zero;
        if (!target.IsValid(EntityManager) ||
            !TryComp(mount, out WeaponMountComponent? weaponMount) ||
            !TryComp(weaponMount.MountedEntity, out MountableWeaponComponent? weapon))
            return false;

        var mapTarget = _transform.ToMapCoordinates(target);
        if (mapTarget.MapId != Transform(mount).MapID)
            return false;

        var direction = mapTarget.Position - _transform.GetWorldPosition(mount);
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || direction.LengthSquared() < 0.000001f)
            return false;

        return TryGetAimOffset(_transform.GetWorldRotation(mount), Angle.FromWorldVec(direction), weapon.ShootArc, out offset);
    }

    public static bool TryGetAimOffset(Angle facing, Angle target, int shootArc, out Angle offset)
    {
        offset = Angle.ShortestDistance(facing, target);
        return double.IsFinite(offset.Theta) && Math.Abs(offset.Degrees) <= shootArc / 2f;
    }
}
