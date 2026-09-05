using System.Numerics;
using Content.Client.CMU14.ZLevels.Culling;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.CMU14.ZLevels.Core;

/// <summary>
/// Applies client eye offsets and renders Z elevation without retaining sprite presentation changes.
/// </summary>
public sealed partial class CMUClientZLevelsSystem : CMUSharedZLevelsSystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private CMUZLevelSpriteCullingSystem _culling = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SpriteTreeSystem _spriteTree = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public static float ZLevelOffset = CMUSharedZLevelsSystem.ZLevelVisualOffset;

    private CMUZLevelVisibleEntityOverlay? _visibleEntityOverlay;

    public CMUZLevelOpeningCache OpeningCache { get; } = new();

    public override void Initialize()
    {
        base.Initialize();

        InitializePresentation();

        _overlay.AddOverlay(new CMUZLevelBlurOverlay());
        _visibleEntityOverlay = new CMUZLevelVisibleEntityOverlay();
        _overlay.AddOverlay(_visibleEntityOverlay);

        SubscribeLocalEvent<CMUZPhysicsComponent, MoveEvent>(OnZPhysicsMoveGroundSnap);
        SubscribeLocalEvent<CMUZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridShutdown);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void OnGridShutdown(GridRemovalEvent args)
    {
        InvalidateSharedOpeningCache(args.EntityUid);
        OpeningCache.RemoveGrid(args.EntityUid);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        InvalidateSharedOpeningCache(ref args);
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

    public override void Shutdown()
    {
        base.Shutdown();
        _presentationCandidates.Clear();
        _overlay.RemoveOverlay<CMUZLevelBlurOverlay>();

        if (_visibleEntityOverlay is not null && _overlay.HasOverlay<CMUZLevelVisibleEntityOverlay>())
            _overlay.RemoveOverlay(_visibleEntityOverlay);
    }
}
