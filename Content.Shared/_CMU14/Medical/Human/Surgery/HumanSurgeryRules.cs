using System;

namespace Content.Shared._CMU14.Medical.Human.Surgery;

public static class HumanSurgeryRules
{
    public const float SelfSurgerySlowdown = 1.5f;

    public static float GetToolMultiplier(SurgeryToolQuality quality)
    {
        return quality switch
        {
            SurgeryToolQuality.Ideal => 1f,
            SurgeryToolQuality.Suboptimal => 1.2f,
            SurgeryToolQuality.Substitute => 1.4f,
            SurgeryToolQuality.BadSubstitute => 1.6f,
            SurgeryToolQuality.Awful => 1.8f,
            _ => 1f,
        };
    }

    public static float GetSurfaceMultiplier(SurgerySurfaceQuality quality)
    {
        return quality switch
        {
            SurgerySurfaceQuality.Ideal => 1f,
            SurgerySurfaceQuality.Adequate => 1.33f,
            SurgerySurfaceQuality.Unsuited => 1.67f,
            SurgerySurfaceQuality.Awful => 2f,
            _ => 2f,
        };
    }

    public static int GetFailureChance(
        SurgeryToolQuality toolQuality,
        SurgerySurfaceQuality surfaceQuality,
        int surgerySkill,
        bool usesSurface)
    {
        var penalties = GetToolFailurePenalty(toolQuality);
        if (usesSurface)
            penalties += GetSurfaceFailurePenalty(surfaceQuality);

        penalties -= GetSkillFailureReduction(surgerySkill);
        return GetFailureChance(penalties);
    }

    public static int GetFailureChance(int failurePenalties)
    {
        if (failurePenalties <= 0)
            return 0;
        if (failurePenalties == 1)
            return 5;
        if (failurePenalties == 2)
            return 25;

        return 50;
    }

    public static int GetPainFailureChance(
        SurgeryPainRequirement requirement,
        bool anesthetized,
        bool conscious,
        int painReduction)
    {
        if (anesthetized || !conscious)
            return 0;

        return Math.Max(0, ((int) requirement - painReduction) * 2);
    }

    public static TimeSpan GetStepDuration(
        TimeSpan baseDelay,
        SurgeryToolQuality toolQuality,
        SurgerySurfaceQuality surfaceQuality,
        float skillMultiplier,
        bool usesSurface,
        bool selfSurgery)
    {
        var seconds = baseDelay.TotalSeconds;
        seconds *= skillMultiplier;
        seconds *= GetToolMultiplier(toolQuality);

        if (usesSurface)
            seconds *= GetSurfaceMultiplier(surfaceQuality);
        if (selfSurgery)
            seconds *= SelfSurgerySlowdown;

        return TimeSpan.FromSeconds(seconds);
    }

    private static int GetToolFailurePenalty(SurgeryToolQuality quality)
    {
        return quality switch
        {
            SurgeryToolQuality.BadSubstitute => 1,
            SurgeryToolQuality.Awful => 2,
            _ => 0,
        };
    }

    private static int GetSurfaceFailurePenalty(SurgerySurfaceQuality quality)
    {
        return quality switch
        {
            SurgerySurfaceQuality.Unsuited => 1,
            SurgerySurfaceQuality.Awful => 2,
            _ => 0,
        };
    }

    private static int GetSkillFailureReduction(int surgerySkill)
    {
        if (surgerySkill >= 3)
            return 3;
        if (surgerySkill >= 2)
            return 1;

        return 0;
    }
}
