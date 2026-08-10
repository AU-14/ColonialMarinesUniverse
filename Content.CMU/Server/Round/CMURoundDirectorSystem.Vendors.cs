#nullable enable

using System.Diagnostics.CodeAnalysis;
using Content.Server.AU14.Scenario;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Round;

public sealed partial class CMURoundDirectorSystem
{
    private CommittedRoundVendorProfiles? _committedVendorProfiles;

    /// <summary>
    /// Returns immutable vendor data committed for one side and semantic slot without consulting prototypes.
    /// </summary>
    internal bool TryGetCommittedVendorProfile(
        RoundSide side,
        RoundSetupSlot slot,
        [NotNullWhen(true)] out ResolvedRoundVendorProfile? profile)
    {
        if (slot != RoundSetupSlot.WeaponsVendor)
        {
            if (!Enum.IsDefined(slot))
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown round setup slot.");

            profile = null;
            return false;
        }

        profile = side switch
        {
            RoundSide.Govfor => _committedVendorProfiles?.GovforWeapons,
            RoundSide.Opfor => _committedVendorProfiles?.OpforWeapons,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown round side."),
        };

        return profile != null;
    }

    private CommittedRoundVendorProfiles ResolveCommittedVendorProfiles(
        RoundPlanSelectionSnapshot selection)
    {
        return new CommittedRoundVendorProfiles(
            ResolveCommittedVendorProfile(selection.GovforAssignment, RoundSetupSlot.WeaponsVendor),
            ResolveCommittedVendorProfile(selection.OpforAssignment, RoundSetupSlot.WeaponsVendor));
    }

    private ResolvedRoundVendorProfile? ResolveCommittedVendorProfile(
        RoundForceAssignment? assignment,
        RoundSetupSlot slot)
    {
        if (assignment is not { } assigned)
            return null;

        if (!_prototypes.TryIndex<PlatoonPrototype>(assigned.Force.Value, out var platoon))
        {
            throw new InvalidOperationException(
                $"Round force '{assigned.Force}' has no legacy platoon compatibility prototype.");
        }

        var markerClass = slot switch
        {
            RoundSetupSlot.WeaponsVendor => PlatoonMarkerClass.Weapons,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unsupported round vendor slot."),
        };

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
        return profile;
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
        ResolvedRoundVendorProfile? GovforWeapons,
        ResolvedRoundVendorProfile? OpforWeapons);
}
