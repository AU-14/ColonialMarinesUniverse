using Content.Shared.Preferences;

namespace Content.Shared.CMU14.Preferences;

public static class CMUBuildTypes
{
    /// <summary>
    ///     Reads a saved build. Characters saved before the body shapes had one of eight written builds,
    ///     which become the closest shape.
    /// </summary>
    public static BuildType Parse(string? saved)
    {
        if (Enum.TryParse<BuildType>(saved, out var build) && Enum.IsDefined(build))
            return build;

        return saved switch
        {
            "Thin" => BuildType.ThinNoMuscle,
            "Lean" => BuildType.ThinLean,
            "Average" => BuildType.AverageLean,
            "Athletic" => BuildType.ThinRipped,
            "Muscular" => BuildType.AverageRipped,
            "Broad" => BuildType.LargeRipped,
            "Stocky" => BuildType.LargeLean,
            "Heavyset" => BuildType.LargeNoMuscle,
            _ => BuildType.AverageLean,
        };
    }
}
