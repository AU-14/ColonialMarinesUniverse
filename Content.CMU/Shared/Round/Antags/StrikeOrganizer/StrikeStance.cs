namespace Content.Shared.CMU14.Round.Antags.StrikeOrganizer;

/// <summary>
/// The venture's standing toward an armed faction. Rolled per round on the rule and
/// binding for the organizer and every participant; only the grievance is free to reinterpret.
/// </summary>
public enum StrikeStance
{
    Aligned,
    Neutral,
    Opposed,
}

public static class StrikeStanceLocIds
{
    public static string Clf(StrikeStance stance)
        => $"cmu-strike-stance-clf-{stance.ToString().ToLowerInvariant()}";

    public static string Govfor(StrikeStance stance)
        => $"cmu-strike-stance-govfor-{stance.ToString().ToLowerInvariant()}";
}

public static class StrikeVentureText
{
    /// <summary>
    /// Localizes a venture into display strings. Unrolled petitions fall back to the
    /// generic grievance so no surface ever renders a raw LocId.
    /// </summary>
    public static (string Grievance, string Clf, string Govfor) Compose(
        ILocalizationManager loc, string? grievanceId, StrikeStance clf, StrikeStance govfor)
        => (loc.GetString(grievanceId ?? "cmu-strike-grievance-generic"),
            loc.GetString(StrikeStanceLocIds.Clf(clf)),
            loc.GetString(StrikeStanceLocIds.Govfor(govfor)));
}
