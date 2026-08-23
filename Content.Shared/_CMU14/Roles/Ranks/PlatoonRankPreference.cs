using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CMU14.Roles.Ranks;

/// <summary>
/// Rank preference options for a single job, broken down per platoon. Client-only data holder,
/// not a networked BUI state - populated locally in HumanoidProfileEditor.
/// </summary>
public sealed class PlatoonRankPreferenceJobEntry
{
    public string JobId;
    public string JobName;
    public List<PlatoonRankOptions> Platoons;

    public PlatoonRankPreferenceJobEntry(string jobId, string jobName, List<PlatoonRankOptions> platoons)
    {
        JobId = jobId;
        JobName = jobName;
        Platoons = platoons;
    }
}

/// <summary>
/// The selectable ranks for a job under one specific platoon's chevron map - either that
/// platoon's override, or the job's base chevrons if the platoon has no override for it.
/// </summary>
public sealed class PlatoonRankOptions
{
    public string PlatoonId;
    public string PlatoonName;

    /// <summary>
    /// Optional path to the platoon patch texture shown in the tab header.
    /// Example: /Textures/_AU14/Clothing/Patches/uscm_patch.rsi/icon.png
    /// </summary>
    public ResPath? PatchPath;

    public List<RankOption> Ranks;

    public PlatoonRankOptions(string platoonId, string platoonName, ResPath? patchPath, List<RankOption> ranks)
    {
        PlatoonId = platoonId;
        PlatoonName = platoonName;
        PatchPath = patchPath;
        Ranks = ranks;
    }
}

public sealed class RankOption
{
    public string RankId;
    public string RankName;
    public string? Paygrade;
    public bool Unlocked;
    public string? RequirementsText;
    public EntProtoId? ChevronEntity;

    public RankOption(
        string rankId,
        string rankName,
        string? paygrade,
        bool unlocked,
        string? requirementsText,
        EntProtoId? chevronEntity)
    {
        RankId = rankId;
        RankName = rankName;
        Paygrade = paygrade;
        Unlocked = unlocked;
        RequirementsText = requirementsText;
        ChevronEntity = chevronEntity;
    }
}