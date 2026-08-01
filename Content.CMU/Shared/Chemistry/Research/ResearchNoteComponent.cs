using Content.Shared._AU14.Chemistry.Reagents;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

[RegisterComponent]
public sealed partial class ResearchNoteComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public GeneratedReagentData? Data;
    [ViewVariables(VVAccess.ReadOnly)]
    public List<string> Hints = [];
    [ViewVariables(VVAccess.ReadOnly)]
    public string PickedProperty = string.Empty;
    [ViewVariables]
    public int Tier = 0;
    [ViewVariables]
    public bool FullReport = false;
    [ViewVariables]
    public bool Grant = false;
    [ViewVariables]
    public int GrantReward = 0;
    [ViewVariables]
    public bool Contract = false;
    [ViewVariables]
    public ResearchNoteType NoteType = ResearchNoteType.Uninitialized;
}
