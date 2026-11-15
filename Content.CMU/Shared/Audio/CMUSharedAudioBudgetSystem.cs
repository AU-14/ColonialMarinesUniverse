using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Audio;

/// <summary>
/// Admissions for content-owned transient audio. Rejected requests never create
/// audio entities and cannot release another sound's concurrency slot on deletion.
/// </summary>
public abstract partial class CMUSharedAudioBudgetSystem : EntitySystem
{
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected IConfigurationManager Configuration = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<(ICommonSession Listener, string File), List<AudioComponent>> _active = new();
    private readonly List<(ICommonSession Listener, string File)> _empty = new();
    private TimeSpan _nextCleanup;

    public virtual (EntityUid Entity, AudioComponent Component)? PlayPredicted(
        SoundSpecifier? sound, EntityUid source, EntityUid? user, AudioParams? parameters = null)
    {
        return Audio.PlayPredicted(sound, source, user, parameters);
    }

    public Filter LimitAudience(ResolvedSoundSpecifier sound, Filter audience, AudioParams parameters)
    {
        if (parameters.Loop || Audio.GetAudioPath(sound) is not { } file)
            return audience;

        var limit = Math.Max(0, Configuration.GetCVar(CCVars.CMUTransientAudioPerFile));
        var admitted = Filter.Empty();
        foreach (var listener in audience.Recipients)
        {
            var count = 0;
            if (_active.TryGetValue((listener, file), out var playing))
            {
                playing.RemoveAll(static audio => audio.Deleted);
                count = playing.Count;
            }
            if (count < limit)
                admitted.AddPlayer(listener);
        }
        return admitted;
    }

    public void Track(AudioComponent audio, Filter audience)
    {
        if (audio.Params.Loop)
            return;

        foreach (var listener in audience.Recipients)
        {
            var key = (listener, audio.FileName);
            if (!_active.TryGetValue(key, out var playing))
                _active.Add(key, playing = new());
            playing.Add(audio);
        }
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextCleanup)
            return;
        _nextCleanup = _timing.CurTime + TimeSpan.FromSeconds(1);
        _empty.Clear();
        foreach (var (key, playing) in _active)
        {
            playing.RemoveAll(static audio => audio.Deleted);
            if (playing.Count == 0)
                _empty.Add(key);
        }
        foreach (var key in _empty)
            _active.Remove(key);
        _empty.Clear();
    }

    public override void Shutdown()
    {
        _active.Clear();
        _empty.Clear();
        base.Shutdown();
    }
}
