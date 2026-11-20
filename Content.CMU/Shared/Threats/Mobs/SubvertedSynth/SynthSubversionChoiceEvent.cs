using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Threats.Mobs.SubvertedSynth;

[Serializable, NetSerializable]
public sealed record SynthSubversionChoiceEvent(NetEntity Synth, int Generation, bool Accept);
