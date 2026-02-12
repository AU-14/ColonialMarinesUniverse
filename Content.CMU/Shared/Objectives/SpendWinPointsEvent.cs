using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives;

public sealed class SpendWinPointsEvent : EntityEventArgs
{
    public string Team = string.Empty;
    public int Amount = 0;
}
