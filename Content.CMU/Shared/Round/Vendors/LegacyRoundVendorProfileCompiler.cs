#nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Vendors;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU.Round;

/// <summary>
/// Compatibility compiler that detaches the currently supported legacy automated-vendor schema.
/// </summary>
public static class LegacyRoundVendorProfileCompiler
{
    /// <summary>
    /// Compiles one supported legacy vendor into immutable round-plan data.
    /// Unsupported authoring fails closed so it cannot be silently lost during migration.
    /// </summary>
    public static ResolvedRoundVendorProfile Compile(
        RoundForceId force,
        RoundSetupSlot slot,
        EntityPrototype prototype,
        IComponentFactory componentFactory,
        ResPath? baseRsi = null)
    {
        ArgumentNullException.ThrowIfNull(prototype);
        ArgumentNullException.ThrowIfNull(componentFactory);

        if (!force.IsValid)
            throw Invalid(prototype, "has no valid round force identity");
        if (slot is not (RoundSetupSlot.WeaponsVendor or
            RoundSetupSlot.VehicleCrewVendor or
            RoundSetupSlot.MilitaryDoctorVendor or
            RoundSetupSlot.JuniorOfficerVendor or
            RoundSetupSlot.RadioTelephoneOperatorVendor or
            RoundSetupSlot.MilitaryPoliceVendor or
            RoundSetupSlot.SectionSergeantVendor or
            RoundSetupSlot.SquadSergeantVendor or
            RoundSetupSlot.CombatTechnicianVendor or
            RoundSetupSlot.RiflemanVendor or
            RoundSetupSlot.SpecialWeaponsVendor or
            RoundSetupSlot.ShipsideUniformVendor or
            RoundSetupSlot.AutomaticRiflemanVendor or
            RoundSetupSlot.OperationsOfficerVendor))
            throw Invalid(prototype, $"cannot be compiled for unsupported setup slot '{slot}'");
        if (baseRsi == null &&
            slot is (RoundSetupSlot.ShipsideUniformVendor or
                RoundSetupSlot.AutomaticRiflemanVendor or
                RoundSetupSlot.OperationsOfficerVendor))
        {
            throw Invalid(prototype, $"has no resolved presentation for setup slot '{slot}'");
        }
        if (!prototype.TryGetComponent<CMAutomatedVendorComponent>(out var vendor, componentFactory))
            throw Invalid(prototype, "has no automated-vendor component");

        ValidateVendorDefaults(prototype, vendor);

        var sections = ImmutableArray.CreateBuilder<ResolvedRoundVendorSection>(vendor.Sections.Count);
        foreach (var section in vendor.Sections)
        {
            if (section == null)
                throw Invalid(prototype, "contains a null vendor section");
            ValidateSectionDefaults(prototype, section);
            if (string.IsNullOrWhiteSpace(section.Name))
                throw Invalid(prototype, "contains a section without a name");

            RoundVendorChoice? choice = null;
            if (section.Choices is { } authoredChoice)
            {
                if (string.IsNullOrWhiteSpace(authoredChoice.Id) || authoredChoice.Amount <= 0)
                    throw Invalid(prototype, $"section '{section.Name}' has an invalid choice allowance");
                choice = new RoundVendorChoice(authoredChoice.Id, authoredChoice.Amount);
            }

            var entries = ImmutableArray.CreateBuilder<ResolvedRoundVendorEntry>(section.Entries.Count);
            foreach (var entry in section.Entries)
            {
                if (entry == null)
                    throw Invalid(prototype, $"section '{section.Name}' contains a null entry");
                ValidateEntryDefaults(prototype, section, entry);
                if (string.IsNullOrWhiteSpace(entry.Id.Id))
                    throw Invalid(prototype, $"section '{section.Name}' contains an entry without a product");
                if (entry.Name is { } entryName && string.IsNullOrWhiteSpace(entryName))
                    throw Invalid(prototype, $"section '{section.Name}' product '{entry.Id}' has an invalid display name");
                if (entry.Amount is <= 0)
                    throw Invalid(prototype, $"section '{section.Name}' product '{entry.Id}' has an invalid amount");

                entries.Add(new ResolvedRoundVendorEntry(
                    entry.Id,
                    entry.Name,
                    entry.Amount,
                    entry.Points,
                    entry.Recommended,
                    entry.GiveSquadRoleName,
                    entry.IsAppendSquadRoleName,
                    entry.GivePrefix,
                    entry.IsAppendPrefix));
            }

            if (section.TakeAll is { } takeAll && string.IsNullOrWhiteSpace(takeAll))
                throw Invalid(prototype, $"section '{section.Name}' has an invalid take-all allowance");
            if (section.TakeOne is { } takeOne && string.IsNullOrWhiteSpace(takeOne))
                throw Invalid(prototype, $"section '{section.Name}' has an invalid take-one allowance");
            if (section.SharedSpecLimit is <= 0)
                throw Invalid(prototype, $"section '{section.Name}' has an invalid shared specialist limit");

            sections.Add(new ResolvedRoundVendorSection(
                section.Name,
                choice,
                section.TakeAll,
                section.TakeOne,
                section.SharedSpecLimit,
                entries.MoveToImmutable()));
        }

        if (vendor.PointsType is { } pointsType && string.IsNullOrWhiteSpace(pointsType))
            throw Invalid(prototype, "contains an invalid points type");
        if (vendor.Jobs.Any(job => string.IsNullOrWhiteSpace(job.Id)))
            throw Invalid(prototype, "contains an invalid job restriction");

        var access = CompileAccess(prototype, componentFactory);
        return new ResolvedRoundVendorProfile(
            force,
            slot,
            prototype.Name,
            prototype.Description,
            baseRsi,
            access,
            vendor.PointsType,
            vendor.Jobs.ToImmutableArray(),
            sections.MoveToImmutable());
    }

    private static ResolvedRoundVendorAccess CompileAccess(
        EntityPrototype prototype,
        IComponentFactory componentFactory)
    {
        if (!prototype.TryGetComponent<AccessReaderComponent>(out var access, componentFactory))
        {
            return new ResolvedRoundVendorAccess(
                true,
                ImmutableArray<ImmutableArray<ProtoId<AccessLevelPrototype>>>.Empty);
        }

        if (!access.Enabled ||
            access.DenyTags.Count != 0 ||
            access.AccessKeys.Count != 0 ||
            access.ContainerAccessProvider != null ||
            access.AccessLog.Count != 0 ||
            access.AccessLogLimit != 20 ||
            access.LoggingDisabled ||
            !access.BreakOnAccessBreaker)
        {
            throw Invalid(prototype, "uses unsupported access-reader authoring");
        }

        if (access.AccessLists.Count == 0)
            throw Invalid(prototype, "has an access reader without an access requirement");

        var groups = access.AccessLists
            .Select(group =>
            {
                if (group.Count == 0 || group.Any(level => string.IsNullOrWhiteSpace(level.Id)))
                    throw Invalid(prototype, "contains an invalid access requirement");

                return group
                    .OrderBy(level => level.Id, StringComparer.Ordinal)
                    .ToImmutableArray();
            })
            .OrderBy(group => string.Join('\u001f', group.Select(level => level.Id)), StringComparer.Ordinal)
            .ToImmutableArray();

        return new ResolvedRoundVendorAccess(false, groups);
    }

    private static void ValidateVendorDefaults(
        EntityPrototype prototype,
        CMAutomatedVendorComponent vendor)
    {
        if (vendor.Ranks.Count != 0 ||
            vendor.MinOffset != new Vector2(-0.2f, -0.2f) ||
            vendor.MaxOffset != new Vector2(0.2f, 0.2f) ||
            vendor.Hackable ||
            vendor.Hacked ||
            vendor.HackSkill != (EntProtoId<SkillDefinitionComponent>) "RMCSkillEngineer" ||
            vendor.HackSkillLevel != 2 ||
            vendor.HackDelay != TimeSpan.FromSeconds(10) ||
            vendor.Access.Count != 0 ||
            !vendor.Scaling ||
            vendor.RandomUnstockAmount != null ||
            vendor.RandomEmptyChance != null ||
            vendor.Sound != null ||
            vendor.BaseSprite != null ||
            vendor.AnimationSprite != null ||
            vendor.EjectContentsOnDestruction ||
            vendor.UseObjectivePoints ||
            !string.IsNullOrEmpty(vendor.Faction) ||
            vendor.CachedFactionWinPoints != 0 ||
            vendor.CanManualRestock ||
            vendor.IgnoreBulkRestockById.Count != 0 ||
            vendor.PartialProductStacks.Count != 0 ||
            vendor.RestockEntries.Count != 0 ||
            vendor.StackEntries.Count != 0)
        {
            throw Invalid(prototype, "uses unsupported automated-vendor authoring");
        }
    }

    private static void ValidateSectionDefaults(
        EntityPrototype prototype,
        CMVendorSection section)
    {
        if (section.SharedJOLimit != null ||
            section.Jobs.Count != 0 ||
            section.Ranks.Count != 0 ||
            section.Holidays.Count != 0 ||
            section.HasBoxes)
        {
            throw Invalid(prototype, $"section '{section.Name}' uses unsupported authoring");
        }
    }

    private static void ValidateEntryDefaults(
        EntityPrototype prototype,
        CMVendorSection section,
        CMVendorEntry entry)
    {
        if (entry.GiveSquadRoleName == null && entry.IsAppendSquadRoleName)
            throw Invalid(prototype, $"section '{section.Name}' product '{entry.Id}' appends a missing role name");
        if (entry.GivePrefix == null && entry.IsAppendPrefix)
            throw Invalid(prototype, $"section '{section.Name}' product '{entry.Id}' appends a missing prefix");

        if (entry.Spawn != 1 ||
            entry.Multiplier != null ||
            entry.Max != null ||
            entry.LinkedEntries.Count != 0 ||
            entry.Box != null ||
            entry.BoxAmount != null ||
            entry.BoxSlots != null ||
            entry.GiveIcon != null ||
            entry.GiveMapBlip != null ||
            entry.ReplaceSlot != null)
        {
            throw Invalid(
                prototype,
                $"section '{section.Name}' product '{entry.Id}' uses unsupported authoring");
        }
    }

    private static InvalidOperationException Invalid(EntityPrototype prototype, string reason)
    {
        return new InvalidOperationException($"Legacy vendor '{prototype.ID}' {reason}.");
    }
}
