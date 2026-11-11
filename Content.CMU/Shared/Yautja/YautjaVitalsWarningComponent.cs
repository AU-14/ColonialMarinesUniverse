using Robust.Shared.Audio;

namespace Content.Shared.CMU14.Yautja;

// one-shot heads-up before crit. cmss13 preds read this off the pain doll, we don't have one
[RegisterComponent]
public sealed partial class YautjaVitalsWarningComponent : Component
{
    // fraction of the crit threshold. cmss13's doll tops out at 80% pain, and pred max pain == damage to crit
    [DataField]
    public float WarnFraction = 0.8f;

    // has to heal back under this before it can warn again, so chip damage around the line doesn't spam it
    [DataField]
    public float RearmFraction = 0.7f;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/CMU14/Yautja/signal.ogg");

    [DataField]
    public bool Warned;
}
