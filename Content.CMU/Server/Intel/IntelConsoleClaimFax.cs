using System.Collections.Generic;

namespace Content.Server._CMU14.Intel;

public readonly record struct ClfRosterEntry(string Name, string Job);

public static class IntelConsoleClaimFax
{
    public static string BuildGovforFaxContent(IReadOnlyList<ClfRosterEntry> roster)
    {
        var rosterText = roster.Count == 0
            ? "No further operatives could be identified during the debrief."
            : BuildRosterList(roster);

        return "[head=3][color=#7a1f1f]⬢ MILITARY CONFIDENTIAL ⬢[/color][/head]\n" +
               "[color=#7a1f1f][italic]Provost Marshal Office | Counter-Intelligence Liaison Bulletin[/italic][/color]\n\n" +
               "[color=#7a1f1f]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/color]\n\n" +
               "[bold]Case File:[/bold] [color=#7a1f1f]PMO-CI-CLFHUM[/color]\n" +
               "[bold]Classification:[/bold] [color=#7a1f1f][bold]MILITARY CONFIDENTIAL | EYES ONLY[/bold][/color]\n" +
               "[bold]Distribution:[/bold] [italic]Colony Marshal Bureau // Military Command // Fleet Command[/italic]\n" +
               "[bold]Source:[/bold] [bold]HUMINT | Debriefed Personnel[/bold]\n" +
               "[color=#7a1f1f]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]\n" +
               "Command,\n" +
               "  A CLF base intelligence console has been seized. Personnel captured in the operation have" +
               " been relocated off-site for debriefing under Provost custody. HUMINT obtained during" +
               " interrogation has compromised the identities of the following remaining CLF operatives:\n\n" +
               rosterText + "\n\n" +
               "Signed,\n" +
               "[color=#7a1f1f][bolditalic]Provost Marshal Office | Counter-Intelligence Section[/bolditalic][/color]\n" +
               "[color=#7a1f1f]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]\n\n" +
               "[color=#7a1f1f]⬢ Unauthorized disclosure, duplication, or diversion of this bulletin from" +
               " designated channels constitutes a violation of Marine Law. ⬢[/color]";
    }

    private static string BuildRosterList(IReadOnlyList<ClfRosterEntry> roster)
    {
        var list = string.Empty;
        for (var i = 0; i < roster.Count; i++)
        {
            var entry = roster[i];
            list += $"- {entry.Name} — {entry.Job}";
            if (i < roster.Count - 1)
                list += "\n";
        }

        return list;
    }

    public static string BuildGovforAnnouncementText(IReadOnlyList<ClfRosterEntry> roster)
    {
        return roster.Count == 0
            ? "Provost advisory: a CLF base intel console has been seized and captured personnel" +
              " relocated for debriefing. No further operatives could be identified. Full report available" +
              " at the Military Command or Marshal's office fax."
            : " Provost advisory: a CLF base intel console has been seized and captured personnel" +
              " relocated for debriefing. HUMINT from the debrief has identified the remaining CLF operatives." +
              " Check the Military Command or Marshal's office fax for the full roster.";
    }

    public static string BuildColonyAnnouncementText()
    {
        return "Colony Marshal Bureau advisory: heightened security activity has been reported in the area." +
               " Colony residents are advised to remain vigilant and report any suspicious behavior to the nearest security checkpoint.";
    }
}
