using System;
using System.Collections.Generic;
using Content.Shared._AU14.Insurgency;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Insurgency;

/// <summary>
///     Server-side gate for untrusted faction definitions. Everything a client sends passes through
///     here before it is stored or applied: strings are clamped to the schema caps, list counts are
///     capped, and out-of-range numbers are pulled back into sane ranges.
///
///     This is the reusable untrusted-input seam the plan calls for. Phase 2 (Custom factions)
///     extends it to also recompute vendor costs from the server's own balance tables and reject
///     unknown prototype ids. It is written so the building-improvement buildsave upload can adopt
///     the same gate. Keep the caps in <see cref="FactionDefinition"/> as the single source of truth.
/// </summary>
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

                // These fields are not exposed by the faction editor. Normalize them so a crafted DTO cannot
                // smuggle job/rank restrictions, boxes, role changes, or special-vendor behavior into runtime.
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

    /// <summary>
    ///     Full untrusted-payload gate for Custom factions: clamps like <see cref="Sanitize"/>, then
    ///     drops any prototype id the server does not actually know. A Custom faction is authored on a
    ///     player's own machine, so it may reference entities/jobs/icons this server never loaded; those
    ///     references are pruned rather than trusted, so nothing the client sends can spawn an unknown
    ///     prototype. Mutates and returns the passed instance.
    /// </summary>
    public static FactionDefinition SanitizeCustom(FactionDefinition definition, IPrototypeManager prototypes)
    {
        Sanitize(definition);

        var meta = definition.Metadata;

        // Flag entity and status icon: clear the field if the id is unknown.
        if (meta.FlagEntity is { } flag && !prototypes.HasIndex<EntityPrototype>(flag.Id))
            meta.FlagEntity = null;
        if (meta.StatusIcon is { } icon && !prototypes.HasIndex<FactionIconPrototype>(icon.Id))
            meta.StatusIcon = null;
        if (meta.RecruitStatusIcon is { } recruitIcon && !prototypes.HasIndex<FactionIconPrototype>(recruitIcon.Id))
            meta.RecruitStatusIcon = null;

        // Per-job icon overrides: drop rows whose job or icon the server does not know.
        meta.JobStatusIcons.RemoveAll(j =>
            string.IsNullOrWhiteSpace(j.Role) ||
            !prototypes.HasIndex<JobPrototype>(j.Role) ||
            j.Icon is not { } jobIcon ||
            !prototypes.HasIndex<FactionIconPrototype>(jobIcon.Id));

        PruneEntities(definition.CellKit.PlaceableEntities, prototypes);

        // Drop submittable-for-points entries whose entity the server does not know.
        definition.Economy.PointsSubmissions.RemoveAll(s =>
            string.IsNullOrWhiteSpace(s.Entity.Id) || !prototypes.HasIndex<EntityPrototype>(s.Entity.Id));

        // A vendor whose base model is unknown could never spawn, so drop the whole definition.
        definition.CellKit.VendorDefinitions.RemoveAll(v =>
            string.IsNullOrWhiteSpace(v.BaseModel.Id) || !prototypes.HasIndex<EntityPrototype>(v.BaseModel.Id));

        foreach (var vendor in definition.CellKit.VendorDefinitions)
        {
            foreach (var section in vendor.Sections)
            {
                section.Entries.RemoveAll(e =>
                    string.IsNullOrWhiteSpace(e.Id.Id) || !prototypes.HasIndex<EntityPrototype>(e.Id.Id));
            }
        }

        foreach (var loadout in definition.RoleLoadouts)
            PruneEntities(loadout.Contents, prototypes);

        // Drop loadouts whose role is not a real job; their package could never be matched anyway.
        definition.RoleLoadouts.RemoveAll(l =>
            string.IsNullOrWhiteSpace(l.Role) || !prototypes.HasIndex<JobPrototype>(l.Role));

        return definition;
    }

    // Removes any entity id the server does not know from a placeable/contents list in place.
    private static void PruneEntities(List<EntProtoId> list, IPrototypeManager prototypes)
    {
        list.RemoveAll(e => !prototypes.HasIndex<EntityPrototype>(e.Id));
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
