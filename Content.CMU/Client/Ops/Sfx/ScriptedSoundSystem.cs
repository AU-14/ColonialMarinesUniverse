using Content.Shared.CMU14.Ops.Sfx;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Client.GameStates;
using Robust.Client.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Ops.Sfx;

public sealed partial class ScriptedSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClientGameStateManager _gameStates = default!;
    [Dependency] private IPlayerManager _plyMan = default!;
    [Dependency] private IGameTiming _timing = default!;

    private bool _muted;
    private bool _resyncPending;
    private EntityUid? _lastResyncMap;
    private readonly Dictionary<(int Handle, string Layer), EntityUid> _loops = new();
    private readonly List<(EntityUid Entity, TimeSpan StopAt)> _scheduledStops = new();

    // Hard ceiling on concurrent scripted sounds. Legit playback peaks around a
    // dozen loop layers; anything past this is a flood, and scripted sound must
    // never be the system that pushes an exhausted OpenAL source budget over.
    private readonly List<EntityUid> _activePlays = new();
    private const int MaxConcurrentPlays = 48;

    // Cap episode bookkeeping: warn once going into a flood, summarize coming out.
    private int? _suppressedPlays;

    public override void Initialize()
    {
        SubscribeNetworkEvent<PlayScriptedSoundNetEvent>(OnPlaySound);
        SubscribeNetworkEvent<StopScriptedSoundLayersNetEvent>(OnStopLayers);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => StopAll());
        Subs.CVar(_cfg, CCVars.MuteScriptedSounds, OnMuteChanged, true);
        _gameStates.GameStateApplied += OnGameStateApplied;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _gameStates.GameStateApplied -= OnGameStateApplied;
    }

    public override void Update(float frameTime)
    {
        var map = _plyMan.LocalEntity is { } player ? Transform(player).MapUid : null;
        if (map != _lastResyncMap)
        {
            _lastResyncMap = map;
            if (map != null)
                _resyncPending = true;
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
            _resyncPending = true;
    }

    private void OnGameStateApplied(GameStateAppliedArgs args)
    {
        if (!_resyncPending)
            return;

        _resyncPending = false;
        RaiseNetworkEvent(new RequestScriptedSoundResyncNetEvent());
    }

    private void OnPlaySound(PlayScriptedSoundNetEvent ev)
    {
        if (_muted)
            return;

        if (!CanPlayAnother())
        {
            if (_suppressedPlays == null)
            {
                _suppressedPlays = 0;
                Logger.GetSawmill("cmu-sfx")
                    .Warning($"[SFX] Concurrent sound cap hit ({MaxConcurrentPlays}), dropping scripted sounds until it clears.");
            }

            _suppressedPlays++;
            return;
        }

        var played = ev.Global
            ? _audio.PlayGlobal(ev.Sound, Filter.Local(), false, ev.Params)
            : ev.AnchorCoords is { } coords
                ? _audio.PlayPvs(ev.Sound, GetCoordinates(coords), ev.Params)
                : null;

        if (played is not { } stream)
            return;

        _activePlays.Add(stream.Entity);

        if (ev.Layer != null)
            ReplaceLoop(ev.Handle, ev.Layer, stream.Entity);

        if (ev.DurationSeconds is { } dur)
            _scheduledStops.Add((stream.Entity, _timing.CurTime + TimeSpan.FromSeconds(dur)));
    }

    private bool CanPlayAnother()
    {
        // Entity lifetime is the truth: one-shots despawn on their own, loops die
        // on replace or stop, so pruning dead entities keeps the count honest.
        for (var i = _activePlays.Count - 1; i >= 0; i--)
        {
            if (TerminatingOrDeleted(_activePlays[i]))
                _activePlays.RemoveAt(i);
        }

        if (_activePlays.Count >= MaxConcurrentPlays)
            return false;

        if (_suppressedPlays is { } suppressed)
        {
            _suppressedPlays = null;
            Logger.GetSawmill("cmu-sfx")
                .Warning($"[SFX] Concurrent sound cap cleared, {suppressed} plays were dropped while it held.");
        }

        return true;
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
