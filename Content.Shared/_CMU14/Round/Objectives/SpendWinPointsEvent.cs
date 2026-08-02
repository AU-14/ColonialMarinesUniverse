namespace Content.Shared._CMU14.Round.Objectives;

public sealed class SpendWinPointsEvent : EntityEventArgs
{
    public string Team = string.Empty;
    public int Amount = 0;
}
