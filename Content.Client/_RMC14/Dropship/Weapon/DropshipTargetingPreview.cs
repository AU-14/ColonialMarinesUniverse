using System.Numerics;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.Dropship.Weapon;

public sealed class DropshipTargetingPreview : Control
{
    private readonly IEntityManager _entities;
    private readonly EntityUid _console;
    private readonly bool _first;
    private readonly DropshipWeaponsScreen _screen;
    private readonly IInputManager _input = IoCManager.Resolve<IInputManager>();

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
