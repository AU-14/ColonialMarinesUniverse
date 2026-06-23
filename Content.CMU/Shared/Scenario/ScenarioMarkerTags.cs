namespace Content.Shared.AU14.Scenario;

public static class ScenarioMarkerTags
{
    public const string ForceHostile = "force:hostile";
    public const string ForceThirdParty = "force:third-party";
    public const string EntryParachute = "entry:parachute";
    public const string ForceClfSafehouse = "force:clf:safehouse";

    public static string Bucket(string bucket)
    {
        return $"bucket:{bucket}";
    }

    public static string MarkerId(string markerId)
    {
        return string.IsNullOrWhiteSpace(markerId)
            ? "marker-id:<generic>"
            : $"marker-id:{markerId}";
    }

    public static string ClfCivilianSpawn(string jobId)
    {
        return $"force:clf:civilian-spawn:{jobId}";
    }
}
