using Content.Shared._RMC14.Deafness;
using Content.Shared.CCVar;
using Content.Shared.StatusEffect;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Deafness;

public sealed partial class DeafnessSystem : SharedDeafnessSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IAudioManager _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private StatusEffectQuerySystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;

    private float _configuredMasterVolume = 0.5f;
    private bool _overridingMasterGain;
    private EntityUid? _overriddenEntity;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeafComponent, ComponentShutdown>(OnDeafShutdown);
        SubscribeLocalEvent<DeafComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        Subs.CVar(_cfg, CCVars.AudioMasterVolume, value => _configuredMasterVolume = value, true);
    }

    private void OnDeafShutdown(EntityUid uid, DeafComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid || _overriddenEntity == uid)
            RestoreMasterGain();
    }

    private void OnPlayerDetached(EntityUid uid, DeafComponent component, LocalPlayerDetachedEvent args)
    {
        RestoreMasterGain();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { } player)
        {
            RestoreMasterGain();
            return;
        }

        var curTime = _timing.CurTime;

        if (!TryComp(player, out DeafComponent? comp) ||
            !TryComp<StatusEffectsComponent>(player, out var status))
        {
            RestoreMasterGain();
            return;
        }

        (TimeSpan, TimeSpan)? time = null;
        (TimeSpan, TimeSpan)? time2 = null;

        if (!_statusEffects.TryGetTime(player, DeafKey, out time, status) &&
            !_statusEffects.TryGetTime(player, "Unconscious", out time2, status))
        {
            comp.DidFadeOut = false;
            RestoreMasterGain();
            return;
        }

        if (time2 != null && (time == null || time.Value.Item2 < time2.Value.Item2))
            time = time2.Value;

        if (time == null)
        {
            comp.DidFadeOut = false;
            RestoreMasterGain();
            return;
        }

        var statusTime = time.Value;

        var lastsFor = (float)(statusTime.Item2 - statusTime.Item1).TotalSeconds;
        var timeLeft = (float)(statusTime.Item2 - curTime).TotalSeconds;
        var timeDone = (float)(curTime - statusTime.Item1).TotalSeconds;

        if (lastsFor <= 0f || timeLeft <= 0f)
        {
            comp.DidFadeOut = false;
            RestoreMasterGain();
            return;
        }

        var volume = 0f;

        var fadeOutDuration = Math.Clamp(lastsFor * 0.35f, 0.2f, 2f);
        var fadeInDuration = Math.Clamp(lastsFor * 0.15f, 0.1f, 1f);

        if (timeDone <= 2f && !comp.DidFadeOut) // Fade out during two seconds of deafness
        {
            var fadeOut = 1f - timeDone / fadeOutDuration;
            volume = fadeOut * _configuredMasterVolume;

            if (volume <= 0.1f) // this is so audio doesn't clip out if a status effect refreshes
            {
                volume = 0f;
                comp.DidFadeOut = true;
            }
        }
        else if (timeLeft <= 1f) // Fade in during last second of deafness
        {
            var fadeIn = 1f - timeLeft / fadeInDuration;
            volume = fadeIn * _configuredMasterVolume;
        }

        volume = Math.Max(0f, volume); // prevents negative volume
        _overridingMasterGain = true;
        _overriddenEntity = player;
        _audio.SetMasterGain(volume);
    }

    private void RestoreMasterGain()
    {
        if (!_overridingMasterGain)
            return;

        _overridingMasterGain = false;
        _overriddenEntity = null;
        _audio.SetMasterGain(_configuredMasterVolume);
    }
}
