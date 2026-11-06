using System.Numerics;
using System.Linq;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Dropship.DirectFire;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client.CMU14.Dropship.DirectFire;

public sealed class MohawkGunnerOverlay(IEntityManager entities, IPlayerManager player) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public bool ShouldDrawForViewport(Robust.Client.UserInterface.CustomControls.IViewportControl? viewport)
        => viewport is Content.Client.Viewport.ScalingViewport { Parent: Content.Client.UserInterface.Controls.MainViewport };

    protected override void Draw(in OverlayDrawArgs args)
    {
        var query = entities.EntityQueryEnumerator<DropshipTerminalWeaponsComponent>();
        while (query.MoveNext(out var uid, out var terminal))
        {
            if (!terminal.ChinGunAr || terminal.ChinGunOperator != player.LocalEntity ||
                !ShouldDrawForViewport(args.ViewportControl))
                continue;
            var transform = entities.System<SharedTransformSystem>();
            var gun = entities.System<MohawkChinGunSystem>();
            Vector2 center;
            float radius;
            Angle rotation = Angle.Zero;
            var arc = 360f;
            if (terminal.ChinGunAirborne && terminal.Target is { } flare &&
                entities.TryGetComponent(flare, out TransformComponent? flareTransform))
            {
                var location = transform.GetMapCoordinates(flare, flareTransform);
                if (location.MapId != args.MapId)
                    continue;
                center = location.Position;
                radius = terminal.ChinGunFlareRadius;
            }
            else
            {
                if (!entities.System<SharedDropshipSystem>().TryGetGridDropship(uid, out var ship))
                    continue;
                var point = ship.Comp.AttachmentPoints.FirstOrDefault(point =>
                    entities.HasComponent<MohawkChinGunComponent>(point));
                if (!entities.TryGetComponent(point, out MohawkChinGunComponent? chin))
                    continue;
                var location = transform.ToMapCoordinates(gun.GetMountCoordinates((point, chin)));
                if (location.MapId != args.MapId)
                    continue;
                center = location.Position;
                radius = chin.Range;
                rotation = transform.GetWorldRotation(ship);
                if (!entities.TryGetComponent(point, out DropshipWeaponPointComponent? slot) ||
                    !entities.System<Robust.Shared.Containers.SharedContainerSystem>().TryGetContainer(point, slot.WeaponContainerSlotId, out var container))
                    continue;
                var weapon = container.ContainedEntities.FirstOrDefault(weapon => entities.HasComponent<GunshipDirectFireWeaponComponent>(weapon));
                if (!entities.TryGetComponent(weapon, out GunshipDirectFireWeaponComponent? direct))
                    continue;
                arc = direct.GimbalDegrees;
            }
            var handle = args.WorldHandle;
            handle.SetTransform(Matrix3x2.Identity);
            var previous = center + (rotation + Angle.FromDegrees(-arc / 2)).RotateVec(Vector2.UnitY * radius);
            if (arc < 360)
                handle.DrawLine(center, previous, Color.Cyan.WithAlpha(0.7f));
            for (var i = 1; i <= 96; i++)
            {
                var next = center + (rotation + Angle.FromDegrees(-arc / 2 + arc * i / 96)).RotateVec(Vector2.UnitY * radius);
                handle.DrawLine(previous, next, Color.Cyan.WithAlpha(0.7f));
                previous = next;
            }
            if (arc < 360)
                handle.DrawLine(center, previous, Color.Cyan.WithAlpha(0.7f));
            if ((terminal.TrackedTarget ?? terminal.ChinGunAiTarget) is { } target &&
                entities.TryGetComponent(target, out TransformComponent? targetTransform))
            {
                var location = transform.GetMapCoordinates(target, targetTransform);
                if (location.MapId == args.MapId)
                    handle.DrawCircle(location.Position, 0.6f, Color.Yellow, false);
            }
        }
    }
}
