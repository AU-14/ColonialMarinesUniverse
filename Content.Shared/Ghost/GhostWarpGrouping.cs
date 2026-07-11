using System.Linq;

namespace Content.Shared.Ghost;

public readonly record struct GhostWarpGroupingResult(string Tab, string Section);

public static class GhostWarpGrouping
{
    public const string TabMilitary = "Military";
    public const string TabXenos = "Xenos";
    public const string TabCorruptedHive = "Corrupted Hive";
    public const string TabOpfor = "OPFOR";
    public const string TabYautja = "Yautja";
    public const string TabThirdParty = "Third Party";
    public const string TabSurvivors = "Survivors";
    public const string TabWeYaPmc = "WeYa/PMC";
    public const string TabClf = "CLF";
    public const string TabSpp = "SPP";
    public const string TabTseRoyal = "TSE/Royal";
    public const string TabCmbProvost = "CMB/Provost";
    public const string TabThreat = "Threat";
    public const string TabCursed = "Cursed";
    public const string TabApe = "APE";
    public const string TabLocations = "Locations";
    public const string TabOther = "Other";

    public const string SectionAll = "All";
    public const string SectionCommand = "Command";
    public const string SectionHighCommand = "High Command";
    public const string SectionSquadLeads = "Squad Leads";
    public const string SectionSpecialists = "Specialists";
    public const string SectionPilotsCrew = "Pilots/Crew";
    public const string SectionLine = "Line";
    public const string SectionLinePersonnel = "Line Personnel";
    public const string SectionPersonnel = "Personnel";
    public const string SectionQueen = "Queen";
    public const string SectionUnknownTier = "Unknown Tier";
    public const string SectionHunters = "Hunters";
    public const string SectionThralls = "Thralls";
    public const string SectionAbominations = "Abominations";
    public const string SectionLeaders = "Leaders";
    public const string SectionMembers = "Members";
    public const string SectionSummoners = "Summoners";
    public const string SectionWarpPoints = "Warp Points";
    public const string SectionOther = "Other";

    private static readonly Dictionary<string, string> FactionTabs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RMCXeno"] = TabXenos,
        ["Xeno"] = TabXenos,
        ["UNMC"] = TabMilitary,
        ["GOVFOR"] = TabMilitary,
        ["RoyalMarines"] = TabTseRoyal,
        ["TSE"] = TabTseRoyal,
        ["SPP"] = TabSpp,
        ["WeYa"] = TabWeYaPmc,
        ["AUWeYu"] = TabWeYaPmc,
        ["Halcyon"] = TabWeYaPmc,
        ["CLF"] = TabClf,
        ["Bureau"] = TabCmbProvost,
        ["AUBureau"] = TabCmbProvost,
        ["Civilian"] = TabSurvivors,
        ["AUColonist"] = TabSurvivors,
        ["ColonySynth"] = TabSurvivors,
        ["OPFOR"] = TabOpfor,
        ["THREAT"] = TabThreat,
        ["CMUAPE"] = TabApe,
        ["CMUYautja"] = TabYautja,
    };

    private static readonly string[] FactionPriority =
    {
        "RMCXeno",
        "Xeno",
        "CMUYautja",
        "OPFOR",
        "UNMC",
        "GOVFOR",
        "RoyalMarines",
        "TSE",
        "SPP",
        "WeYa",
        "AUWeYu",
        "Halcyon",
        "CLF",
        "Bureau",
        "AUBureau",
        "Civilian",
        "AUColonist",
        "ColonySynth",
        "THREAT",
        "CMUAPE",
    };

    private static readonly HashSet<string> MarineDepartments = new(StringComparer.Ordinal)
    {
        "CMAuxiliarySupport",
        "CMCommand",
        "CMEngineering",
        "CMSquad",
        "CMMedbay",
        "CMMilitaryPolice",
        "CMRequisitions",
        "AU14DepartmentGovernmentForces",
    };

    public static GhostWarpGroupingResult Classify(
        bool isWarpPoint,
        string? jobId,
        string? departmentId,
        IEnumerable<string>? factions,
        bool isXeno,
        bool isYautja,
        bool isCorruptedHive,
        int? xenoTier,
        int realDisplayWeight,
        bool isYautjaThrall = false,
        bool isYautjaAbomination = false,
        bool isCursedSummoner = false)
    {
        if (isWarpPoint)
            return new GhostWarpGroupingResult(TabLocations, SectionWarpPoints);

        if (isCursedSummoner)
            return new GhostWarpGroupingResult(TabCursed, SectionSummoners);

        if ((isXeno || IsXenoJob(jobId)) && isCorruptedHive)
            return new GhostWarpGroupingResult(TabCorruptedHive, GetXenoSection(jobId, xenoTier));

        if (isXeno || IsXenoJob(jobId))
            return new GhostWarpGroupingResult(TabXenos, GetXenoSection(jobId, xenoTier));

        if (isYautja || HasFaction(factions, "CMUYautja"))
            return new GhostWarpGroupingResult(TabYautja, GetYautjaSection(isYautjaThrall, isYautjaAbomination));

        if (IsOpfor(jobId, departmentId, factions))
            return new GhostWarpGroupingResult(TabOpfor, GetOpforSection(jobId, realDisplayWeight));

        if (IsThirdParty(jobId, departmentId))
            return new GhostWarpGroupingResult(TabThirdParty, GetThirdPartySection(jobId, realDisplayWeight));

        if (TryGetFactionTab(factions, out var factionTab))
            return new GhostWarpGroupingResult(factionTab, GetFactionSection(factionTab, jobId, realDisplayWeight));

        if (departmentId == "CMSurvivor")
            return new GhostWarpGroupingResult(TabSurvivors, TabSurvivors);

        if (departmentId != null && MarineDepartments.Contains(departmentId))
            return new GhostWarpGroupingResult(TabMilitary, GetMilitarySection(jobId, realDisplayWeight));

        if (TryGetJobIdTab(jobId, out var jobTab))
            return new GhostWarpGroupingResult(jobTab, GetHumanSection(jobTab, realDisplayWeight));

        return new GhostWarpGroupingResult(TabOther, SectionPersonnel);
    }

    public static string GetCompactTabName(string tab)
    {
        return tab switch
        {
            TabMilitary => "MIL",
            TabXenos => "XENO",
            TabCorruptedHive => "HIVE",
            TabOpfor => "OPF",
            TabYautja => "YAU",
            TabThirdParty => "3P",
            TabSurvivors => "SURV",
            TabWeYaPmc => "PMC",
            TabTseRoyal => "TSE",
            TabCmbProvost => "CMB",
            TabCursed => "CUR",
            TabLocations => "LOC",
            TabOther => "OTH",
            _ => tab,
        };
    }

    private static bool IsXenoJob(string? jobId)
    {
        return jobId?.StartsWith("CMXeno", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string GetXenoSection(string? jobId, int? xenoTier)
    {
        if (jobId?.Contains("Queen", StringComparison.OrdinalIgnoreCase) == true)
            return SectionQueen;

        return xenoTier != null
            ? $"Tier {xenoTier}"
            : SectionUnknownTier;
    }

    private static string GetYautjaSection(bool isThrall, bool isAbomination)
    {
        if (isAbomination)
            return SectionAbominations;

        return isThrall ? SectionThralls : SectionHunters;
    }

    private static bool IsOpfor(string? jobId, string? departmentId, IEnumerable<string>? factions)
    {
        return departmentId == "AU14DepartmentOpfor" ||
               HasFaction(factions, "OPFOR") ||
               ContainsAny(jobId, "Opfor", "OPFOR");
    }

    private static string GetOpforSection(string? jobId, int realDisplayWeight)
    {
        return GetMilitaryRoleSection(jobId) ?? GetHumanSection(TabOpfor, realDisplayWeight);
    }

    private static string GetMilitarySection(string? jobId, int realDisplayWeight)
    {
        return GetMilitaryRoleSection(jobId) ?? GetHumanSection(TabMilitary, realDisplayWeight);
    }

    private static string? GetMilitaryRoleSection(string? jobId)
    {
        if (ContainsAny(jobId, "Pilot", "DCC", "Dropship", "Crew"))
            return SectionPilotsCrew;

        if (ContainsAny(
                jobId,
                "PlatCo",
                "PlatOp",
                "Advisor",
                "Commander",
                "CommandingOfficer",
                "ExecutiveOfficer",
                "StaffOfficer",
                "SeniorEnlistedAdvisor",
                "AuxiliarySupportOfficer",
                "ChiefMP",
                "ChiefMedicalOfficer",
                "ChiefEngineer",
                "Quartermaster",
                "Operations"))
        {
            return SectionCommand;
        }

        if (ContainsAny(
                jobId,
                "SectionSergeant",
                "SquadSergeant",
                "PlatoonSergeant",
                "SquadLeader",
                "FireteamLeader"))
        {
            return SectionSquadLeads;
        }

        if (ContainsAny(
                jobId,
                "CombatTech",
                "Corpsman",
                "AutomaticRifleman",
                "WeaponsSpecialist",
                "SmartGun",
                "RadioTelephoneOperator",
                "RTO",
                "MilitaryDoctor",
                "Doctor",
                "Nurse",
                "Researcher",
                "AuxTech",
                "Synth",
                "WorkingJoe",
                "K9",
                "IntelOfficer",
                "OrdnanceTech",
                "MaintTech",
                "MilitaryPolice",
                "MilitaryWarden"))
        {
            return SectionSpecialists;
        }

        if (ContainsAny(jobId, "Rifleman", "Recruit"))
            return SectionLine;

        return null;
    }

    private static bool IsThirdParty(string? jobId, string? departmentId)
    {
        return departmentId == "AU14DepartmentThirdParty" ||
               ContainsAny(jobId, "ThirdParty");
    }

    private static string GetThirdPartySection(string? jobId, int realDisplayWeight)
    {
        if (ContainsAny(jobId, "Leader"))
            return SectionLeaders;

        if (ContainsAny(jobId, "Member"))
            return SectionMembers;

        if (ContainsAny(jobId, "K9", "Specialist", "Synth", "Doctor", "Tech"))
            return SectionSpecialists;

        return realDisplayWeight >= 5 ? SectionLeaders : SectionOther;
    }

    private static string GetHumanSection(string tab, int realDisplayWeight)
    {
        if (tab == TabSurvivors)
            return TabSurvivors;

        if (realDisplayWeight >= 10)
            return SectionHighCommand;

        if (realDisplayWeight >= 5)
            return SectionCommand;

        if (realDisplayWeight >= 2)
            return SectionSpecialists;

        return realDisplayWeight > 0 ? SectionLinePersonnel : SectionPersonnel;
    }

    private static string GetFactionSection(string tab, string? jobId, int realDisplayWeight)
    {
        return tab == TabMilitary
            ? GetMilitarySection(jobId, realDisplayWeight)
            : GetHumanSection(tab, realDisplayWeight);
    }

    private static bool TryGetFactionTab(IEnumerable<string>? factions, out string tab)
    {
        if (factions == null)
        {
            tab = string.Empty;
            return false;
        }

        var factionList = factions
            .Where(faction => !string.IsNullOrWhiteSpace(faction))
            .ToList();

        foreach (var priority in FactionPriority)
        {
            if (!factionList.Any(faction => faction.Equals(priority, StringComparison.OrdinalIgnoreCase)))
                continue;

            tab = FactionTabs[priority];
            return true;
        }

        foreach (var faction in factionList)
        {
            if (!FactionTabs.TryGetValue(faction, out tab!))
                continue;

            return true;
        }

        tab = factionList.FirstOrDefault() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(tab);
    }

    private static bool TryGetJobIdTab(string? jobId, out string tab)
    {
        if (ContainsAny(jobId, "CLF"))
        {
            tab = TabClf;
            return true;
        }

        if (ContainsAny(jobId, "SPP"))
        {
            tab = TabSpp;
            return true;
        }

        if (ContainsAny(jobId, "PMC", "WeYa", "Corporate"))
        {
            tab = TabWeYaPmc;
            return true;
        }

        if (ContainsAny(jobId, "Bureau", "CMB", "Provost"))
        {
            tab = TabCmbProvost;
            return true;
        }

        if (ContainsAny(jobId, "Royal", "TSE"))
        {
            tab = TabTseRoyal;
            return true;
        }

        tab = string.Empty;
        return false;
    }

    private static bool HasFaction(IEnumerable<string>? factions, string faction)
    {
        return factions?.Any(value => value.Equals(faction, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool ContainsAny(string? value, params string[] needles)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
