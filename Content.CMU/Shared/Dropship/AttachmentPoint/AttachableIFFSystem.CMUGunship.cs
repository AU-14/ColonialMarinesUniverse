using System.Numerics;
using Content.Shared.Physics;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics;

namespace Content.Shared._RMC14.Attachable.Systems;

public sealed partial class AttachableIFFSystem
{
    /// <summary>
    /// Uses the optic IFF's predicted scatter, range, and projectile-width rays,
    /// but only interlocks when a ray's first impact belongs to this gunship.
    /// </summary>
    public void CMUCheckPreventGunshipHit(EntityUid weapon, EntityUid mount, EntityUid ship, ref AttemptShootEvent args)
    {
        if (args.Cancelled || args.ToCoordinates is not { } target || !TryComp(weapon, out GunComponent? gun))
            return;

        var from = _transform.ToMapCoordinates(args.FromCoordinates);
        var to = _transform.ToMapCoordinates(target);
        if (from.MapId != to.MapId)
            return;

        var direction = to.Position - from.Position;
        if (direction == Vector2.Zero)
            return;

        var shot = GetPredictedShotDirection((weapon, gun), direction);
        var range = GetIffCheckDistance((weapon, gun));
        if (shot == Vector2.Zero || range <= 0)
            return;

        var radius = GetIffCheckRadius((weapon, gun));
        var perpendicular = new Vector2(-shot.Y, shot.X);
        var user = args.User;
        if (HitsShip(0) || radius > 0 && (HitsShip(radius) || HitsShip(-radius)))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("cmu-gunship-crew-iff-blocked");
        }

        bool HitsShip(float offset)
        {
            var ray = new CollisionRay(from.Position + perpendicular * offset, shot, (int) CollisionGroup.BulletImpassable);
            var hits = _physics.IntersectRayWithPredicate(from.MapId, ray,
                (user, weapon, mount),
                static (uid, state) => uid == state.user || uid == state.weapon || uid == state.mount,
                range, returnOnFirstHit: false);
            foreach (var hit in hits)
            {
                if (hit.HitEntity == ship)
                    return true;

                // Structural fixtures are anchored to the ship. People and loose
                // objects aboard it are other targets, not part of the hull.
                var transform = Transform(hit.HitEntity);
                return transform.Anchored && transform.GridUid == ship;
            }
            return false;
        }
    }
}
