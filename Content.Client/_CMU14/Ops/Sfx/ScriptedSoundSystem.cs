using Content.Shared._CMU14.Ops.Sfx;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Client.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Ops.Sfx;

public sealed partial class ScriptedSoundSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _plyMan = default!;

    private bool _muted;
    private EntityUid? _lastResyncMap;
    private readonly Dictionary<(int Handle, string Layer), EntityUid> _loops = new();
    private readonly List<(EntityUid Entity, TimeSpan StopAt)> _scheduledStops = new();

    public override void Initialize()
    {
        SubscribeNetworkEvent<PlayScriptedSoundNetEvent>(OnPlaySound);
        SubscribeNetworkEvent<StopScriptedSoundLayersNetEvent>(OnStopLayers);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => StopAll());
        Subs.CVar(_cfg, CCVars.MuteScriptedSounds, OnMuteChanged, true);
    }

    public override void Update(float frameTime)
    {
        var map = _plyMan.LocalEntity is { } player ? Transform(player).MapUid : null;
        if (map != _lastResyncMap)
        {
            _lastResyncMap = map;
            if (map != null)
                RaiseNetworkEvent(new RequestScriptedSoundResyncNetEvent());
        }

        for (var i = _scheduledStops.Count - 1; i >= 0; i--)
        {
            var (entity, stopAt) = _scheduledStops[i];
            if (_timing.CurTime < stopAt)
                continue;

            _audio.Stop(entity);
            _scheduledStops.RemoveAt(i);
        }
    }

    private void OnMuteChanged(bool muted)
    {
        var wasMuted = _muted;
        _muted = muted;
        if (muted)
        {
            StopAll();
            return;
        }

        if (wasMuted)
            RaiseNetworkEvent(new RequestScriptedSoundResyncNetEvent());
    }

    private void OnPlaySound(PlayScriptedSoundNetEvent ev)
    {
        if (_muted)
            return;

        var played = ev.Global
            ? _audio.PlayGlobal(ev.Sound, Filter.Local(), false, ev.Params)
            : ev.AnchorCoords is { } coords
                ? _audio.PlayPvs(ev.Sound, EntityManager.GetCoordinates(coords), ev.Params)
                : null;

        if (played is not { } stream)
            return;

        if (ev.Layer != null)
            ReplaceLoop(ev.Handle, ev.Layer, stream.Entity);

        if (ev.DurationSeconds is { } dur)
            _scheduledStops.Add((stream.Entity, _timing.CurTime + TimeSpan.FromSeconds(dur)));
    }

    private void ReplaceLoop(int handle, string layer, EntityUid entity)
    {
        var key = (handle, layer);
        if (_loops.Remove(key, out var old))
            _audio.Stop(old);
        _loops[key] = entity;
    }

    private void OnStopLayers(StopScriptedSoundLayersNetEvent ev)
    {
        if (ev.Layers == null)
        {
            RemoveSequence(ev.Handle);
            return;
        }

        foreach (var layer in ev.Layers)
        {
            if (_loops.Remove((ev.Handle, layer), out var stream))
                _audio.Stop(stream);
        }
    }

    private void RemoveSequence(int handle)
    {
        List<(int Handle, string Layer)>? dead = null;
        foreach (var key in _loops.Keys)
        {
            if (key.Handle == handle)
                (dead ??= new List<(int Handle, string Layer)>()).Add(key);
        }

        if (dead == null)
            return;

        foreach (var key in dead)
        {
            if (_loops.Remove(key, out var stream))
                _audio.Stop(stream);
        }
    }

    private void StopAll()
    {
        foreach (var (_, stream) in _loops)
            _audio.Stop(stream);
        _loops.Clear();

        foreach (var (entity, _) in _scheduledStops)
            _audio.Stop(entity);
        _scheduledStops.Clear();
    }
}
