using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

[Serializable, NetSerializable]
public sealed partial class YautjaHarvestTrophyDoAfterEvent : SimpleDoAfterEvent
{
    public readonly YautjaTrophyKind Kind;

    public YautjaHarvestTrophyDoAfterEvent(YautjaTrophyKind kind)
    {
        Kind = kind;
    }

    public override DoAfterEvent Clone()
    {
        return new YautjaHarvestTrophyDoAfterEvent(Kind);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is YautjaHarvestTrophyDoAfterEvent trophy && trophy.Kind == Kind;
    }
}

[Serializable, NetSerializable]
public sealed partial class YautjaButcherDoAfterEvent : SimpleDoAfterEvent
{
    public readonly YautjaButcherKind Kind;
    public readonly int Stage;

    public YautjaButcherDoAfterEvent(YautjaButcherKind kind, int stage)
    {
        Kind = kind;
        Stage = stage;
    }

    public override DoAfterEvent Clone()
    {
        return new YautjaButcherDoAfterEvent(Kind, Stage);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is YautjaButcherDoAfterEvent butcher &&
               butcher.Kind == Kind &&
               butcher.Stage == Stage;
    }
}

[Serializable, NetSerializable]
public sealed partial class YautjaCleanserDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaHivebreakerDoAfterEvent : SimpleDoAfterEvent;
