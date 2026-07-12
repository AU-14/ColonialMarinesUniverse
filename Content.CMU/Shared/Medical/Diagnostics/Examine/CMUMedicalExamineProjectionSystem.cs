using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Medical.Diagnostics.Examine;

/// <summary>
///     Owns the replicated wound examine read model and exposes read-only
///     queries to client-side examine systems.
/// </summary>
public sealed partial class CMUMedicalExamineProjectionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    /// <summary>
    ///     Returns the ordered public per-part projection without exposing its mutable backing list.
    /// </summary>
    public IReadOnlyList<CMUMedicalExaminePartProjection> GetParts(
        CMUMedicalExamineProjectionComponent projection)
        => projection.Parts;

    /// <summary>
    ///     Resolves one public part projection by canonical type and symmetry.
    /// </summary>
    public bool TryGetPart(
        CMUMedicalExamineProjectionComponent projection,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        out CMUMedicalExaminePartProjection part)
    {
        foreach (var candidate in projection.Parts)
        {
            if (candidate.Type != type || candidate.Symmetry != symmetry)
                continue;

            part = candidate;
            return true;
        }

        part = default;
        return false;
    }

    /// <summary>
    ///     Returns the public remaining-damage aggregate for a wound family.
    /// </summary>
    public FixedPoint2 GetRemainingDamage(
        CMUMedicalExamineProjectionComponent projection,
        WoundType type)
    {
        return type switch
        {
            WoundType.Burn => projection.BurnRemaining,
            WoundType.Brute or WoundType.Surgery => projection.BruteRemaining,
            _ => FixedPoint2.Zero,
        };
    }

    /// <summary>
    ///     Returns the highest externally visible bleeding tier in the compact projection.
    /// </summary>
    public ExternalBleedTier GetWorstExternalBleeding(CMUMedicalExamineProjectionComponent projection)
    {
        var worst = ExternalBleedTier.None;
        foreach (var part in projection.Parts)
        {
            if (part.ExternalBleeding > worst)
                worst = part.ExternalBleeding;
        }

        return worst;
    }

    internal void Replace(
        EntityUid body,
        List<CMUMedicalExaminePartProjection> parts,
        FixedPoint2 bruteRemaining,
        FixedPoint2 burnRemaining)
    {
        if (_net.IsClient)
            return;

        if (parts.Count == 0 &&
            bruteRemaining <= FixedPoint2.Zero &&
            burnRemaining <= FixedPoint2.Zero)
        {
            RemComp<CMUMedicalExamineProjectionComponent>(body);
            return;
        }

        var projection = EnsureComp<CMUMedicalExamineProjectionComponent>(body);
        if (SameProjection(projection, parts, bruteRemaining, burnRemaining))
            return;

        projection.Parts.Clear();
        projection.Parts.AddRange(parts);
        projection.BruteRemaining = bruteRemaining;
        projection.BurnRemaining = burnRemaining;
        Dirty(body, projection);
    }

    private static bool SameProjection(
        CMUMedicalExamineProjectionComponent current,
        List<CMUMedicalExaminePartProjection> next,
        FixedPoint2 bruteRemaining,
        FixedPoint2 burnRemaining)
    {
        if (current.BruteRemaining != bruteRemaining ||
            current.BurnRemaining != burnRemaining ||
            current.Parts.Count != next.Count)
        {
            return false;
        }

        for (var i = 0; i < next.Count; i++)
        {
            var currentPart = current.Parts[i];
            var nextPart = next[i];
            if (currentPart.Type != nextPart.Type ||
                currentPart.Symmetry != nextPart.Symmetry ||
                currentPart.ExternalBleeding != nextPart.ExternalBleeding ||
                currentPart.Wounds.Count != nextPart.Wounds.Count)
            {
                return false;
            }

            for (var j = 0; j < nextPart.Wounds.Count; j++)
            {
                if (currentPart.Wounds[j] != nextPart.Wounds[j])
                    return false;
            }
        }

        return true;
    }
}
