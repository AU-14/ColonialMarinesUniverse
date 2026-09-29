namespace Content.Shared._RMC14.LinkAccount;

public static class BoostyYautjaWhitelist
{
    public const string JobId = "CMUYautjaHunter";
    public const int MinPriority = 1;
    public const int MaxPriority = 4;

    public static bool IsAllowed(string jobId, int? priority)
    {
        return jobId == JobId && priority is >= MinPriority and <= MaxPriority;
    }
}
