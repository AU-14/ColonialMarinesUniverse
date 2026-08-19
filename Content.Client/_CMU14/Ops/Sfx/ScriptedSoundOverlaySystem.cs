using Content.Shared._CMU14.Ops.Sfx;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Evacuation;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Ops.Sfx;

public sealed partial class ScriptedSoundOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _plyMan = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private readonly List<(ExplosionFlashOverlay Overlay, TimeSpan Expires)> _flashes = new();
    private AlarmOverlay? _alarm;
    private EntityUid? _alarmMap;
    private const float DefaultAlarmFrequency = 2f;

    public override void Initialize()
    {
        SubscribeNetworkEvent<ScriptedSequenceMarkerNetEvent>(OnMarker);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => Cleanup());
        SubscribeLocalEvent<EvacuationProgressComponent, AfterAutoHandleStateEvent>(OnEvacuationHandleState);
    }

    private void OnEvacuationHandleState(EntityUid uid, EvacuationProgressComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (comp.SelfDestructAt == null || comp.SelfDestructed || !comp.Enabled)
        {
            if (_alarmMap == uid)
            {
                _alarmMap = null;
                DisableAlarm();
            }
            return;
        }

        if (_alarm != null)
            return;

        var player = _plyMan.LocalEntity;
        if (player == null)
            return;

        var playerMap = Transform(player.Value).MapUid;
        if (playerMap == null)
            return;

        if (!PlayerShouldReceiveMarker(uid, playerMap.Value))
            return;

        _alarmMap = uid;
        EnableAlarm(DefaultAlarmFrequency);
    }

    public override void Update(float frameTime)
    {
        UpdateAlarm();

        for (var i = _flashes.Count - 1; i >= 0; i--)
        {
            var (overlay, expires) = _flashes[i];
            if (_timing.CurTime < expires) continue;
            _overlay.RemoveOverlay(overlay);
            _flashes.RemoveAt(i);
        }
    }

    private void OnMarker(ScriptedSequenceMarkerNetEvent ev)
    {
        if (ev.Marker == ScriptedSoundMarkers.SequenceStopped)
        {
            DisableAlarm();
            return;
        }

        var player = _plyMan.LocalEntity;
        if (player == null || ev.AnchorCoords is null)
            return;

        var anchorCoords = EntityManager.GetCoordinates(ev.AnchorCoords.Value);
        var anchorMapEntity = anchorCoords.GetMapUid(EntityManager);
        var playerMapUid = Transform(player.Value).MapUid;
        if (anchorMapEntity == null || playerMapUid == null)
            return;

        if (!PlayerShouldReceiveMarker(anchorMapEntity.Value, playerMapUid.Value))
            return;

        switch (ev.Marker)
        {
            case ScriptedSoundMarkers.RedAlarmOn:
                EnableAlarm(ev.MarkerData?.Frequency ?? DefaultAlarmFrequency);
                break;
            case ScriptedSoundMarkers.RedAlarmOff:
                DisableAlarm();
                break;
            case ScriptedSoundMarkers.Explode:
                if (!_config.GetCVar(CCVars.ReducedMotion))
                    TriggerWhiteout(ev.MarkerData?.FlashDuration ?? 0.25f, ev.MarkerData?.Color);
                break;
        }
    }

    private void EnableAlarm(float frequency)
    {
        if (_alarm != null)
        {
            _alarm.Frequency = frequency;
            return;
        }

        _alarm = new AlarmOverlay(frequency);
        _overlay.AddOverlay(_alarm);
    }

    private void DisableAlarm()
    {
        if (_alarm == null) return;
        _overlay.RemoveOverlay(_alarm);
        _alarm = null;
    }

    private bool PlayerShouldReceiveMarker(EntityUid anchorMap, EntityUid playerMap)
    {
        if (anchorMap == playerMap)
            return true;

        return _zLevels.TryGetZNetwork(anchorMap, out var network) &&
               _zLevels.IsMapInNetwork(network.Value, playerMap);
    }

    private void Cleanup()
    {
        _alarmMap = null;

        if (_alarm != null)
        {
            _overlay.RemoveOverlay(_alarm);
            _alarm = null;
        }

        foreach (var (flash, _) in _flashes)
            _overlay.RemoveOverlay(flash);
        _flashes.Clear();
    }

    private void TriggerWhiteout(float duration = 0.25f, Color? color = null)
    {
        var flash = new ExplosionFlashOverlay(color);
        _overlay.AddOverlay(flash);
        _flashes.Add((flash, _timing.CurTime + TimeSpan.FromSeconds(duration)));
    }

    private void UpdateAlarm()
    {
        if (_alarm == null)
            return;

        if (_config.GetCVar(CCVars.ReducedMotion))
        {
            _alarm.StaticMode = true;
            return;
        }

        _alarm.StaticMode = false;
        _alarm.Phase = (float)_timing.CurTime.TotalSeconds;
    }
}

public sealed partial class AlarmOverlay(float frequency = 2f) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;
    public float Frequency { get; set; } = frequency;
    public float Phase { get; set; }
    public bool StaticMode;

    private const float PulseFloor = 0.08f;
    private const float PulseRange = 0.35f;
    private const float StaticAlpha = 0.2f;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var bounds = args.ViewportBounds;
        var handle = args.ScreenHandle;
        var alpha = StaticMode
            ? StaticAlpha
            : MathF.Abs(MathF.Sin(Phase * Frequency)) * PulseRange + PulseFloor;
        var color = new Color(1f, 0.02f, 0.02f, alpha);
        const int thickness = 12;

        handle.DrawRect(new UIBox2i(bounds.Left, bounds.Top, bounds.Right, bounds.Top + thickness), color);
        handle.DrawRect(new UIBox2i(bounds.Left, bounds.Bottom - thickness, bounds.Right, bounds.Bottom), color);
        handle.DrawRect(new UIBox2i(bounds.Left, bounds.Top + thickness, bounds.Left + thickness, bounds.Bottom - thickness), color);
        handle.DrawRect(new UIBox2i(bounds.Right - thickness, bounds.Top + thickness, bounds.Right, bounds.Bottom - thickness), color);
    }
}

public sealed partial class ExplosionFlashOverlay(Color? color = null) : Overlay
{
    private readonly Color _color = (color ?? Color.White).WithAlpha(0.8f);
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    protected override void Draw(in OverlayDrawArgs args)
        => args.ScreenHandle.DrawRect(args.ViewportBounds, _color);
}
