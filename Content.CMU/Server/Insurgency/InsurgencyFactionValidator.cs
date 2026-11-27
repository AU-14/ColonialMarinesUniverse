using System;
using System.Collections.Generic;
using Content.Shared.CMU14.Insurgency;

namespace Content.Server.CMU14.Insurgency;

/// <summary>Normalizes the equipment and economy limits applied to an insurgent cell.</summary>
public static class InsurgencyFactionValidator
{
    // Sane bounds for the economy rate. Kept here next to the other clamps so a future contributor
    // tunes them in one place.
    public const float MinDollarsToPointsRate = 0f;
    public const float MaxDollarsToPointsRate = 100f;

    /// <summary>
    ///     Returns a cleaned copy-in-place of the definition. The passed instance is mutated and
    ///     returned; callers use the result and discard the original reference.
    /// </summary>
    public static FactionDefinition Sanitize(FactionDefinition definition)
    {
        // Force the stored schema version to the one we understand right now.
        definition.SchemaVersion = FactionDefinition.SchemaVersionCurrent;

        var meta = definition.Metadata;
        meta.Title = Clamp(meta.Title, FactionDefinition.MaxTitleLength);
        meta.Description = Clamp(meta.Description, FactionDefinition.MaxDescriptionLength);
        meta.RoleplayText = Clamp(meta.RoleplayText, FactionDefinition.MaxRoleplayTextLength);
        meta.RecruitedMessage = Clamp(meta.RecruitedMessage, FactionDefinition.MaxRoleplayTextLength);
        CapCount(meta.OpposedGovforFactions, FactionDefinition.MaxOpposedGovforFactions);
        CapCount(meta.JobStatusIcons, FactionDefinition.MaxRoleLoadouts);

        definition.Economy.DollarsToPointsRate = Math.Clamp(
            definition.Economy.DollarsToPointsRate,
            MinDollarsToPointsRate,
            MaxDollarsToPointsRate);

        CapCount(definition.CellKit.PlaceableEntities, FactionDefinition.MaxPlaceableEntities);
        CapCount(definition.CellKit.VendorDefinitions, FactionDefinition.MaxVendorDefinitions);
        CapCount(definition.RoleLoadouts, FactionDefinition.MaxRoleLoadouts);
        CapCount(definition.Economy.PointsSubmissions, FactionDefinition.MaxPlaceableEntities);

        // Bound both conversion modes so negative values cannot mint points and extreme values cannot
        // overflow downstream arithmetic.
        foreach (var submission in definition.Economy.PointsSubmissions)
        {
            submission.AmountPerPoint = Math.Clamp(submission.AmountPerPoint, 1, FactionDefinition.MaxSubmissionRatio);
            submission.PointsPerItem = Math.Clamp(submission.PointsPerItem, 1, FactionDefinition.MaxSubmissionRatio);
        }

        foreach (var vendor in definition.CellKit.VendorDefinitions)
        {
            vendor.Name = Clamp(vendor.Name, FactionDefinition.MaxTitleLength);
            CapCount(vendor.Sections, FactionDefinition.MaxVendorSections);

            foreach (var section in vendor.Sections)
            {
                section.Name = Clamp(section.Name, FactionDefinition.MaxTitleLength);
                CapCount(section.Entries, FactionDefinition.MaxVendorEntries);

                section.Choices = section.Choices is { } choices
                    ? (Clamp(choices.Id, FactionDefinition.MaxTitleLength),
                        Math.Clamp(choices.Amount, 0, FactionDefinition.MaxVendorStock))
                    : null;
                section.SharedJOLimit = ClampNullable(section.SharedJOLimit, 0, FactionDefinition.MaxVendorStock);

                // Cell-kit stock uses the regular vendor inventory and the cell economy.
                section.TakeAll = null;
                section.TakeOne = null;
                section.SharedSpecLimit = null;
                section.Jobs.Clear();
                section.Ranks.Clear();
                section.Holidays.Clear();
                section.HasBoxes = false;

                foreach (var entry in section.Entries)
                {
                    entry.Name = entry.Name == null ? null : Clamp(entry.Name, FactionDefinition.MaxTitleLength);
                    entry.Points = ClampNullable(entry.Points, 0, FactionDefinition.MaxVendorPoints);
                    entry.Amount = ClampNullable(entry.Amount, 0, FactionDefinition.MaxVendorStock);
                    entry.Max = ClampNullable(entry.Max, 0, FactionDefinition.MaxVendorStock);
                    if (entry.Amount is { } amount && entry.Max is { } max && amount > max)
                        entry.Amount = max;
                    entry.Spawn = Math.Clamp(entry.Spawn, 1, FactionDefinition.MaxVendorSpawn);
                    entry.Multiplier = null;
                    entry.LinkedEntries.Clear();
                    entry.Box = null;
                    entry.BoxAmount = null;
                    entry.BoxSlots = null;
                    entry.GiveSquadRoleName = null;
                    entry.IsAppendSquadRoleName = false;
                    entry.GivePrefix = null;
                    entry.GiveIcon = null;
                    entry.GiveMapBlip = null;
                    entry.ReplaceSlot = null;
                }
            }
        }

        foreach (var loadout in definition.RoleLoadouts)
        {
            loadout.Role = Clamp(loadout.Role, FactionDefinition.MaxTitleLength);
            CapCount(loadout.Contents, FactionDefinition.MaxRoleLoadoutContents);
        }

        return definition;
    }

    private static string Clamp(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

    private static int? ClampNullable(int? value, int min, int max) =>
        value is { } present ? Math.Clamp(present, min, max) : null;

    private static void CapCount<T>(List<T> list, int max)
    {
        if (list.Count > max)
            list.RemoveRange(max, list.Count - max);
    }
}
