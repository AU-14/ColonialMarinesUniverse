using Content.Shared._AU14.Chemistry.Reagents;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

[RegisterComponent]
public sealed partial class ResearchReportComponent : Component
{
    public GeneratedReagentData Data;
    public bool Completed = false;
    public bool Valid = true;
}
