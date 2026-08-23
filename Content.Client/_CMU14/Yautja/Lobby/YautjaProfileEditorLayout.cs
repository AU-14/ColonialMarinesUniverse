using System.Collections.Generic;
using Content.Shared._CMU14.Yautja;

namespace Content.Client._CMU14.Yautja.Lobby;

public enum YautjaProfileEditorCategory
{
    Appearance,
    Equipment,
    Sets,
    Technology,
    Description,
}

public sealed record YautjaProfileEditorCategoryInfo(
    YautjaProfileEditorCategory Id,
    string LocalizationKey);

public sealed record YautjaProfileEditorSummary(
    string Set,
    string Armor,
    string Mask,
    string Greaves,
    string Cape,
    string Bracer,
    string Caster);

public sealed record YautjaProfileEditorSelection(
    YautjaUniqueSet Unique,
    YautjaLegacySet Legacy,
    YautjaGearMaterial ArmorMaterial,
    int ArmorStyle,
    YautjaGearMaterial MaskMaterial,
    int MaskStyle,
    YautjaGearMaterial GreavesMaterial,
    int GreavesStyle,
    YautjaCapeStyle CapeStyle,
    YautjaBracerMaterial BracerMaterial,
    YautjaBracerMaterial CasterMaterial);

public static class YautjaProfileEditorLayout
{
    public const int TechOptionSpacing = 6;
    public const int TechOptionBottomMargin = 12;
    public const float HorizontalWorkAreaMinWidth = 750;

    public static IReadOnlyList<YautjaProfileEditorCategoryInfo> Categories { get; } =
    [
        new(YautjaProfileEditorCategory.Appearance, "cmu-yautja-lobby-category-appearance"),
        new(YautjaProfileEditorCategory.Equipment, "cmu-yautja-lobby-category-equipment"),
        new(YautjaProfileEditorCategory.Sets, "cmu-yautja-lobby-category-sets"),
        new(YautjaProfileEditorCategory.Technology, "cmu-yautja-lobby-category-technology"),
        new(YautjaProfileEditorCategory.Description, "cmu-yautja-lobby-category-description"),
    ];

    public static bool IsUniqueSetLocked(YautjaCharacterProfile profile, YautjaUniqueSet unique)
    {
        return unique != YautjaUniqueSet.None && !YautjaRankResolver.CanUseUnique(profile);
    }

    public static bool IsUniqueSetLocked(YautjaProfileCapabilities capabilities, YautjaUniqueSet unique)
    {
        return unique != YautjaUniqueSet.None && !capabilities.CanUseUnique;
    }

    public static bool IsLegacySetLocked(YautjaProfileCapabilities capabilities, YautjaLegacySet legacy)
    {
        return !capabilities.CanUseLegacySet(legacy);
    }

    public static bool IsCapeLocked(YautjaProfileCapabilities capabilities, YautjaCapeStyle cape)
    {
        return !capabilities.CanUseCape(cape);
    }

    public static bool IsBracerLocked(YautjaProfileCapabilities capabilities, YautjaBracerMaterial bracer)
    {
        return !capabilities.CanUseBracer(bracer);
    }

    public static YautjaProfileEditorSummary BuildSummary(YautjaCharacterProfile profile)
    {
        var selection = GetSummarySelection(profile);
        var set = selection.Unique != YautjaUniqueSet.None
            ? YautjaCharacterProfile.GetUniqueDisplayName(selection.Unique)
            : selection.Legacy != YautjaLegacySet.None
                ? YautjaCharacterProfile.GetLegacyDisplayName(selection.Legacy)
                : "—";

        return new YautjaProfileEditorSummary(
            set,
            YautjaCharacterProfile.GetArmorStyleDisplayName(selection.ArmorMaterial, selection.ArmorStyle),
            YautjaCharacterProfile.GetMaskStyleDisplayName(selection.MaskMaterial, selection.MaskStyle),
            YautjaCharacterProfile.GetGreavesStyleDisplayName(selection.GreavesMaterial, selection.GreavesStyle),
            YautjaCharacterProfile.GetCapeDisplayName(selection.CapeStyle),
            YautjaCharacterProfile.GetBracerDisplayName(selection.BracerMaterial),
            YautjaCharacterProfile.GetCasterDisplayName(selection.CasterMaterial));
    }

    public static YautjaProfileEditorSelection GetSummarySelection(YautjaCharacterProfile profile)
    {
        return new YautjaProfileEditorSelection(
            profile.Unique,
            profile.Legacy,
            profile.ArmorMaterial,
            profile.ArmorStyle,
            profile.MaskMaterial,
            profile.MaskStyle,
            profile.GreavesMaterial,
            profile.GreavesStyle,
            profile.CapeStyle,
            profile.BracerMaterial,
            profile.CasterMaterial);
    }

    public static bool IsCategoryActive(
        YautjaProfileEditorCategory active,
        YautjaProfileEditorCategory candidate)
    {
        return active == candidate;
    }

    public static int GetResponsiveColumnCount(float availableWidth, int preferredColumns)
    {
        const float cardWidth = 108;
        const float separation = 8;

        if (preferredColumns <= 0 || availableWidth <= 0)
            return 1;

        var columns = (int) MathF.Floor((availableWidth + separation) / (cardWidth + separation));
        return Math.Clamp(columns, 1, preferredColumns);
    }

    public static bool ShouldStackWorkArea(float availableWidth)
    {
        return availableWidth < HorizontalWorkAreaMinWidth;
    }
}
