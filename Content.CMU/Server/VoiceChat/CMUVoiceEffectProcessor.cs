using Content.Shared.CMU14.VoiceChat;
using NWaves.Effects;
using NWaves.Effects.Base;
using NWaves.Filters.Base;
using NWaves.Filters.BiQuad;
using NWaves.Signals.Builders;
using NWaves.Signals.Builders.Base;

namespace Content.Server.CMU14.VoiceChat;

public enum CMUVoiceMuffle : byte
{
    None,
    Light,
    Heavy,
}

public readonly record struct CMUVoiceEffectSettings(CMUVoiceEffect Effect, CMUVoiceMuffle Muffle, bool Megaphone = false);

public sealed class CMUVoiceEffectProcessor
{
    private const int SampleRate = CMUVoiceCodec.SampleRate;
    private const int FftSize = 512;
    private const int HopSize = 128;

    private CMUVoiceEffect _effect = CMUVoiceEffect.None;
    private CMUVoiceMuffle _muffle = CMUVoiceMuffle.None;
    private bool _megaphone;
    private IOnlineFilter? _voice;
    private IOnlineFilter? _megaphoneFilter;
    private IOnlineFilter? _muffleFilter;

    public void Process(short[] buffer, int offset, int count, CMUVoiceEffectSettings settings)
    {
        var effect = settings.Effect;
        var muffle = settings.Muffle;
        if (effect == CMUVoiceEffect.Muffled)
        {
            effect = CMUVoiceEffect.None;
            muffle = CMUVoiceMuffle.Heavy;
        }

        if (effect != _effect)
        {
            _effect = effect;
            _voice = CreateVoice(effect);
        }

        if (muffle != _muffle)
        {
            _muffle = muffle;
            _muffleFilter = CreateMuffle(muffle);
        }

        if (settings.Megaphone != _megaphone)
        {
            _megaphone = settings.Megaphone;
            _megaphoneFilter = _megaphone ? CreateMegaphone() : null;
        }

        if (_voice == null && _megaphoneFilter == null && _muffleFilter == null)
            return;

        for (var i = offset; i < offset + count; i++)
        {
            var sample = buffer[i] / 32768f;
            if (_voice != null)
                sample = _voice.Process(sample);
            if (_megaphoneFilter != null)
                sample = _megaphoneFilter.Process(sample);
            if (_muffleFilter != null)
                sample = _muffleFilter.Process(sample);

            buffer[i] = (short) Math.Clamp(sample * 32767f, short.MinValue, short.MaxValue);
        }
    }

    private static IOnlineFilter? CreateVoice(CMUVoiceEffect effect)
    {
        return effect switch
        {
            CMUVoiceEffect.Robot => Chain(
                Mix(new RobotEffect(HopSize, FftSize), 1.6f)),
            CMUVoiceEffect.Alien => Chain(
                PitchShift(0.8, 0.4f),
                Mix(new TremoloEffect(SampleRate, 0.5f, 28f, 1f), 1f),
                Mix(new DistortionEffect(DistortionMode.SoftClipping, 0f, -2f), 1f)),
            CMUVoiceEffect.Abductor => Chain(
                PitchShift(1.35, 0.72f),
                new RingModulator(90f, 0.45f),
                Mix(new VibratoEffect(SampleRate, 6f, 0.002f), 1f)),
            CMUVoiceEffect.Deep => Chain(
                PitchShift(0.7, 0.35f)),
            CMUVoiceEffect.High => Chain(
                PitchShift(1.5, 0.45f)),
            CMUVoiceEffect.Whisper => Chain(
                Mix(new WhisperEffect(HopSize, FftSize), 1.35f)),
            CMUVoiceEffect.Radio => Chain(
                new HighPassFilter(350.0 / SampleRate, 0.707),
                new LowPassFilter(2800.0 / SampleRate, 0.707),
                Mix(new DistortionEffect(DistortionMode.SoftClipping, 4f, -4f), 1f),
                new NoiseMixer(0.012f)),
            _ => null,
        };
    }

    private static IOnlineFilter CreateMegaphone()
    {
        return Chain(
            new HighPassFilter(500.0 / SampleRate, 0.9),
            new LowPassFilter(3600.0 / SampleRate, 0.9),
            Mix(new DistortionEffect(DistortionMode.HardClipping, 12f, -8f), 1f),
            new LowPassFilter(4200.0 / SampleRate, 0.707));
    }

    private static IOnlineFilter? CreateMuffle(CMUVoiceMuffle muffle)
    {
        return muffle switch
        {
            CMUVoiceMuffle.Light => Chain(
                new LowPassFilter(1800.0 / SampleRate, 0.707)),
            CMUVoiceMuffle.Heavy => Chain(
                new LowPassFilter(700.0 / SampleRate, 0.707),
                new LowPassFilter(700.0 / SampleRate, 0.707)),
            _ => null,
        };
    }

    private static PitchShiftVocoderEffect PitchShift(double shift, float gain)
    {
        return Mix(new PitchShiftVocoderEffect(SampleRate, shift, FftSize, HopSize), gain);
    }

    private static T Mix<T>(T effect, float wet) where T : IMixable
    {
        effect.Wet = wet;
        effect.Dry = 0f;
        return effect;
    }

    private static IOnlineFilter Chain(params IOnlineFilter[] filters)
    {
        return new FilterChain(filters);
    }

    private sealed class RingModulator(float frequency, float mix) : IOnlineFilter
    {
        private readonly SignalBuilder _oscillator = new SineBuilder()
            .SetParameter("frequency", frequency)
            .SampledAt(SampleRate);

        public float Process(float sample)
        {
            return sample * (1f - mix) + sample * _oscillator.NextSample() * mix;
        }

        public void Reset()
        {
            _oscillator.Reset();
        }
    }

    private sealed class NoiseMixer(float level) : IOnlineFilter
    {
        private readonly SignalBuilder _noise = new WhiteNoiseBuilder()
            .SetParameter("min", -1)
            .SetParameter("max", 1)
            .SampledAt(SampleRate);

        public float Process(float sample)
        {
            return sample + _noise.NextSample() * level;
        }

        public void Reset()
        {
            _noise.Reset();
        }
    }
}
