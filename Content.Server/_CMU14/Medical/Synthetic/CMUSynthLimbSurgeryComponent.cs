using System.Collections.Generic;
using Content.Shared.Body.Part;

namespace Content.Server._CMU14.Medical.Synthetic;

[RegisterComponent]
public sealed partial class CMUSynthLimbSurgeryComponent : Component
{
    public readonly List<CMUSynthLimbSurgeryState> Slots = new();
}

public enum CMUSynthLimbSurgeryStage : byte
{
    ChassisOpen,
    WiringPrepped,
    LimbAttached,
}

public sealed class CMUSynthLimbSurgeryState
{
    public string SlotId = string.Empty;
    public EntityUid Parent;
    public BodyPartType Type;
    public BodyPartSymmetry Symmetry;
    public CMUSynthLimbSurgeryStage Stage;
}
