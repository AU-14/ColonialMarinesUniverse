using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.ParaDrop;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterGroundVisualSystem : EntitySystem
{
    private static readonly Color WreckColor = Color.FromHex("#918579");
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    private readonly Dictionary<EntityUid, CrewSpriteState> _crewSprites = [];
    private readonly HashSet<EntityUid> _visibleCrew = [];
    private readonly List<EntityUid> _restoreCrew = [];

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(FighterClientSystem));
        InitializeCrewMask();
        _overlays.AddOverlay(new FighterVtolFloorOverlay(EntityManager));
        _overlays.AddOverlay(new FighterVtolHeatOverlay(EntityManager) { ZIndex = -10 });
    }

    public override void Shutdown()
    {
        _visibleCrew.Clear();
        RestoreCrewSprites();
        _overlays.RemoveOverlay<FighterVtolFloorOverlay>();
        _overlays.RemoveOverlay<FighterVtolHeatOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        var flybys = EntityQueryEnumerator<FighterFlybyComponent, SpriteComponent>();
        while (flybys.MoveNext(out var uid, out var flyby, out _))
        {
            var altitude = Math.Clamp((flyby.Height - FighterFlight.MinimumHeight) /
                (FighterFlight.MaximumHeight - FighterFlight.MinimumHeight), 0, 1);
            _sprites.SetColor(uid, flyby.Crashing ? Color.FromHex("#88776A") : Color.Black.WithAlpha(.32f - altitude * .25f));
            _sprites.SetScale(uid, new Vector2((.5f + altitude * .25f) * FighterGroundComponent.SizeMultiplier));
            // Keep the south-facing source art aligned with the north-facing flight transform.
            _sprites.SetRotation(uid, new Angle(Math.PI + (flyby.Crashing ? Math.Sin(_timing.CurTime.TotalSeconds * 8) * .22 : 0)));
        }
        _visibleCrew.Clear();
        var query = EntityQueryEnumerator<FighterGroundComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var ground, out _))
        {
            _sprites.LayerSetRsiState(uid, 0, ground.State == FighterGroundState.Grounded ? "folded" : "vtolmode");
            FighterVtolPresentation.Hull(ground, _timing.CurTime, out var offset, out var scale, out var opacity);
            offset += GroundElevationOffset(uid);
            _sprites.SetOffset(uid, offset);
            _sprites.SetScale(uid, new Vector2(FighterGroundComponent.SpriteScale * scale));
            var color = ground.State == FighterGroundState.Crashed ? WreckColor : Color.White.WithAlpha(opacity);
            _sprites.SetColor(uid, color);
            foreach (var part in new[] { ground.FrontSeat, ground.RearSeat, ground.Canopy })
            {
                if (part is not { } entity || !TryComp(entity, out SpriteComponent? partSprite) || Transform(entity).ParentUid != uid) continue;
                _sprites.SetOffset(entity, AttachmentOffset(uid, (entity, partSprite), offset, scale));
                _sprites.SetScale(entity, new Vector2(FighterGroundComponent.SpriteScale * scale));
                _sprites.SetColor(entity, color);
                if (part == ground.Canopy)
                    _sprites.LayerSetRsiState(entity, 0, ground.State == FighterGroundState.Grounded ? "folded" : "vtolmode");
                if (TryComp(entity, out FighterSeatComponent? seat) && seat.Occupant is { } crew && TryComp(crew, out SpriteComponent? sprite))
                {
                    _visibleCrew.Add(crew);
                    if (!_crewSprites.TryGetValue(crew, out var original))
                        _crewSprites.Add(crew, original = new CrewSpriteState(sprite, AddCrewMask(sprite)));
                    // A seated body faces along the cockpit, including between
                    // cardinal headings. Upright walking sprites would otherwise
                    // put the head outside the opening when the plane turns.
                    sprite.NoRotation = true;
                    sprite.EnableDirectionOverride = true;
                    sprite.DirectionOverride = Direction.South;
                    _sprites.SetRotation(crew, _transform.GetWorldRotation(entity) + _eye.CurrentEye.Rotation);
                    _sprites.SetScale(crew, original.Scale * scale);
                    _sprites.SetOffset(crew, original.Offset * scale + AttachmentOffset(uid, (crew, sprite), offset, scale));
                    _sprites.SetColor(crew, original.Color.WithAlpha(original.Color.A * opacity));
                }
            }
        }
        var aircrafts = EntityQueryEnumerator<FighterAircraftComponent>();
        while (aircrafts.MoveNext(out _, out var aircraft))
            foreach (var part in new[] { aircraft.FrontSeat, aircraft.RearSeat, aircraft.Canopy })
                if (part is { } uid && TryComp(uid, out SpriteComponent? _) && !HasComp<FighterGroundComponent>(Transform(uid).ParentUid))
                {
                    _sprites.SetScale(uid, new Vector2(2));
                    _sprites.SetOffset(uid, Vector2.Zero);
                    _sprites.SetColor(uid, Color.White);
                    if (part == aircraft.Canopy)
                        _sprites.LayerSetRsiState(uid, 0, "jetfighter");
                }
        RestoreCrewSprites();
        var mounts = EntityQueryEnumerator<FighterHardpointComponent, SpriteComponent, TransformComponent>();
        while (mounts.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            var onGround = TryComp(xform.ParentUid, out FighterGroundComponent? ground);
            var offset = Vector2.Zero;
            var scale = 1f;
            var opacity = 1f;
            if (onGround)
            {
                FighterVtolPresentation.Hull(ground!, _timing.CurTime, out offset, out scale, out opacity);
                offset += GroundElevationOffset(xform.ParentUid);
                offset = AttachmentOffset(xform.ParentUid, (uid, sprite), offset, scale);
            }
            _sprites.SetScale(uid, new Vector2((onGround ? .6f * FighterGroundComponent.AttachmentScale : .6f) * scale));
            _sprites.SetOffset(uid, offset);
            _sprites.SetColor(uid, ground?.State == FighterGroundState.Crashed ? WreckColor : Color.White.WithAlpha(opacity));
        }
    }

    private Vector2 GroundElevationOffset(EntityUid hull)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) || !TryComp(hull, out CMUZPhysicsComponent? physics))
            return Vector2.Zero;

        // Keep the hull, canopy, seats and click map on the same presentation.
        // The generic Z pass temporarily forces NoRotation and moves only the
        // hull, which separates a turning aircraft from its attached cockpit.
        return (-_transform.GetWorldRotation(hull)).RotateVec(
            new Vector2(0, physics.LocalPosition * CMUSharedZLevelsSystem.ZLevelVisualOffset));
    }

    private void RestoreCrewSprites()
    {
        _restoreCrew.Clear();
        foreach (var (crew, original) in _crewSprites)
        {
            if (_visibleCrew.Contains(crew)) continue;
            if (TryComp(crew, out SpriteComponent? sprite))
            {
                // A low-altitude ejection may start its animation before this
                // frame sees the unbuckle. Restore the full-size landing scale.
                if (TryComp(crew, out SkyFallingComponent? falling)) falling.OriginalScale = original.Scale;
                else _sprites.SetScale(crew, original.Scale);
                _sprites.SetOffset(crew, original.Offset);
                _sprites.SetColor(crew, original.Color);
                _sprites.SetRotation(crew, original.Rotation);
                sprite.NoRotation = original.NoRotation;
                sprite.EnableDirectionOverride = original.EnableDirectionOverride;
                sprite.DirectionOverride = original.DirectionOverride;
                _sprites.RemovePostShader(sprite, CrewMaskId);
            }
            original.Mask.Dispose();
            _restoreCrew.Add(crew);
        }
        foreach (var crew in _restoreCrew) _crewSprites.Remove(crew);
    }

    private Vector2 AttachmentOffset(EntityUid hull, Entity<SpriteComponent> part, Vector2 lift, float scale)
    {
        // All attachments share the hull's animated socket in world space. Seats
        // rotate with their transform, but upright crew sprites use camera space.
        var socket = _transform.GetWorldPosition(part) - _transform.GetWorldPosition(hull);
        var worldOffset = _transform.GetWorldRotation(hull).RotateVec(lift) + socket * (scale - 1);
        var rotation = part.Comp.NoRotation ? _eye.CurrentEye.Rotation : -_transform.GetWorldRotation(part);
        return rotation.RotateVec(worldOffset);
    }
}
