using Content.Shared._CMU14.Ops.Sfx;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Ops.Sfx;

/// <summary>Starts a scripted seq anchored to an entity (e.g. speaker)</summary>
public sealed partial class AtmosphericSequenceSystem : EntitySystem
{
    [Dependency] private ScriptedSoundSystem _scriptedSound = default!;

    public int? StartLocal(ProtoId<ScriptedSoundSequencePrototype> prototype, EntityUid anchor)
        => _scriptedSound.StartSequence(prototype, anchor);

    public void Stop(int sequenceHandle)
        => _scriptedSound.StopSequence(sequenceHandle);
}
