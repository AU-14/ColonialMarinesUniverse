using Content.Shared.CMU14.TacticalMap.Scout;

namespace Content.Client.CMU14.TacticalMap.Scout;

public static class CMUScoutReportPresentation
{
    public static string Label(CMUScoutReportInfo report, TimeSpan now)
    {
        return Loc.GetString("cmu-scout-report-label",
            ("type", CMUScoutReportInfo.ContactName(report.ContactType)),
            ("reporter", report.Reporter),
            ("age", Math.Max(0, (int) (now - report.ReportedAt).TotalSeconds)));
    }
}
