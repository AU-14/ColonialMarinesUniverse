#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.AU14.Scenario;
using Content.Shared.CMU.Round;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Round;

public sealed partial class CMURoundDirectorSystem
{
    private CommittedRoundAsrsCatalogs? _committedAsrsCatalogs;

    /// <summary>
    /// Returns the immutable ASRS catalog committed for one side without consulting prototype data.
    /// </summary>
    internal bool TryGetCommittedAsrsCatalog(
        RoundSide side,
        [NotNullWhen(true)] out ResolvedRoundAsrsCatalog? catalog)
    {
        catalog = side switch
        {
            RoundSide.Govfor => _committedAsrsCatalogs?.Govfor,
            RoundSide.Opfor => _committedAsrsCatalogs?.Opfor,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown round side."),
        };

        return catalog != null;
    }

    private CommittedRoundAsrsCatalogs ResolveCommittedAsrsCatalogs(
        RoundPlanSelectionSnapshot selection)
    {
        var requiredForces = new HashSet<RoundForceId>();
        AddRequiredForce(selection.GovforAssignment, requiredForces);
        AddRequiredForce(selection.OpforAssignment, requiredForces);

        if (requiredForces.Count == 0)
            return new CommittedRoundAsrsCatalogs(null, null);

        var profiles = new Dictionary<RoundForceId, ProfileSource>();
        foreach (var prototype in _prototypes
                     .EnumeratePrototypes<EntityPrototype>()
                     .OrderBy(prototype => prototype.ID, StringComparer.Ordinal))
        {
            if (!prototype.TryGetComponent<RoundForceAsrsProfileComponent>(
                    out var profile,
                    _componentFactory) ||
                string.IsNullOrWhiteSpace(profile.ForceId))
            {
                continue;
            }

            var force = new RoundForceId(profile.ForceId);
            if (!requiredForces.Contains(force))
                continue;

            if (profiles.TryGetValue(force, out var existing))
            {
                throw new InvalidOperationException(
                    $"Round force '{force}' has multiple ASRS profiles: '{existing.PrototypeId}' and '{prototype.ID}'.");
            }

            profiles.Add(force, new ProfileSource(prototype.ID, profile));
        }

        var resolved = new Dictionary<RoundForceId, ResolvedRoundAsrsCatalog>();
        foreach (var force in requiredForces.OrderBy(force => force.Value, StringComparer.Ordinal))
        {
            if (!profiles.TryGetValue(force, out var source))
            {
                throw new InvalidOperationException(
                    $"Round force '{force}' has no ASRS profile.");
            }

            ResolvedRoundAsrsCatalog catalog;
            try
            {
                catalog = RoundForceAsrsProfileCompiler.Compile(source.Profile);
            }
            catch (Exception exception) when (
                exception is ArgumentException or RoundAsrsCatalogResolutionException)
            {
                throw new InvalidOperationException(
                    $"ASRS profile '{source.PrototypeId}' for round force '{force}' is invalid.",
                    exception);
            }

            ValidateCratePrototypes(source.PrototypeId, catalog);
            resolved.Add(force, catalog);
        }

        return new CommittedRoundAsrsCatalogs(
            GetResolvedCatalog(selection.GovforAssignment, resolved),
            GetResolvedCatalog(selection.OpforAssignment, resolved));
    }

    private void ValidateCratePrototypes(
        string profileId,
        ResolvedRoundAsrsCatalog catalog)
    {
        foreach (var category in catalog.Categories)
        {
            foreach (var offer in category.Offers)
            {
                if (_prototypes.TryIndex<EntityPrototype>(offer.Crate, out _))
                    continue;

                throw new InvalidOperationException(
                    $"ASRS profile '{profileId}' offer '{offer.Id}' references missing crate '{offer.Crate}'.");
            }
        }
    }

    private static void AddRequiredForce(
        RoundForceAssignment? assignment,
        ISet<RoundForceId> requiredForces)
    {
        if (assignment is { } assigned)
            requiredForces.Add(assigned.Force);
    }

    private static ResolvedRoundAsrsCatalog? GetResolvedCatalog(
        RoundForceAssignment? assignment,
        IReadOnlyDictionary<RoundForceId, ResolvedRoundAsrsCatalog> resolved)
    {
        return assignment is { } assigned
            ? resolved[assigned.Force]
            : null;
    }

    private readonly record struct CommittedRoundAsrsCatalogs(
        ResolvedRoundAsrsCatalog? Govfor,
        ResolvedRoundAsrsCatalog? Opfor);

    private readonly record struct ProfileSource(
        string PrototypeId,
        RoundForceAsrsProfileComponent Profile);
}
