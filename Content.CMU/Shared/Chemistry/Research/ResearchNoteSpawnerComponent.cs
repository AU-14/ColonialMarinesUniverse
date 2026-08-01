using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

[RegisterComponent]
public sealed partial class ResearchNoteSpawnerComponent : Component
{
    [DataField, ViewVariables]
    public ResearchNoteType NoteType = ResearchNoteType.Uninitialized;
}


[Serializable, NetSerializable]
public enum ResearchNoteType : sbyte
{
    Uninitialized = -1,
    Synthesis = 0,
    Test = 1,
    Grant = 2,
    CiphHint = 3,
    CiphHintComplete = 4,
    LegHint = 5
}
