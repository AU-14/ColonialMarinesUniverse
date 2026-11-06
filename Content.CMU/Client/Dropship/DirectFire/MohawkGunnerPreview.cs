using System.Numerics;
using Content.Client._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Dropship.DirectFire;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Dropship.DirectFire;

/// <summary>Cursor aiming and range/flare boundaries for the dedicated M90 camera.</summary>
public sealed class MohawkGunnerPreview(
    IEntityManager entities,
    EntityUid console,
    bool first,
    DropshipWeaponsScreen screen) : Control
{
    private readonly IInputManager _input = IoCManager.Resolve<IInputManager>();
    private float _aimElapsed;
    public event Action<MapCoordinates>? CursorAim;
    public event Action? CursorUnavailable;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = VerticalExpand = RectClipContent = true;
        _aimElapsed += args.DeltaSeconds;
        if (!first || _aimElapsed < 0.1f ||
            !entities.TryGetComponent(console, out DropshipTerminalWeaponsComponent? terminal) ||
            terminal.ChinGunOperator == null || terminal.TrackedTarget != null || terminal.ChinGunAr)
            return;
        _aimElapsed = 0;
        var viewport = screen.Viewport;
        var cursor = _input.MouseScreenPosition.Position;
        var local = cursor - viewport.GlobalPixelPosition;
        if (viewport.Visible && local.X >= 0 && local.Y >= 0 &&
            local.X < viewport.PixelWidth && local.Y < viewport.PixelHeight)
            CursorAim?.Invoke(viewport.PixelToMap(cursor));
        else
            CursorUnavailable?.Invoke();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (!first || !entities.TryGetComponent(console, out DropshipTerminalWeaponsComponent? terminal) ||
            terminal.ChinGunEye == null || screen.Viewport.Eye is not { } eye)
            return;
        var transform = entities.System<SharedTransformSystem>();
        Vector2 Pixel(Vector2 world) => screen.Viewport.WorldToScreen(world) - GlobalPixelPosition;
        void Ring(Vector2 origin, float radius, Color color)
        {
            Vector2? previous = null;
            for (var i = 0; i <= 48; i++)
            {
                var next = Pixel(origin + Angle.FromDegrees(i * 360f / 48).RotateVec(Vector2.UnitX * radius));
                if (previous is { } from)
                    handle.DrawLine(from, next, color);
                previous = next;
            }
        }

        if (terminal.ChinGunAiTarget is { } aiTarget && entities.HasComponent<TransformComponent>(aiTarget))
        {
            var location = transform.GetMapCoordinates(aiTarget);
            if (location.MapId == eye.Position.MapId)
                Ring(location.Position, 0.65f, terminal.ChinGunAiApproved ? Color.Lime : Color.Yellow);
        }
        if (terminal.ChinGunAirborne)
        {
            if (terminal.Target is { } flare && entities.HasComponent<TransformComponent>(flare))
            {
                var anchor = transform.GetMapCoordinates(flare);
                Ring(anchor.Position, terminal.ChinGunFlareRadius, Color.Cyan);
                var cursor = screen.Viewport.PixelToMap(_input.MouseScreenPosition.Position);
                Ring(cursor.Position, 0.45f, cursor.MapId == anchor.MapId &&
                    Vector2.DistanceSquared(cursor.Position, anchor.Position) <=
                    terminal.ChinGunFlareRadius * terminal.ChinGunFlareRadius ? Color.Lime : Color.Red);
            }
            return;
        }
        if (!entities.System<SharedDropshipSystem>().TryGetGridDropship(console, out var ship))
            return;
        foreach (var point in ship.Comp.AttachmentPoints)
        {
            if (!entities.TryGetComponent(point, out MohawkChinGunComponent? chin) ||
                !entities.TryGetComponent(point, out GunshipDirectFirePointComponent? mount))
                continue;
            var origin = transform.GetMapCoordinates(point);
            if (origin.MapId != eye.Position.MapId)
                continue;
            var rotation = transform.GetWorldRotation(ship);
            var muzzle = origin.Position + rotation.RotateVec(new Vector2(mount.LateralOffset, mount.ForwardOffset));
            var selected = entities.GetEntity(terminal.ScreenOne.Weapon);
            var arc = selected is { } weapon &&
                      entities.TryGetComponent(weapon, out GunshipDirectFireWeaponComponent? direct)
                ? direct.GimbalDegrees : 225f;
            var center = Pixel(muzzle);
            Vector2? previous = null;
            for (var i = 0; i <= 45; i++)
            {
                var end = Pixel(muzzle + (rotation + Angle.FromDegrees(-arc / 2 + i * arc / 45))
                    .RotateVec(Vector2.UnitY * chin.Range));
                if (previous is { } from)
                    handle.DrawLine(from, end, Color.Yellow);
                if (i is 0 or 45)
                    handle.DrawLine(center, end, Color.Yellow);
                previous = end;
            }
            var mouse = _input.MouseScreenPosition.Position;
            var local = mouse - screen.Viewport.GlobalPixelPosition;
            var inView = local.X >= 0 && local.Y >= 0 &&
                         local.X < screen.Viewport.PixelWidth && local.Y < screen.Viewport.PixelHeight;
            var aim = terminal.TrackedTarget is { } target && entities.HasComponent<TransformComponent>(target)
                ? transform.GetMapCoordinates(target)
                : inView ? screen.Viewport.PixelToMap(mouse) : eye.Position;
            var delta = aim.Position - muzzle;
            var valid = aim.MapId == origin.MapId && delta.LengthSquared() > 0 &&
                        delta.LengthSquared() <= chin.Range * chin.Range &&
                        Vector2.Dot(rotation.RotateVec(Vector2.UnitY), Vector2.Normalize(delta)) >=
                        MathF.Cos(arc * MathF.PI / 360);
            Ring(aim.Position, 0.45f, !valid || terminal.TrackingFireConfirmed ? Color.Red : Color.Lime);
            break;
        }
    }
}
