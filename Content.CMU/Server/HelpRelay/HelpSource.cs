namespace Content.Server.CMU14.HelpRelay;

/// <summary>
/// The sources the relay delivers for: the help systems plus the round-end news digest. They stay
/// separate systems; only their Discord delivery is unified.
/// </summary>
public enum HelpSource
{
    AHelp,
    MHelp,
    BugHelp,
    News,
}

public static class HelpSourceExt
{
    /// <summary>Embed colors, one per source, stable across the whole family.</summary>
    public static int Color(this HelpSource source) => source switch
    {
        HelpSource.AHelp => 0xED4245,
        HelpSource.MHelp => 0xE67E22,
        HelpSource.BugHelp => 0x5865F2,
        _ => 0x7CFC00,
    };

    public static string Label(this HelpSource source) => source switch
    {
        HelpSource.AHelp => "AHelp",
        HelpSource.MHelp => "MHelp",
        HelpSource.BugHelp => "BugHelp",
        _ => "News",
    };
}
