#nullable enable

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Content.Server.AU14.Scenario;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Round;

public sealed partial class CMURoundDirectorSystem
{
    private static readonly ImmutableArray<(RoundSetupSlot Slot, PlatoonMarkerClass MarkerClass)> SupportedVendorSlots =
    [
        (RoundSetupSlot.WeaponsVendor, PlatoonMarkerClass.Weapons),
        (RoundSetupSlot.VehicleCrewVendor, PlatoonMarkerClass.VehicleCrew),
        (RoundSetupSlot.MilitaryDoctorVendor, PlatoonMarkerClass.MilitaryDoctor),
        (RoundSetupSlot.JuniorOfficerVendor, PlatoonMarkerClass.JuniorOfficer),
        (RoundSetupSlot.RadioTelephoneOperatorVendor, PlatoonMarkerClass.Rto),
        (RoundSetupSlot.MilitaryPoliceVendor, PlatoonMarkerClass.MilitaryPolice),
        (RoundSetupSlot.SectionSergeantVendor, PlatoonMarkerClass.SectionSergeant),
        (RoundSetupSlot.SquadSergeantVendor, PlatoonMarkerClass.SquadSergeant),
        (RoundSetupSlot.CombatTechnicianVendor, PlatoonMarkerClass.combattech),
        (RoundSetupSlot.RiflemanVendor, PlatoonMarkerClass.Rifleman),
        (RoundSetupSlot.SpecialWeaponsVendor, PlatoonMarkerClass.SWeapons),
    ];

    private CommittedRoundVendorProfiles? _committedVendorProfiles;

    /// <summary>
    /// Returns immutable vendor data committed for one side and semantic slot without consulting prototypes.
    /// </summary>
    internal bool TryGetCommittedVendorProfile(
        RoundSide side,
        RoundSetupSlot slot,
        [NotNullWhen(true)] out ResolvedRoundVendorProfile? profile)
    {
        if (!Enum.IsDefined(slot))
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown round setup slot.");

        var profiles = side switch
        {
            RoundSide.Govfor => _committedVendorProfiles?.Govfor,
            RoundSide.Opfor => _committedVendorProfiles?.Opfor,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown round side."),
        };

        if (profiles == null)
        {
            profile = null;
            return false;
        }

        return profiles.TryGetValue(slot, out profile);
    }

    private CommittedRoundVendorProfiles ResolveCommittedVendorProfiles(
        RoundPlanSelectionSnapshot selection)
    {
        return new CommittedRoundVendorProfiles(
            ResolveCommittedVendorProfiles(selection.GovforAssignment),
            ResolveCommittedVendorProfiles(selection.OpforAssignment));
    }

    private ImmutableDictionary<RoundSetupSlot, ResolvedRoundVendorProfile> ResolveCommittedVendorProfiles(
        RoundForceAssignment? assignment)
    {
        if (assignment is not { } assigned)
            return ImmutableDictionary<RoundSetupSlot, ResolvedRoundVendorProfile>.Empty;

        if (!_prototypes.TryIndex<PlatoonPrototype>(assigned.Force.Value, out var platoon))
        {
            throw new InvalidOperationException(
                $"Round force '{assigned.Force}' has no legacy platoon compatibility prototype.");
        }

        var profiles = ImmutableDictionary.CreateBuilder<RoundSetupSlot, ResolvedRoundVendorProfile>();
        foreach (var (slot, markerClass) in SupportedVendorSlots)
        {
            if (!TryResolveLegacyVendor(platoon, markerClass, out var vendorId))
            {
                throw new InvalidOperationException(
                    $"Round force '{assigned.Force}' does not resolve vendor slot '{slot}'.");
            }

            if (!_prototypes.TryIndex<EntityPrototype>(vendorId, out var vendor))
            {
                throw new InvalidOperationException(
                    $"Round force '{assigned.Force}' vendor slot '{slot}' references missing entity '{vendorId}'.");
            }

            var profile = LegacyRoundVendorProfileCompiler.Compile(
                assigned.Force,
                slot,
                vendor,
                _componentFactory);
            ValidateVendorProductPrototypes(profile, vendor.ID);
            profiles.Add(slot, profile);
        }

        return profiles.ToImmutable();
    }

    private bool TryResolveLegacyVendor(
        PlatoonPrototype platoon,
        PlatoonMarkerClass markerClass,
        out EntProtoId vendorId)
    {
        if (platoon.VendorOverrides.TryGetValue(markerClass, out vendorId))
            return true;

        if (platoon.VendorMarkersByClass.TryGetValue(markerClass, out vendorId))
            return true;

        if (platoon.VendorSet is { } vendorSetId)
        {
            if (!_prototypes.TryIndex<PlatoonVendorSetPrototype>(vendorSetId, out var vendorSet))
            {
                throw new InvalidOperationException(
                    $"Round force '{platoon.ID}' references missing vendor set '{vendorSetId}'.");
            }

            if (vendorSet.Vendors.TryGetValue(markerClass, out vendorId))
                return true;
        }

        vendorId = default;
        return false;
    }

    private void ValidateVendorProductPrototypes(
        ResolvedRoundVendorProfile profile,
        string sourcePrototypeId)
    {
        foreach (var job in profile.Jobs)
        {
            if (_prototypes.TryIndex<JobPrototype>(job, out _))
                continue;

            throw new InvalidOperationException(
                $"Legacy vendor '{sourcePrototypeId}' job restriction '{job}' does not exist.");
        }

        foreach (var section in profile.Sections)
        {
            foreach (var entry in section.Entries)
            {
                if (_prototypes.TryIndex<EntityPrototype>(entry.Product, out _))
                    continue;

                throw new InvalidOperationException(
                    $"Legacy vendor '{sourcePrototypeId}' product '{entry.Product}' does not exist.");
            }
        }
    }

    private readonly record struct CommittedRoundVendorProfiles(
        ImmutableDictionary<RoundSetupSlot, ResolvedRoundVendorProfile> Govfor,
        ImmutableDictionary<RoundSetupSlot, ResolvedRoundVendorProfile> Opfor);
}
