using Content.Goobstation.Client.Audio;
using Content.Shared._RMC14.Deafness;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Hearing;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Hearing;

/// <summary>
///     While the local player is unconscious (including in crit) every sound is turned down and put
///     through a heavy echo, using the area echo's <see cref="AudioEffectSystem"/>. Area echo stands
///     aside while this is active and takes the sounds back afterwards.
/// </summary>
public sealed class CMUUnconsciousHearingSystem : EntitySystem
{
    private static readonly ProtoId<AudioPresetPrototype> Preset = "CMUUnconscious";

    /// <summary>
    ///     How loud everything is while unconscious, as a share of the player's master volume.
    /// </summary>
    private const float VolumeScale = 0.35f;

    /// <summary>
    ///     How long the volume takes to fade between normal and unconscious, in seconds.
    /// </summary>
    private const float FadeSeconds = 1.5f;

    private static readonly TimeSpan ReapplyInterval = TimeSpan.FromSeconds(1);

    [Dependency] private IAudioManager _audioManager = default!;
    [Dependency] private AudioEffectSystem _audioEffect = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _echoed = new();
    private float _configuredMasterVolume = 0.5f;
    private float _scale = 1f;
    private bool _overridingGain;
    private TimeSpan _nextReapply;

    /// <summary>
    ///     Whether the local player is hearing through unconsciousness right now.
    /// </summary>
    public bool Active { get; private set; }

    public override void Initialize()
    {
        Subs.CVar(_cfg, CCVars.AudioMasterVolume, value => _configuredMasterVolume = value, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        StopEcho();
        RestoreGain();
    }

    public override void FrameUpdate(float frameTime)
    {
        var muffled = _player.LocalEntity is { } player && HasComp<CMUMuffledHearingComponent>(player);
        var deaf = _player.LocalEntity is { } local && HasComp<DeafComponent>(local);

        if (muffled && !Active)
        {
            Active = true;
            _nextReapply = TimeSpan.Zero;
        }
        else if (!muffled && Active)
        {
            Active = false;
            StopEcho();
        }

        if (Active)
            ApplyEcho();

        UpdateGain(frameTime, deaf);
    }

    private void ApplyEcho()
    {
        // Sounds can have their effect reset by a new server state, so go over all of them now
        // and then; in between, only new sounds need it.
        var now = _timing.RealTime;
        var reapplyAll = now >= _nextReapply;
        if (reapplyAll)
        {
            _nextReapply = now + ReapplyInterval;
            _echoed.Clear();
        }

        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            if (!_echoed.Add(uid))
                continue;

            _audioEffect.TryAddEffect((uid, audio), Preset);
        }
    }

    private void StopEcho()
    {
        _echoed.Clear();

        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            _audioEffect.TryRemoveEffect((uid, audio));
        }
    }

    private void UpdateGain(float frameTime, bool deaf)
    {
        // Deafness owns the master volume while it lasts.
        if (deaf)
        {
            _overridingGain = false;
            return;
        }

        var target = Active ? VolumeScale : 1f;
        var step = frameTime / FadeSeconds;
        _scale = _scale < target
            ? MathF.Min(target, _scale + step)
            : MathF.Max(target, _scale - step);

        if (_scale >= 1f && !Active)
        {
            RestoreGain();
            return;
        }

        _overridingGain = true;
        _audioManager.SetMasterGain(_configuredMasterVolume * _scale);
    }

    private void RestoreGain()
    {
        _scale = 1f;
        if (!_overridingGain)
            return;

        _overridingGain = false;
        _audioManager.SetMasterGain(_configuredMasterVolume);
    }
}
