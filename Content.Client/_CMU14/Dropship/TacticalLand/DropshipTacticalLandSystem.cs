using System.Numerics;
using Content.Client.Eye;
using Content.Client.Movement.Systems;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Dropship.TacticalLand;

public sealed partial class DropshipTacticalLandSystem : SharedDropshipTacticalLandSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private static readonly Vector2 TacticalLandZoom = new(2.25f, 2.25f);
    private static readonly TimeSpan MobVisibilityRefreshInterval = TimeSpan.FromMilliseconds(100);

    private bool _mobsHidden;
    private readonly HashSet<EntityUid> _hiddenMobs = new();
    private readonly HashSet<Entity<MobStateComponent>> _viewportMobs = new();
    private TimeSpan _nextMobVisibilityRefresh;

    private bool _zoomApplied;
    private EntityUid? _zoomedEntity;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(EyeLerpingSystem));
        _overlay.AddOverlay(new DropshipTacticalLandOverlay(EntityManager, _player));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<DropshipTacticalLandOverlay>();
        if (_mobsHidden)
            RestoreMobs();
        if (_zoomApplied)
            RestoreZoom();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var inEye = IsLocalPlayerInPilotEye();

        if (inEye)
        {
            if (_timing.CurTime >= _nextMobVisibilityRefresh)
            {
                _nextMobVisibilityRefresh = _timing.CurTime + MobVisibilityRefreshInterval;
                HideMobsTick();
            }
            _mobsHidden = true;
            ApplyZoom();
        }
        else
        {
            if (_mobsHidden)
            {
                RestoreMobs();
                _mobsHidden = false;
            }
            if (_zoomApplied)
                RestoreZoom();
        }
    }

    private void ApplyZoom()
    {
        if (_player.LocalEntity is not { } local)
            return;

        if (_zoomApplied && _zoomedEntity is { } prev && prev != local)
            RestoreZoom();

        _zoomedEntity = local;
        _eyeManager.CurrentEye.Zoom = TacticalLandZoom;
        _zoomApplied = true;
    }

    private void RestoreZoom()
    {
        if (_zoomedEntity is { } prev &&
            !TerminatingOrDeleted(prev) &&
            TryComp(prev, out ContentEyeComponent? content))
        {
            _eyeManager.CurrentEye.Zoom = content.TargetZoom;
        }

        _zoomApplied = false;
        _zoomedEntity = null;
    }

    private bool IsLocalPlayerInPilotEye()
    {
        if (_player.LocalEntity is not { } local)
            return false;

        if (!TryComp(local, out EyeComponent? eye) || eye.Target is not { } target)
            return false;

        return HasComp<DropshipPilotEyeComponent>(target);
    }

    private void HideMobsTick()
    {
        var eye = _eyeManager.CurrentEye;
        if (eye.Position.MapId == MapId.Nullspace)
            return;

        _viewportMobs.Clear();
        _lookup.GetEntitiesIntersecting(eye.Position.MapId, _eyeManager.GetWorldViewbounds(), _viewportMobs);
        foreach (var (uid, _) in _viewportMobs)
        {
            if (!TryComp(uid, out SpriteComponent? sprite))
                continue;

            if (!sprite.Visible)
                continue;
            _sprite.SetVisible((uid, sprite), false);
            _hiddenMobs.Add(uid);
        }
    }

    private void RestoreMobs()
    {
        foreach (var uid in _hiddenMobs)
        {
            if (TryComp(uid, out SpriteComponent? sprite))
                _sprite.SetVisible((uid, sprite), true);
        }
        _hiddenMobs.Clear();
    }
}
