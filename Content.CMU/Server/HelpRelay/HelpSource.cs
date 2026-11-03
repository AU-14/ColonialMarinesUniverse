namespace Content.Server.CMU14.HelpRelay;

/// <summary>
/// The three help systems the relay delivers for. They stay separate systems; only their Discord
/// delivery is unified.
/// </summary>
public enum HelpSource
{
    AHelp,
    MHelp,
    BugHelp,
}

public static class HelpSourceExt
{
    /// <summary>Embed colors, one per source, stable across the whole family.</summary>
    public static int Color(this HelpSource source) => source switch
    {
        HelpSource.AHelp => 0xED4245,
        HelpSource.MHelp => 0xE67E22,
        _ => 0x5865F2,
    };

    public static string Label(this HelpSource source) => source switch
    {
        HelpSource.AHelp => "AHelp",
        HelpSource.MHelp => "MHelp",
        _ => "BugHelp",
    };
}
