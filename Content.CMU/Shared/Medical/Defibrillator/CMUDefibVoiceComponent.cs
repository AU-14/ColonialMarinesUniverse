using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Defibrillator;

/// <summary>
/// A defibrillator's voice prompts. An automated one (an AED) analyzes the patient before every shock, says
/// whether a shock is advised, refuses to charge when it isn't and picks its own energy.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUDefibVoiceSystem))]
public sealed partial class CMUDefibVoiceComponent : Component
{
    [DataField]
    public bool Automated;

    [DataField]
    public SoundSpecifier? AnalyzingSound;

    [DataField]
    public SoundSpecifier? ShockAdvisedSound;

    [DataField]
    public SoundSpecifier? NoShockAdvisedSound;

    [DataField]
    public SoundSpecifier? StandClearSound;

    /// <summary>
    /// How long before the shock lands to say "stand clear".
    /// </summary>
    [DataField]
    public TimeSpan StandClearBeforeShock = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Never say "stand clear" sooner than this after charging starts, so it doesn't talk over "shock advised".
    /// </summary>
    [DataField]
    public TimeSpan StandClearMinDelay = TimeSpan.FromSeconds(1.8);

    [ViewVariables]
    public TimeSpan? StandClearAt;
}
