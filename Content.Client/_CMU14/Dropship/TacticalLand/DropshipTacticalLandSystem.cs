using System.Numerics;
using Content.Client.Movement.Systems;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Dropship.TacticalLand;

public sealed class DropshipTacticalLandSystem : SharedDropshipTacticalLandSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ContentEyeSystem _contentEye = default!;

    private static readonly Vector2 TacticalLandZoom = new(2.25f, 2.25f);

    private bool _mobsHidden;
    private readonly HashSet<EntityUid> _hiddenMobs = new();

    private bool _zoomApplied;
    private EntityUid? _zoomedEntity;
    private Vector2 _originalZoom = Vector2.One;

    public override void Initialize()
    {
        base.Initialize();
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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var inEye = IsLocalPlayerInPilotEye();

        if (inEye)
        {
            HideMobsTick();
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
        // RequestZoom raises a predictive event; valid only during the first-time prediction pass.
        if (!_timing.IsFirstTimePredicted)
            return;

        if (_player.LocalEntity is not { } local)
            return;

        if (!TryComp(local, out ContentEyeComponent? content))
            return;

        if (_zoomApplied && _zoomedEntity == local)
            return;

        if (_zoomApplied && _zoomedEntity is { } prev && prev != local)
            RestoreZoom();

        _originalZoom = content.TargetZoom;
        _zoomedEntity = local;
        _contentEye.RequestZoom(local, TacticalLandZoom, ignoreLimit: true, scalePvs: true, content);
        _zoomApplied = true;
    }

    private void RestoreZoom()
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (_zoomedEntity is { } prev && !TerminatingOrDeleted(prev) && TryComp(prev, out ContentEyeComponent? content))
        {
            _contentEye.RequestZoom(prev, _originalZoom, ignoreLimit: true, scalePvs: true, content);
        }

        _zoomApplied = false;
        _zoomedEntity = null;
        _originalZoom = Vector2.One;
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
        var query = EntityQueryEnumerator<MobStateComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
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
