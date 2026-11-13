using Content.Shared.CMU14.Threats;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Threats;

/// <summary>
///     Raised (broadcast) when a voted threat with <see cref="ThreatPrototype.ReserveLeftoverVoters"/> has
///     finished seating its voters. <see cref="Voters"/> are the consenting, still-eligible voters who got no
///     body. They have not been respawned and their round-join block is already lifted.
/// </summary>
[ByRefEvent]
public readonly record struct ThreatLeftoverVotersEvent(
    ProtoId<ThreatPrototype> ThreatId,
    IReadOnlyList<NetUserId> Voters);
