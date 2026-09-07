using Content.Shared.Chat;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Random.Helpers;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class SpeechSoundSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    [SubscribeLocalEvent]
    private void OnEntitySpoke(Entity<SpeechComponent> ent, ref EntitySpokeEvent args)
    {
        var speechSounds = GetSpeechSoundOverride(ent.Owner, args.Language) ?? ent.Comp.SpeechSounds;
        if (speechSounds == null)
            return;

        var currentTime = _gameTiming.CurTime;
        var cooldown = TimeSpan.FromSeconds(ent.Comp.SoundCooldownTime);

        // Ensure more than the cooldown time has passed since last speaking
        if (currentTime - ent.Comp.LastTimeSoundPlayed < cooldown)
            return;

        var sound = GetSpeechSound(ent, args.Message, speechSounds);
        ent.Comp.LastTimeSoundPlayed = currentTime;
        if (_net.IsServer) // TODO: replace this call with PlayPredicted when chat is predicted.
            _audio.PlayPvs(sound, ent);
    }

    /// <summary>
    /// Gets the speech sound for a message.
    /// </summary>
    public SoundSpecifier? GetSpeechSound(
        Entity<SpeechComponent> ent,
        string message,
        ProtoId<SpeechSoundsPrototype>? speechSoundsOverride = null)
    {
        var speechSounds = speechSoundsOverride ?? ent.Comp.SpeechSounds;
        if (speechSounds == null)
            return null;

        // Play speech sound
        var prototype = ProtoMan.Index<SpeechSoundsPrototype>(speechSounds);

        // Different sounds for ask/exclaim based on last character
        var contextSound = message[^1] switch
        {
            '?' => prototype.AskSound,
            '!' => prototype.ExclaimSound,
            _ => prototype.SaySound
        };

        // Use exclaim sound if most characters are uppercase.
        var uppercaseCount = 0;
        foreach (var t in message)
        {
            if (char.IsUpper(t))
                uppercaseCount++;
        }

        if (uppercaseCount > message.Length / 2)
        {
            contextSound = prototype.ExclaimSound;
        }

        var random = SharedRandomExtensions.PredictedRandom(_gameTiming, GetNetEntity(ent));
        var scale = (float)random.NextGaussian(1, prototype.Variation);
        contextSound.Params = ent.Comp.AudioParams.WithPitchScale(scale);
        return contextSound;
    }

    private ProtoId<SpeechSoundsPrototype>? GetSpeechSoundOverride(
        EntityUid uid,
        ProtoId<LanguagePrototype>? language)
    {
        if (language == null ||
            HasComp<XenoComponent>(uid) ||
            !ProtoMan.TryIndex(language.Value, out var languagePrototype))
        {
            return null;
        }

        return languagePrototype.SpeechOverride.SpeechSoundsOverride;
    }
}
