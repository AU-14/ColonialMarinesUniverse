using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Abilities;

public sealed partial class CMUWendigoPunchSwingActionEvent : InstantActionEvent;

public sealed partial class CMUClawedSweepActionEvent : WorldTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class CMUClawedSweepDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public Angle Direction;

    public CMUClawedSweepDoAfterEvent(Angle direction)
    {
        Direction = direction;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUClawedSweepDoAfterEvent(Direction);
    }
}
