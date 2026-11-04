using Content.Shared.CMU14.VoiceChat;

namespace Content.Client.CMU14.VoiceChat;

/// <summary>
/// Makes 16 kHz voice sound like it came through a speaker: a band-pass, soft-clipped drive for
/// crunch and a faint hiss. The default is a headset (350 Hz–3 kHz); <see cref="PublicAddress"/>
/// is a wider, cleaner ship PA and <see cref="Telephone"/> a phone line. Holds filter state, so use one per stream.
/// </summary>
public sealed class CMUVoiceRadioFilter
{
    private readonly float _drive;
    private readonly float _outputGain;
    private readonly float _hiss;
    private readonly Biquad _highPass1;
    private readonly Biquad _highPass2;
    private readonly Biquad _lowPass1;
    private readonly Biquad _lowPass2;
    private readonly Random _random;

    public CMUVoiceRadioFilter(int seed = 0) : this(350f, 3000f, 2.2f, 0.85f, 0.006f, seed)
    {
    }

    private CMUVoiceRadioFilter(float lowCut, float highCut, float drive, float outputGain, float hiss, int seed)
    {
        _drive = drive;
        _outputGain = outputGain;
        _hiss = hiss;
        _highPass1 = Biquad.HighPass(lowCut);
        _highPass2 = Biquad.HighPass(lowCut);
        _lowPass1 = Biquad.LowPass(highCut);
        _lowPass2 = Biquad.LowPass(highCut);
        _random = new Random(seed);
    }

    /// <summary>
    /// The ship and colony PA used for live console announcements.
    /// </summary>
    public static CMUVoiceRadioFilter PublicAddress(int seed = 0)
    {
        return new CMUVoiceRadioFilter(250f, 4500f, 1.6f, 0.9f, 0.002f, seed);
    }

    /// <summary>
    /// A telephone line: the classic 300 Hz–3.4 kHz band, a little warmer and quieter than a headset.
    /// </summary>
    public static CMUVoiceRadioFilter Telephone(int seed = 0)
    {
        return new CMUVoiceRadioFilter(300f, 3400f, 1.5f, 0.9f, 0.003f, seed);
    }

    public void Process(Span<short> pcm)
    {
        for (var i = 0; i < pcm.Length; i++)
        {
            pcm[i] = ToShort(ProcessSample(pcm[i] / 32768f));
        }
    }

    private float ProcessSample(float sample)
    {
        sample += (float) (_random.NextDouble() * 2 - 1) * _hiss;
        sample = _highPass2.Process(_highPass1.Process(sample));
        sample = SoftClip(sample * _drive) * _outputGain;
        return _lowPass2.Process(_lowPass1.Process(sample));
    }

    /// <summary>
    /// The squelch played when a radio transmission opens: a short tone blip over a burst of static.
    /// </summary>
    public static short[] CreateOpenClick(float volume)
    {
        return CreateSquelch(volume, lengthMs: 70, toneHz: 1800f, toneMs: 18, noiseDecayMs: 18, seed: 1);
    }

    /// <summary>
    /// The longer static tail played when a radio transmission closes.
    /// </summary>
    public static short[] CreateCloseClick(float volume)
    {
        return CreateSquelch(volume, lengthMs: 140, toneHz: 1200f, toneMs: 10, noiseDecayMs: 45, seed: 2);
    }

    private static short[] CreateSquelch(float volume, int lengthMs, float toneHz, int toneMs, float noiseDecayMs, int seed)
    {
        const int sampleRate = CMUVoiceCodec.SampleRate;
        var length = sampleRate * lengthMs / 1000;
        var toneLength = sampleRate * toneMs / 1000;
        var noiseDecay = sampleRate * noiseDecayMs / 1000f;
        var random = new Random(seed);
        var pcm = new short[length];

        for (var i = 0; i < length; i++)
        {
            var noise = (float) (random.NextDouble() * 2 - 1) * 0.55f * MathF.Exp(-i / noiseDecay);
            var tone = i < toneLength
                ? MathF.Sin(MathF.Tau * toneHz * i / sampleRate) * 0.35f * (1f - i / (float) toneLength)
                : 0f;

            pcm[i] = ToShort((noise + tone) * volume);
        }

        // Run it through the same headset chain so the click matches the voice.
        new CMUVoiceRadioFilter(seed).Process(pcm);
        return pcm;
    }

    private static float SoftClip(float x)
    {
        return x / (1f + MathF.Abs(x));
    }

    private static short ToShort(float sample)
    {
        return (short) Math.Clamp(sample * 32767f, short.MinValue, short.MaxValue);
    }

    /// <summary>
    /// RBJ cookbook biquad, Butterworth Q, at the voice codec's sample rate.
    /// </summary>
    private sealed class Biquad
    {
        private const float Q = 0.7071f;

        private readonly float _b0, _b1, _b2, _a1, _a2;
        private float _x1, _x2, _y1, _y2;

        private Biquad(float b0, float b1, float b2, float a0, float a1, float a2)
        {
            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }

        public static Biquad LowPass(float cutoff)
        {
            var (cos, alpha) = Prepare(cutoff);
            return new Biquad((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public static Biquad HighPass(float cutoff)
        {
            var (cos, alpha) = Prepare(cutoff);
            return new Biquad((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        private static (float Cos, float Alpha) Prepare(float cutoff)
        {
            var omega = MathF.Tau * cutoff / CMUVoiceCodec.SampleRate;
            return (MathF.Cos(omega), MathF.Sin(omega) / (2 * Q));
        }

        public float Process(float x)
        {
            var y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;
            return y;
        }
    }
}
