using Content.Shared._CMU14.Ops.Sfx;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.CameraShake;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server._RMC14.Announce;
using Content.Shared._RMC14.Announce;
using Content.Shared.GameTicking;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Ops.Sfx;

public sealed partial class ScriptedSoundSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private GeneralAnnounceSystem _generalAnnounce = default!;
    [Dependency] private RMCCameraShakeSystem _cameraShake = default!;

    private readonly Dictionary<MapId, HashSet<MapId>> _connectedMapsCache = new();
    private readonly Dictionary<int, ActiveScriptedSound> _activeSequences = new();
    private readonly List<int> _updateScratch = new();
    private int _nextSequenceHandle = 1;

    public override void Initialize()
    {
        SubscribeLocalEvent<StartScriptedSequenceEvent>(OnStart);
        SubscribeLocalEvent<StopScriptedSequenceEvent>(OnStop);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<MapCreatedEvent>(OnMapCreated);
        SubscribeLocalEvent<MapRemovedEvent>(OnMapRemoved);
        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnZLevelNetworkUpdated);

        foreach (var proto in _proto.EnumeratePrototypes<ScriptedSoundSequencePrototype>())
        {
            ValidateSequenceOrder(proto.ID, proto);
            Logger.GetSawmill("cmu-sfx").Debug($"[SFX] Loaded sequence prototype '{proto.ID}' with {proto.Entries.Count} entries");
        }
    }

    private void OnStart(ref StartScriptedSequenceEvent ev)
        => ev.SequenceHandle ??= CreateSequence(ev.SequenceId, ev.Anchor);

    private void OnStop(ref StopScriptedSequenceEvent ev)
        => StopSequence(ev.SequenceHandle);

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _connectedMapsCache.Clear();
        _nextSequenceHandle = 1;
        foreach (var uid in new List<int>(_activeSequences.Keys))
            StopSequence(uid);
    }

    private void OnMapCreated(MapCreatedEvent ev) => _connectedMapsCache.Clear();
    private void OnMapRemoved(MapRemovedEvent ev) => _connectedMapsCache.Clear();
    private void OnZLevelNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent ev) => _connectedMapsCache.Clear();

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (!ev.WasModified<ScriptedSoundSequencePrototype>()) return;
        foreach (var proto in _proto.EnumeratePrototypes<ScriptedSoundSequencePrototype>())
        {
            ValidateSequenceOrder(proto.ID, proto);
            Logger.GetSawmill("cmu-sfx").Debug($"[SFX] Reloaded prototype '{proto.ID}'");
        }
    }

    public override void Update(float frameTime)
    {
        if (_activeSequences.Count == 0)
            return;

        _updateScratch.Clear();
        _updateScratch.EnsureCapacity(_activeSequences.Count);
        foreach (var uid in _activeSequences.Keys)
            _updateScratch.Add(uid);

        foreach (var uid in _updateScratch)
        {
            if (!_activeSequences.TryGetValue(uid, out var active))
                continue;
            List<string>? deadKeys = null;
            foreach (var kv in active.ActiveLoops)
            {
                if (!TerminatingOrDeleted(kv.Value))
                    continue;

                deadKeys ??= new List<string>();
                deadKeys.Add(kv.Key);
            }

            if (deadKeys != null)
                foreach (var key in deadKeys)
                    active.ActiveLoops.Remove(key);

            if (!active.WarnedEmptyFilter && active.AnchorEntity is { } anchor && TerminatingOrDeleted(anchor))
            {
                active.WarnedEmptyFilter = true;
                Logger.GetSawmill("cmu-sfx").Warning($"[SFX] Sequence '{active.SequenceId}' anchor entity was deleted. Remaining audio/announcements and markers will not reach any players!");
            }

            if (!_proto.TryIndex<ScriptedSoundSequencePrototype>(active.SequenceId, out var seq))
            {
                Logger.GetSawmill("cmu-sfx").Warning($"[SFX] Sequence prototype '{active.SequenceId}' no longer exists. Stopping {uid}!");
                StopSequence(uid);
                continue;
            }

            var elapsed = _timing.CurTime - active.StartTime;
            for (var i = active.NextEntryIndex; i < seq.Entries.Count; i++)
            {
                var entry = seq.Entries[i];
                if (!active.JitteredDelays.TryGetValue(i, out var effectiveDelay))
                    effectiveDelay = TimeSpan.FromSeconds(entry.DelaySeconds);

                if (elapsed < effectiveDelay)
                    break;

                PlayEntry(uid, active, seq, entry);
                if (entry.RepeatSeconds is { } rep)
                    active.RepeatingEntries.Add((i, effectiveDelay + TimeSpan.FromSeconds(rep)));
                active.NextEntryIndex = i + 1;
            }

            for (var i = active.RepeatingEntries.Count - 1; i >= 0; i--)
            {
                var (index, nextFire) = active.RepeatingEntries[i];
                var entry = index < seq.Entries.Count ? seq.Entries[index] : null;
                if (entry?.RepeatSeconds is not { } interval)
                {
                    active.RepeatingEntries.RemoveAt(i);
                    continue;
                }

                if (elapsed < nextFire)
                    continue;

                PlayEntry(uid, active, seq, entry);
                var seconds = interval;
                if (entry.DelayJitterSeconds is { } jitter and > 0)
                    seconds += _random.NextFloat(-jitter, jitter);
                active.RepeatingEntries[i] = (index, nextFire + TimeSpan.FromSeconds(MathF.Max(seconds, 0.1f)));
            }

            for (var i = active.ScheduledLoopStops.Count - 1; i >= 0; i--)
            {
                var (stopAt, loopEnt) = active.ScheduledLoopStops[i];
                if (_timing.CurTime < stopAt) continue;
                _audio.Stop(loopEnt);
                RemoveLoopByEntity(active, loopEnt);
                active.ScheduledLoopStops.RemoveAt(i);
            }

            if (active.NextEntryIndex >= seq.Entries.Count
                    && active.ActiveLoops.Count == 0
                    && active.ScheduledLoopStops.Count == 0
                    && active.RepeatingEntries.Count == 0)
                StopSequence(uid);
        }
    }

    private static void RemoveLoopByEntity(ActiveScriptedSound active, EntityUid entity)
    {
        string? key = null;
        foreach (var (k, v) in active.ActiveLoops)
        {
            if (v != entity)
                continue;

            key = k;
            break;
        }

        if (key != null)
            active.ActiveLoops.Remove(key);
    }

    public bool TryGetActiveSequence(int handle, out ActiveScriptedSound active)
        => _activeSequences.TryGetValue(handle, out active!);

    public bool TryGetActiveSequence(string sequenceId, EntityUid? anchor, out int handle)
    {
        foreach (var (h, active) in _activeSequences)
        {
            if (active.SequenceId == sequenceId && active.AnchorEntity == anchor)
            {
                handle = h;
                return true;
            }
        }
        handle = 0;
        return false;
    }

    public void StopSequence(int handle)
    {
        if (!_activeSequences.Remove(handle, out var active))
        {
            Logger.GetSawmill("cmu-sfx").Debug($"[SFX] Tried to stop unknown sequence {handle}");
            return;
        }
        Logger.GetSawmill("cmu-sfx").Debug($"[SFX] Stopping sequence {handle} ({active.SequenceId})");

        var anchor = active.AnchorEntity;
        NetCoordinates? netCoords = null;
        if (anchor is { } a && !TerminatingOrDeleted(a))
            netCoords = GetNetCoordinates(Transform(a).Coordinates);

        StopLoops(active);
        var filter = GetMapFilter(anchor);
        RaiseNetworkEvent(new ScriptedSequenceMarkerNetEvent(
                active.SequenceId,
                ScriptedSoundMarkers.SequenceStopped,
                netCoords),
            filter);
        active.JitteredDelays.Clear();
        active.RepeatingEntries.Clear();
    }

    public int? StartSequence(ProtoId<ScriptedSoundSequencePrototype> id, EntityUid? anchor = null)
    {
        var result = CreateSequence(id, anchor);
        if (result != null)
            Logger.GetSawmill("cmu-sfx").Info($"[SFX] Started sequence '{id}' as {result.Value} with anchor={anchor}");
        return result;
    }

    private int? CreateSequence(string id, EntityUid? anchor)
    {
        if (!_proto.TryIndex<ScriptedSoundSequencePrototype>(id, out var seq))
        {
            Logger.GetSawmill("cmu-sfx").Error($"[SFX] Tried to start unknown scripted sound sequence '{id}'");
            return null;
        }
        if (!ValidateSequenceOrder(id, seq))
        {
            Logger.GetSawmill("cmu-sfx").Error($"[SFX] Sequence Prototype Order is invalid '{id}'");
            return null;
        }

        if (TryGetActiveSequence(id, anchor, out var existing))
        {
            Logger.GetSawmill("cmu-sfx").Debug($"[SFX] Sequence '{id}' already active on anchor {anchor} (handle {existing}), reusing.");
            return existing;
        }

        var handle = _nextSequenceHandle++;
        var active = new ActiveScriptedSound
        {
            SequenceId = id,
            StartTime = _timing.CurTime,
            AnchorEntity = anchor,
        };

        InitializeJitteredDelays(active, seq);
        _activeSequences[handle] = active;
        return handle;
    }

    private void PlayEntry(int handle, ActiveScriptedSound active, ScriptedSoundSequencePrototype seq, ScriptedSoundEntry entry)
    {
        if (entry.StopAllLoops)
            StopLoops(active);
        else if (entry.StopLoops is { } stopLayers)
            foreach (var layer in stopLayers)
                StopLoop(active, layer);

        if (entry.Sound != null)
        {
            var audioParams = entry.AudioParams ?? entry.Sound.Params;
            if (entry.VolumeJitter is { } vj and > 0)
                audioParams = audioParams.AddVolume(_random.NextFloat(-vj, vj));
            if (entry.Loop)
                audioParams = audioParams.WithLoop(true);
            (EntityUid Entity, AudioComponent Component)? played;

            if (entry.GlobalAudio)
            {
                var filter = GetMapFilter(active.AnchorEntity);
                if (filter.Count == 0)
                {
                    Logger.GetSawmill("cmu-sfx").Warning($"[SFX] Sequence '{active.SequenceId}' has no valid anchor; skipping global audio.");
                    return;
                }

                var resolved = _audio.ResolveSound(entry.Sound);
                played = _audio.PlayGlobal(resolved, filter, false, audioParams);
            }
            else
            {
                if (active.AnchorEntity == null || TerminatingOrDeleted(active.AnchorEntity.Value))
                {
                    Logger.GetSawmill("cmu-sfx").Warning($"[SFX] Sequence '{active.SequenceId}' anchor entity is invalid for non-global audio. Skipping sound.");
                    return;
                }
                played = _audio.PlayPvs(entry.Sound, active.AnchorEntity.Value, audioParams);
            }

            if (played != null)
            {
                if (entry.Loop)
                {
                    var layer = entry.Layer ?? $"__anon_{active.NextEntryIndex}";
                    if (active.ActiveLoops.Remove(layer, out var old))
                    {
                        _audio.Stop(old);
                        for (var i = active.ScheduledLoopStops.Count - 1; i >= 0; i--)
                        {
                            if (active.ScheduledLoopStops[i].Entity == old)
                                active.ScheduledLoopStops.RemoveAt(i);
                        }
                    }
                    active.ActiveLoops[layer] = played.Value.Entity;
                }

                if (entry.DurationSeconds is { } dur)
                    active.ScheduledLoopStops.Add((_timing.CurTime + TimeSpan.FromSeconds(dur), played.Value.Entity));
            }
        }

        if (entry.Announcement is { } announcement)
        {
            var filter = GetMapFilter(active.AnchorEntity);
            _generalAnnounce.AnnounceAdvanced(new AnnouncementRequest
            {
                Preset = announcement.Preset ?? seq.DefaultAnnouncementPreset ?? "SelfDestructAnnouncement",
                Message = announcement.Message,
                Target = AnnouncementTarget.All,
                Speaker = active.AnchorEntity,
                Source = active.AnchorEntity,
            }, filter);
        }

        if (!string.IsNullOrEmpty(entry.Marker))
        {
            var localEv = new ScriptedSequenceMarkerEvent(active.SequenceId, entry.Marker, handle);
            RaiseLocalEvent(ref localEv);

            if (entry is { Marker: ScriptedSoundMarkers.Explode, MarkerData.ShakeIntensity: { } shakeIntensity })
            {
                var filter = GetMapFilter(active.AnchorEntity);
                var duration = entry.MarkerData.Duration ?? 1f;
                var spacing = TimeSpan.FromSeconds(0.05);
                var shakes = (int)(duration / spacing.TotalSeconds);
                _cameraShake.ShakeCamera(filter, shakes, (int)shakeIntensity, spacing);
            }

            NetCoordinates? netCoords = null;
            if (active.AnchorEntity is { Valid: true } markerAnchor)
                netCoords = GetNetCoordinates(Transform(markerAnchor).Coordinates);

            RaiseNetworkEvent(new ScriptedSequenceMarkerNetEvent(active.SequenceId, entry.Marker,
                netCoords, entry.MarkerData), GetMapFilter(active.AnchorEntity));

            if (active.AnchorEntity == null || TerminatingOrDeleted(active.AnchorEntity.Value))
                return;
        }

        Logger.GetSawmill("cmu-sfx").Debug($"[SFX] Sequence {active.SequenceId} fired marker '{entry.Marker}' at {_timing.CurTime}");
    }

    private void StopLoops(ActiveScriptedSound active)
    {
        foreach (var (_, entity) in active.ActiveLoops)
            _audio.Stop(entity);

        active.ActiveLoops.Clear();
        active.ScheduledLoopStops.Clear();
    }

    private void StopLoop(ActiveScriptedSound active, string layer)
    {
        if (!active.ActiveLoops.Remove(layer, out var entity))
            return;

        _audio.Stop(entity);
        for (var i = active.ScheduledLoopStops.Count - 1; i >= 0; i--)
        {
            if (active.ScheduledLoopStops[i].Entity == entity)
                active.ScheduledLoopStops.RemoveAt(i);
        }
    }

    private Filter GetMapFilter(EntityUid? reference)
    {
        if (reference == null || TerminatingOrDeleted(reference.Value))
            return Filter.Empty();

        var mapUid = Transform(reference.Value).MapUid;
        if (mapUid == null)
            return Filter.Empty();

        var mapId = Transform(mapUid.Value).MapID;
        if (!_connectedMapsCache.TryGetValue(mapId, out var maps))
        {
            maps = GetConnectedMaps(mapUid.Value);
            _connectedMapsCache[mapId] = maps;
        }

        if (maps.Count == 1)
            return Filter.BroadcastMap(mapId);

        var filter = Filter.Empty();
        foreach (var connectedMap in maps)
            filter.AddPlayers(Filter.BroadcastMap(connectedMap).Recipients);

        return filter;
    }

    private HashSet<MapId> GetConnectedMaps(EntityUid mapUid)
    {
        if (!_zLevels.TryGetZNetwork(mapUid, out var network))
            return new HashSet<MapId> { Transform(mapUid).MapID };

        var maps = new HashSet<MapId>();
        foreach (var mapEntity in network.Value.Comp.ZLevels.Values)
        {
            if (mapEntity is not { } resolved)
                continue;

            maps.Add(Transform(resolved).MapID);
        }

        return maps;
    }

    private void InitializeJitteredDelays(ActiveScriptedSound active, ScriptedSoundSequencePrototype seq)
    {
        active.JitteredDelays.Clear();
        var last = TimeSpan.Zero;

        for (var i = 0; i < seq.Entries.Count; i++)
        {
            var entry = seq.Entries[i];
            TimeSpan effectiveDelay;

            if (entry.JitterInterval && i > 0 && entry.DelayJitterSeconds is { } intervalJitter and > 0)
            {
                var prevEntry = seq.Entries[i - 1];
                var nominalInterval = entry.DelaySeconds - prevEntry.DelaySeconds;
                var jitter = _random.NextFloat(-intervalJitter, intervalJitter);
                effectiveDelay = active.JitteredDelays[i - 1] + TimeSpan.FromSeconds(nominalInterval + jitter);
            }
            else
            {
                effectiveDelay = TimeSpan.FromSeconds(entry.DelaySeconds);

                if (entry.DelayJitterSeconds is { } absJitter and > 0)
                    effectiveDelay += TimeSpan.FromSeconds(_random.NextFloat(-absJitter, absJitter));
            }

            if (effectiveDelay < last)
                effectiveDelay = last;

            active.JitteredDelays[i] = effectiveDelay;
            last = effectiveDelay;
        }
    }

    private static bool ValidateSequenceOrder(string id, ScriptedSoundSequencePrototype seq)
    {
        for (var i = 0; i < seq.Entries.Count; i++)
        {
            var entry = seq.Entries[i];
            if (entry.RepeatSeconds is < 0.1f)
            {
                Logger.GetSawmill("cmu-sfx").Error($"[SFX] Sequence '{id}' entry={i} repeat={entry.RepeatSeconds}s; minimum is 0.1s.");
                return false;
            }
        }

        for (var i = 1; i < seq.Entries.Count; i++)
        {
            if (!(seq.Entries[i].DelaySeconds < seq.Entries[i - 1].DelaySeconds)) continue;
            Logger.GetSawmill("cmu-sfx").Error(
                $"[SFX] Sequence '{id}' entry={i} has delay {seq.Entries[i].DelaySeconds}s < prev entry delay {seq.Entries[i - 1].DelaySeconds}s." +
                " Entries must be sorted by ascending delay!");
            return false;
        }
        return true;
    }

    public IReadOnlyDictionary<int, ActiveScriptedSound> GetActiveSequences() => _activeSequences;
}
