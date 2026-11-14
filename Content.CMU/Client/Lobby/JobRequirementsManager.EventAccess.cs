namespace Content.Client.Players.PlayTimeTracking;

public sealed partial class JobRequirementsManager
{
    internal void RefreshEventAccess() => Updated?.Invoke();
}
