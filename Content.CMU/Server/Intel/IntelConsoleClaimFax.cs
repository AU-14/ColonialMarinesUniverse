using System.Collections.Generic;

namespace Content.Server.CMU14.Intel;

public readonly record struct ClfRosterEntry(string Name, string Job);

public static class IntelConsoleClaimFax
{
    public static string BuildGovforFaxContent(IReadOnlyList<ClfRosterEntry> roster)
    {
        var rosterText = roster.Count == 0
            ? Loc.GetString("cmu-intel-console-fax-roster-none")
            : BuildRosterList(roster);

        return Loc.GetString("cmu-intel-console-fax-content", ("roster", rosterText));
    }

    private static string BuildRosterList(IReadOnlyList<ClfRosterEntry> roster)
    {
        var list = string.Empty;
        for (var i = 0; i < roster.Count; i++)
        {
            var entry = roster[i];
            list += Loc.GetString("cmu-intel-console-fax-roster-entry", ("name", entry.Name), ("job", entry.Job));
            if (i < roster.Count - 1)
                list += "\n";
        }

        return list;
    }

    public static string BuildGovforAnnouncementText(IReadOnlyList<ClfRosterEntry> roster)
    {
        return roster.Count == 0
            ? Loc.GetString("cmu-intel-console-govfor-announcement-empty")
            : Loc.GetString("cmu-intel-console-govfor-announcement");
    }

    public static string BuildColonyAnnouncementText() =>
        Loc.GetString("cmu-intel-console-colony-announcement");

    public static string BuildClfBroadcastText() =>
        Loc.GetString("cmu-intel-console-clf-broadcast");
}
