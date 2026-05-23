using System.Numerics;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._CMU14.ZLevels.Core;

/// <summary>
/// Only process Eye offset and drawdepth on clientside
/// </summary>
public sealed partial class CMUClientZLevelsSystem : CMUSharedZLevelsSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IConfigurationManager _config = default!;

    public static float ZLevelOffset = 0.7f;

    private CMUZLevelVisibleEntityOverlay? _visibleEntityOverlay;

    public CMUZLevelOpeningCache OpeningCache { get; } = new();

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new CMUZLevelBlurOverlay());
        _visibleEntityOverlay = new CMUZLevelVisibleEntityOverlay();
        _overlay.AddOverlay(_visibleEntityOverlay);

        SubscribeLocalEvent<CMUZPhysicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
        SubscribeLocalEvent<CMUZFallingComponent, ComponentShutdown>(OnFallingShutdown);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridShutdown);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void OnGridShutdown(GridRemovalEvent args)
    {
        OpeningCache.RemoveGrid(args.EntityUid);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        OpeningCache.InvalidateTiles(args.Entity, args.Changes);
    }

    private void OnEyeOffset(Entity<CMUZPhysicsComponent> ent, ref GetEyeOffsetEvent args)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled))
            return;

        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var offset = rotation.RotateVec(new Vector2(0, ent.Comp.LocalPosition * ZLevelOffset));
        args.Offset += offset;
    }

    private void OnFallingShutdown(Entity<CMUZFallingComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<CMUZPhysicsComponent>(ent, out var zPhys) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
        {
            return;
        }

        sprite.NoRotation = zPhys.NoRotDefault;
        _sprite.SetOffset((ent.Owner, sprite), zPhys.SpriteOffsetDefault);
        _sprite.SetDrawDepth((ent.Owner, sprite), zPhys.DrawDepthDefault);
    }

    private void OnStartup(Entity<CMUZPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (sprite.SnapCardinals)
            return;

        ent.Comp.NoRotDefault = sprite.NoRotation;
        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
    }

    public bool TryGetSpeechBubbleZOffset(
        EntityUid speaker,
        out Vector2 zPassOffset,
        TransformComponent? speakerXform = null)
    {
        zPassOffset = default;

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !_config.GetCVar(CMUZLevelsCVars.RenderEnabled))
        {
            return false;
        }

        if (speakerXform == null &&
            !TryComp(speaker, out speakerXform))
        {
            return false;
        }

        if (speakerXform.MapUid is not { } speakerMap)
            return false;

        if (speakerXform.MapID == _eye.CurrentEye.Position.MapId)
            return true;

        if (_player.LocalEntity is not { } player ||
            !TryComp<CMUZLevelViewerComponent>(player, out var viewer) ||
            !TryComp(player, out TransformComponent? playerXform) ||
            playerXform.MapUid is not { } playerMap ||
            !TryComp<CMUZLevelMapComponent>(playerMap, out var playerZMap) ||
            !TryComp<CMUZLevelMapComponent>(speakerMap, out var speakerZMap) ||
            speakerZMap.NetworkUid != playerZMap.NetworkUid)
        {
            return false;
        }

        var depthOffset = speakerZMap.Depth - playerZMap.Depth;
        if (depthOffset == 0)
            return true;

        if (depthOffset > 0)
        {
            if (depthOffset != 1 ||
                !viewer.LookUp && !viewer.StairPreviewUp)
            {
                return false;
            }
        }
        else
        {
            var maxDepth = Math.Clamp(
                _config.GetCVar(CMUZLevelsCVars.MaxRenderDepth),
                0,
                MaxZLevelsBelowRendering);

            if (-depthOffset > maxDepth)
                return false;
        }

        Angle rotation = _eye.CurrentEye.Rotation * -1;
        zPassOffset = rotation.ToWorldVec() * ZLevelOffset * depthOffset;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled))
            return;

        var query = EntityQueryEnumerator<CMUZFallingComponent, CMUZPhysicsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var _, out var zPhys, out var sprite))
        {
            if (zPhys.LocalPosition != 0)
                sprite.NoRotation = true;
            else
                sprite.NoRotation = zPhys.NoRotDefault;

            _sprite.SetOffset((uid, sprite), zPhys.SpriteOffsetDefault + new Vector2(0, zPhys.LocalPosition * ZLevelOffset));
            _sprite.SetDrawDepth((uid, sprite), zPhys.LocalPosition > 0 ? (int)Shared.DrawDepth.DrawDepth.OverMobs : zPhys.DrawDepthDefault);
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CMUZLevelBlurOverlay>();

        if (_visibleEntityOverlay is not null && _overlay.HasOverlay<CMUZLevelVisibleEntityOverlay>())
            _overlay.RemoveOverlay(_visibleEntityOverlay);
    }
}
