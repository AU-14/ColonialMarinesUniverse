using System.Numerics;
using Content.Client._RMC14.Buckle;
using Content.Client._RMC14.Sprite;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Sprite;
using Content.Shared._RMC14.Weapons.Ranged.Overheat;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.CombatMode;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Dropship.AttachmentPoint;

public sealed partial class CMUDoorGunVisualSystem : EntitySystem
{
    [Dependency] private CMUDoorGunAimSystem _aim = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private RMCSpriteSystem _rmcSprites = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityUid? _localMount;
    private Angle _localAim;
    private TimeSpan _nextSend;

    public override void Initialize()
    {
        InitializeBarrelLight();
        SubscribeLocalEvent<BuckleComponent, GetDrawDepthEvent>(OnOperatorDrawDepth,
            after: [typeof(RMCBuckleVisualsSystem)]);
    }

    private void OnOperatorDrawDepth(Entity<BuckleComponent> ent, ref GetDrawDepthEvent args)
    {
        if (ent.Comp.BuckledTo is not { } mount
            || !HasComp<CMUDoorGunVisualComponent>(mount)
            || !TryComp(mount, out SpriteComponent? sprite))
            return;

        args.DrawDepth = (Content.Shared.DrawDepth.DrawDepth) (sprite.DrawDepth - 1);
    }

    public override void FrameUpdate(float frameTime)
    {
        EntityUid? localMount = null;
        if (_player.LocalEntity is { } user && _aim.TryGetMount(user, out var localGun, out var localVisual))
        {
            localMount = localGun;
            if (_localMount != localGun)
                _localAim = localVisual.AimOffset;

            var cursor = _eye.PixelToMap(_input.MouseScreenPosition);
            if (cursor.MapId != MapId.Nullspace && cursor.MapId == Transform(localGun).MapID)
            {
                var coordinates = _transform.ToCoordinates(cursor);
                if (_aim.TryGetAimOffset(localGun, coordinates, out var offset))
                {
                    _localAim = offset;
                    if (_timing.CurTime >= _nextSend)
                    {
                        _nextSend = _timing.CurTime + TimeSpan.FromMilliseconds(100);
                        RaiseNetworkEvent(new CMUDoorGunAimEvent(GetNetCoordinates(coordinates)));
                    }
                }
            }
        }
        _localMount = localMount;

        var query = EntityQueryEnumerator<CMUDoorGunVisualComponent, WeaponMountComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var visual, out var mount, out var sprite))
        {
            var aim = localMount == uid ? _localAim :
                _combat.IsInCombatMode(visual.Operator) ? visual.AimOffset : Angle.Zero;
            UpdateBarrelLight(uid, visual, mount, aim);
            var mountScreenAngle = _transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation;
            var frame = GetGunFrame(mountScreenAngle, aim);
            var gunScreenAngle = mountScreenAngle + aim;
            Vector2? frameOffset = visual.NorthSouthGunOffset == Vector2.Zero ? null :
                frame.Direction is Direction.North or Direction.South ? visual.NorthSouthGunOffset : Vector2.Zero;
            sprite.EnableDirectionOverride = true;
            sprite.DirectionOverride = frame.Direction;
            if (TryComp(uid, out StrapComponent? strap))
            {
                // Keep operators below the mount even if its visualizer refreshes the gun's draw depth.
                foreach (var buckled in strap.BuckledEntities)
                    _rmcSprites.UpdateDrawDepth(buckled);
            }
            AimLayer((uid, sprite), WeaponMountComponentVisualLayers.Mounted,
                frame.Rotation, gunScreenAngle, frameOffset);
            AimLayer((uid, sprite), WeaponMountComponentVisualLayers.MountedAmmo,
                frame.Rotation, gunScreenAngle, frameOffset);
            AimLayer((uid, sprite), WeaponMountComponentVisualLayers.Overheated,
                frame.Rotation, gunScreenAngle, frameOffset);

            if (_sprites.LayerMapTryGet((uid, sprite), WeaponMountComponentVisualLayers.Overheated,
                    out var heatLayer, false)
                && TryComp(mount.MountedEntity, out OverheatComponent? overheat) && overheat.MaxHeat > 0)
            {
                var alpha = Math.Clamp(overheat.Heat / overheat.MaxHeat, 0f, 1f);
                _sprites.LayerSetColor((uid, sprite), heatLayer, new Color(1f, 0.25f, 0.05f, alpha));
            }
        }
    }

    private void AimLayer(Entity<SpriteComponent?> sprite, WeaponMountComponentVisualLayers key,
        Angle directionalRotation, Angle screenAngle, Vector2? frameOffset)
    {
        if (!_sprites.LayerMapTryGet(sprite, key, out var layer, false))
            return;

        var directional = _sprites.LayerGetDirections(sprite, layer) == RsiDirectionType.Dir4;
        // Keep the door-facing frame while aiming; only the gun layer's rotation changes.
        // Single-direction custom artwork can still opt into the original east-facing convention.
        var rotation = directional
            ? directionalRotation
            : screenAngle - Angle.FromDegrees(90);
        _sprites.LayerSetRenderingStrategy(sprite, layer, LayerRenderingStrategy.NoRotation);
        _sprites.LayerSetDirOffset(sprite, layer, SpriteComponent.DirectionOffset.None);
        _sprites.LayerSetRotation(sprite, layer, rotation);
        if (frameOffset is { } offset)
            _sprites.LayerSetOffset(sprite, layer, offset);
    }

    internal static (Direction Direction, Angle Rotation) GetGunFrame(Angle mountScreenAngle, Angle aim)
    {
        var direction = mountScreenAngle.GetCardinalDir();
        return (direction, Angle.ShortestDistance(direction.ToAngle(), mountScreenAngle + aim));
    }
}
