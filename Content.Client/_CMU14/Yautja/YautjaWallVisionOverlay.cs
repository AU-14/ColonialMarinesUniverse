using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaWallVisionOverlay : Overlay
{
    private readonly IEntityManager _entity;
    private readonly IPlayerManager _players;
    private readonly ContainerSystem _container;
    private readonly EntityLookupSystem _lookup;
    private readonly InventorySystem _inventory;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public YautjaWallVisionOverlay(IEntityManager entity, IPlayerManager players)
    {
        _entity = entity;
        _players = players;
        _container = entity.System<ContainerSystem>();
        _lookup = entity.System<EntityLookupSystem>();
        _inventory = entity.System<InventorySystem>();
        _sprite = entity.System<SpriteSystem>();
        _transform = entity.System<TransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_players.LocalEntity is not { } viewer || !HasActiveThermalVisor(viewer))
            return;

        _mobs.Clear();
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldAABB, _mobs, LookupFlags.Uncontained);

        var handle = args.WorldHandle;
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;

        foreach (var (target, _) in _mobs)
        {
            if (!_entity.TryGetComponent(target, out SpriteComponent? sprite) ||
                !_entity.TryGetComponent(target, out TransformComponent? xform))
            {
                continue;
            }

            var inContainer = _container.IsEntityOrParentInContainer(target, xform: xform);
            if (!YautjaWallVisionTargeting.IsEligible(
                    viewer,
                    target,
                    args.MapId,
                    xform.MapID,
                    targetIsMob: true,
                    sprite.Visible,
                    inContainer,
                    thermalVisionEnabled: true))
            {
                continue;
            }

            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            _sprite.RenderSprite((target, sprite), handle, eyeRotation, rotation, position);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private bool HasActiveThermalVisor(EntityUid viewer)
    {
        if (!_inventory.TryGetSlotEntity(viewer, "eyes", out var glasses) ||
            glasses is not { } glassesUid ||
            !_entity.TryGetComponent(glassesUid, out YautjaMaskVisorGlassesComponent? visor) ||
            visor.Mask is not { } maskUid ||
            !_entity.TryGetComponent(maskUid, out YautjaMaskComponent? mask))
        {
            return false;
        }

        return YautjaWallVisionTargeting.IsActiveSource(
            visorIsEquipped: true,
            thermalVisionEnabled: visor.ThermalVisionEnabled,
            visorOwnedByViewer: visor.User == viewer,
            visorLinkedToMask: true,
            maskVisorEnabled: mask.VisorEnabled);
    }
}
