using Content.Shared.DoAfter;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Lab;

/// <summary>
/// Tracks a human test subject through the Weyland-Yutani Wendigo procedure.
/// Each stage only accepts its next input once <see cref="StageEndsAt"/> has passed.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUWendigoSubjectComponent : Component
{
    [DataField]
    public CMUWendigoSubjectStage Stage = CMUWendigoSubjectStage.Fed1;

    [DataField, AutoPausedField]
    public TimeSpan StageEndsAt;

    /// <summary>
    /// When the current stage began, for progress readouts.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan StageStartedAt;

    /// <summary>
    /// Once a stage is ready, its next input must land before this or the subject relapses a stage.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan WindowEndsAt;

    /// <summary>
    /// Whether the "window closing" warning has played for the current window.
    /// </summary>
    [DataField]
    public bool WindowWarned;

    /// <summary>
    /// Mistakes accumulated over the procedure. High instability breaks the MH-33 bond; the maximum kills the subject.
    /// </summary>
    [DataField]
    public int Instability;

    /// <summary>
    /// Next "ready" reminder (cough, shiver) while the subject waits for its next input.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan NextReadyCueAt;

    /// <summary>
    /// Whether the one-time cue for the current stage expiring has played.
    /// </summary>
    [DataField]
    public bool ReadyCuePlayed;

    [DataField, AutoPausedField]
    public TimeSpan NextFlavorAt;

    [DataField, AutoPausedField]
    public TimeSpan NextSeizureAt;

    /// <summary>
    /// Constant-seizure refresh during the final mutation.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan NextMutationPulseAt;

    /// <summary>
    /// Whether the final dose was MH-33, which binds the Wendigo to <see cref="Master"/>.
    /// </summary>
    [DataField]
    public bool Tamed;

    [DataField]
    public EntityUid? Master;

    [DataField]
    public string? MasterName;

    /// <summary>
    /// Last entity to stick an injector into the subject, recorded just before the reagent lands.
    /// </summary>
    [DataField]
    public EntityUid? LastInjector;

    [DataField, AutoPausedField]
    public TimeSpan LastInjectorAt;

    /// <summary>
    /// The running final-mutation do-after, cancelled if the subject dies.
    /// </summary>
    [ViewVariables]
    public DoAfterId? DoAfter;
}

public enum CMUWendigoSubjectStage : byte
{
    /// <summary>Fed human meat while starving; waiting 1-5 minutes.</summary>
    Fed1,

    /// <summary>Fed human meat a second time; waiting 1-5 minutes before neural priming.</summary>
    Fed2,

    /// <summary>Neural primer administered; ready for Stabilized Mutagen.</summary>
    Primed,

    /// <summary>Injected with Stabilized Mutagen; waiting 5-10 minutes before brain treatment and a raw heart.</summary>
    Mutagen,

    /// <summary>Fed a raw human heart; 5 minutes of hunger and seizures before tissue stabilization.</summary>
    Gestation,

    /// <summary>Tissue stabilizer administered; ready for MH-32 or MH-33.</summary>
    Stabilized,

    /// <summary>Injected with MH-32 or MH-33; one minute of constant seizure before turning.</summary>
    Mutating,
}
