using Content.Shared._AU14.Chemistry.Reagents;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

[RegisterComponent]
public sealed partial class ResearchReportComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public GeneratedReagentData? Data;
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Completed = false;
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Valid = true;
}
