using System.Collections.Generic;
using Robust.Shared.Audio;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

/// <summary>
///     Declarative sounds tied to one concrete surgery step.
/// </summary>
[RegisterComponent]
public sealed partial class CMUSurgeryStepAudioComponent : Component
{
    [DataField]
    public List<SoundSpecifier> StartSounds = new();

    [DataField]
    public SoundSpecifier? SuccessSound;

    [DataField]
    public SoundSpecifier? FailureSound;
}
