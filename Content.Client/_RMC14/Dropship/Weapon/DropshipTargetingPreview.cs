using System.Numerics;
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

namespace Content.Client._RMC14.Dropship.Weapon;

public sealed class DropshipTargetingPreview : Control
{
    private readonly IEntityManager _entities;
    private readonly EntityUid _console;
    private readonly bool _first;
    private readonly DropshipWeaponsScreen _screen;
    private readonly IInputManager _input = IoCManager.Resolve<IInputManager>();
    private float _aimElapsed;
    public event Action<MapCoordinates>? CursorAim;
    public event Action? CursorUnavailable;

    public DropshipTargetingPreview(IEntityManager entities, EntityUid console, bool first, DropshipWeaponsScreen screen)
    {
        _entities = entities;
        _console = console;
        _first = first;
        _screen = screen;
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
        RectClipContent = true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _aimElapsed += args.DeltaSeconds;
        if (!_first || _aimElapsed < 0.1f ||
            !_entities.TryGetComponent(_console, out DropshipTerminalWeaponsComponent? terminal) ||
            terminal.ChinGunOperator == null || terminal.TrackedTarget != null || terminal.ChinGunAr)
            return;
        _aimElapsed = 0;
        var viewport = _screen.Viewport;
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
        var viewport = _screen.Viewport;
        if (!_entities.TryGetComponent(_console, out DropshipTerminalWeaponsComponent? terminal) ||
            viewport.Eye is not { } eye)
            return;
        var transform = _entities.System<SharedTransformSystem>();
        var weapons = _entities.System<DropshipWeaponSystem>();
        var selection = _first ? terminal.ScreenOne : terminal.ScreenTwo;
        var shipSystem = _entities.System<SharedDropshipSystem>();
        EntityUid? selectedWeapon = _entities.GetEntity(selection.Weapon);
        var center = eye.Position;
        var cursor = _input.MouseScreenPosition.Position;
        var local = cursor - viewport.GlobalPixelPosition;
        var cursorInView = local.X >= 0 && local.Y >= 0 && local.X < viewport.PixelWidth && local.Y < viewport.PixelHeight;
        var color = Color.Yellow.WithAlpha(0.8f);

        Vector2 Pixel(Vector2 world) => viewport.WorldToScreen(world) - GlobalPixelPosition;
        void Ring(Vector2 origin, float radius, Color tint)
        {
            Vector2? previous = null;
            for (var i = 0; i <= 48; i++)
            {
                var next = Pixel(origin + Angle.FromDegrees(i * 360f / 48).RotateVec(Vector2.UnitX * radius));
                if (previous is { } from)
                    handle.DrawLine(from, next, tint);
                previous = next;
            }
        }

        if (_first && terminal.ChinGunAiTarget is { } aiTarget &&
            _entities.TryGetComponent(aiTarget, out TransformComponent? aiTransform))
        {
            var location = transform.GetMapCoordinates(aiTarget, aiTransform);
            if (location.MapId == center.MapId)
                Ring(location.Position, 0.65f, terminal.ChinGunAiApproved ? Color.Lime : Color.Yellow);
        }

        if (_first && terminal.ChinGunEye != null && terminal.ChinGunAirborne)
        {
            if (terminal.Target is { } signal && _entities.HasComponent<TransformComponent>(signal))
            {
                var anchor = transform.GetMapCoordinates(signal);
                Ring(anchor.Position, terminal.ChinGunFlareRadius, Color.Cyan);
                var cursorAim = cursorInView ? viewport.PixelToMap(cursor) : center;
                Ring(cursorAim.Position, 0.45f,
                    cursorAim.MapId == anchor.MapId && Vector2.DistanceSquared(cursorAim.Position, anchor.Position) <=
                    terminal.ChinGunFlareRadius * terminal.ChinGunFlareRadius ? Color.Lime : Color.Red);
                _screen.PayloadPreviewLabel.Text = Loc.GetString("cmu-mohawk-gunnery-airborne-preview");
            }
            else
                _screen.PayloadPreviewLabel.Text = Loc.GetString("cmu-mohawk-gunnery-airborne-flare-only");
            return;
        }

        if (_first && terminal.ChinGunEye != null && shipSystem.TryGetGridDropship(_console, out var ship))
        {
            foreach (var point in ship.Comp.AttachmentPoints)
            {
                if (!_entities.TryGetComponent(point, out MohawkChinGunComponent? chin) ||
                    !_entities.TryGetComponent(point, out GunshipDirectFirePointComponent? mount))
                    continue;
                var origin = transform.GetMapCoordinates(point);
                if (origin.MapId != center.MapId)
                    continue;
                var rotation = transform.GetWorldRotation(ship);
                var muzzle = origin.Position + rotation.RotateVec(new Vector2(mount.LateralOffset, mount.ForwardOffset));
                var arc = selectedWeapon is { } arcWeapon && _entities.TryGetComponent(arcWeapon, out GunshipDirectFireWeaponComponent? direct)
                    ? direct.GimbalDegrees : 225f;
                Vector2? previous = null;
                for (var i = 0; i <= 45; i++)
                {
                    var end = muzzle + (rotation + Angle.FromDegrees(-arc / 2 + i * arc / 45)).RotateVec(Vector2.UnitY * chin.Range);
                    var pixel = Pixel(end);
                    if (previous is { } from)
                        handle.DrawLine(from, pixel, color);
                    if (i is 0 or 45)
                        handle.DrawLine(Pixel(muzzle), pixel, color);
                    previous = pixel;
                }
                center = terminal.TrackedTarget is { } target && _entities.HasComponent<TransformComponent>(target)
                    ? transform.GetMapCoordinates(target)
                    : cursorInView ? viewport.PixelToMap(cursor) : center;
                var distanceToAim = Vector2.Distance(muzzle, center.Position);
                var validAim = center.MapId == origin.MapId && distanceToAim <= chin.Range &&
                    Vector2.Dot(rotation.RotateVec(Vector2.UnitY), Vector2.Normalize(center.Position - muzzle)) >=
                    MathF.Cos(arc * MathF.PI / 360);
                if (terminal.Target is { } flare && _entities.HasComponent<FlareSignalComponent>(flare))
                {
                    var anchor = transform.GetMapCoordinates(flare);
                    if (anchor.MapId == origin.MapId)
                        Ring(anchor.Position, chin.FlareRadius, Color.Cyan);
                    validAim &= _entities.System<MohawkChinGunSystem>().IsInFlareArea(
                        (point, chin), transform.ToCoordinates(center));
                }
                Ring(center.Position, 0.45f, !validAim || terminal.TrackingFireConfirmed ? Color.Red : Color.Lime);
                if (terminal.TrackedTarget is { } tracked)
                {
                    _screen.PayloadPreviewLabel.Text = Loc.GetString("cmu-dropship-track-range",
                        ("target", _entities.GetComponent<MetaDataComponent>(tracked).EntityName),
                        ("range", MathF.Round(distanceToAim, 1)),
                        ("status", Loc.GetString(validAim ? "cmu-dropship-aim-valid" : "cmu-dropship-aim-invalid")));
                }
                else
                    _screen.PayloadPreviewLabel.Text = Loc.GetString("cmu-dropship-aim-range",
                        ("range", MathF.Round(distanceToAim, 1)),
                        ("status", Loc.GetString(validAim ? "cmu-dropship-aim-valid" : "cmu-dropship-aim-invalid")));
                break;
            }
            return;
        }

        if (selectedWeapon is not { } weapon || !_entities.TryGetComponent(weapon, out DropshipWeaponComponent? component) ||
            !weapons.TryGetWeaponAmmo(weapon, out var ammo))
            return;
        if (terminal.Target is { } aim && _entities.HasComponent<TransformComponent>(aim))
            center = transform.GetMapCoordinates(aim).Offset(terminal.Offset);
        else if (cursorInView)
            center = viewport.PixelToMap(cursor);
        if (center.MapId != eye.Position.MapId)
            return;
        var explosion = SharedDropshipWeaponSystem.GetAirburstExplosion(ammo.Comp, component.AirburstHeight);
        var radius = explosion == null ? ammo.Comp.Fire?.Range ?? 0.49f
            : SharedDropshipWeaponSystem.EstimateBlastRadius(explosion);
        var rotationAngle = shipSystem.TryGetGridDropship(weapon, out var platform)
            ? transform.GetWorldRotation(platform) : Angle.Zero;
        if (ammo.Comp.ImpactsPerVolley > 1)
        {
            for (var i = 0; i < ammo.Comp.ImpactsPerVolley; i++)
            {
                var offset = SharedDropshipWeaponSystem.GetLaserImpactOffset(i, ammo.Comp.ImpactsPerVolley,
                    ammo.Comp.BulletSpread, rotationAngle, component.LaserPattern);
                Ring(center.Position + offset, radius, color);
                Ring(center.Position + offset, 0.2f, Color.Lime);
            }
        }
        else
        {
            Ring(center.Position, radius, color);
            if (ammo.Comp.BulletSpread + ammo.Comp.TargetSpread > 0)
                Ring(center.Position, radius + MathF.Sqrt(2) * (ammo.Comp.BulletSpread + ammo.Comp.TargetSpread),
                    Color.Orange.WithAlpha(0.5f));
        }
    }
}
