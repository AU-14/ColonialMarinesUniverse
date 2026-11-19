using Content.Shared.Mind;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// ARCH-02: raised directed on each unfinished Early Warning objective of the current offer, about once a
/// second. A condition system that can tell its objective can no longer be finished sets
/// <see cref="Possible"/> to false, and the ladder withdraws it and re-offers a slot after
/// <c>OfferRetryDelay</c>. Conditions with no answer leave it true (kill counts, the upstream kill-person).
/// <see cref="Maps"/> is the Joe's planet (the z-level network it stands on), already resolved.
/// </summary>
[ByRefEvent]
public record struct EWJoeObjectiveStillPossibleEvent(
    EntityUid MindId,
    MindComponent Mind,
    EntityUid Owner,
    HashSet<MapId> Maps,
    bool Possible = true);
