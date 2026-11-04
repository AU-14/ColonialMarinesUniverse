using Content.Shared.FixedPoint;

namespace Content.Shared.CMU14.Round.Objectives;

public sealed class SpendWinPointsEvent : EntityEventArgs
{
    public string Team = string.Empty;
    public FixedPoint2 Amount = FixedPoint2.Zero;
}
