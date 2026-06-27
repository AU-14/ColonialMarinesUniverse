using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

[RegisterComponent]
public sealed partial class ResearchNoteSpawnerComponent : Component
{
    [DataField, ViewVariables]
    public string NoteType = string.Empty;
}
