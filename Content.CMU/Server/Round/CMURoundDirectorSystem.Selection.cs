using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Round;

/// <summary>
/// Result of attempting to change the candidate round selection.
/// </summary>
internal enum CMURoundSelectionMutationResult : byte
{
    Applied,
    InvalidSelection,
    SelectionFrozen,
}

/// <summary>
/// Announces an accepted application of one side's candidate force assignment.
/// Applications remain observable when the force ID is unchanged to preserve the legacy refresh contract.
/// </summary>
[ByRefEvent]
internal readonly record struct RoundForceSelectionAppliedEvent(
    RoundSide Side,
    RoundForceId? PreviousForce,
    RoundForceId? CurrentForce);

public sealed partial class CMURoundDirectorSystem
{
    /// <summary>
    /// Resolves a committed typed assignment through the temporary legacy platoon projection.
    /// </summary>
    internal bool TryGetCommittedLegacyForce(
        RoundSide side,
        [NotNullWhen(true)] out PlatoonPrototype? platoon)
    {
        var assignment = side switch
        {
            RoundSide.Govfor => Selection?.GovforAssignment,
            RoundSide.Opfor => Selection?.OpforAssignment,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown round side."),
        };

        if (assignment is not { } committed)
        {
            platoon = null;
            return false;
        }

        return _prototypes.TryIndex(committed.Force.Value, out platoon);
    }

    /// <summary>
    /// Resolves the committed force after freeze or the director-controlled lobby candidate before freeze.
    /// </summary>
    internal bool TryGetLegacyForceProjection(
        RoundSide side,
        [NotNullWhen(true)] out PlatoonPrototype? platoon)
    {
        if (_state.Phase != CMURoundPhase.AwaitingSelection)
            return TryGetCommittedLegacyForce(side, out platoon);

        platoon = _round.GetLegacyForceSelection(side);
        return platoon != null;
    }

    /// <summary>
    /// Resolves the committed planet through the temporary legacy prototype component projection.
    /// </summary>
    internal bool TryGetCommittedLegacyPlanet(
        [NotNullWhen(true)] out RMCPlanetMapPrototypeComponent? planet)
    {
        planet = null;
        if (Selection?.PlanetId is not { } planetId ||
            string.IsNullOrWhiteSpace(planetId) ||
            !_prototypes.TryIndex<EntityPrototype>(planetId, out var prototype))
        {
            return false;
        }

        return prototype.TryComp(out planet, _componentFactory);
    }

    /// <summary>
    /// Resolves the committed planet after freeze or the director-controlled lobby candidate before freeze.
    /// </summary>
    internal bool TryGetLegacyPlanetProjection(
        [NotNullWhen(true)] out RMCPlanetMapPrototypeComponent? planet)
    {
        if (_state.Phase != CMURoundPhase.AwaitingSelection)
            return TryGetCommittedLegacyPlanet(out planet);

        planet = _round.GetSelectedPlanet();
        return planet != null;
    }

    /// <summary>
    /// Returns the committed planet identifier after freeze or the lobby candidate before freeze.
    /// </summary>
    internal string? GetLegacyPlanetIdProjection()
    {
        return _state.Phase == CMURoundPhase.AwaitingSelection
            ? _round.GetSelectedPlanetId()
            : Selection?.PlanetId;
    }

    /// <summary>
    /// Applies a legacy platoon projection to a side while the round selection is still mutable.
    /// </summary>
    internal CMURoundSelectionMutationResult TrySetLegacyForce(
        RoundSide side,
        PlatoonPrototype? platoon)
    {
        if (_state.Phase != CMURoundPhase.AwaitingSelection)
            return CMURoundSelectionMutationResult.SelectionFrozen;

        var previous = ToForceId(_round.GetLegacyForceSelection(side));
        _round.ApplyLegacyForceSelection(side, platoon);
        var applied = new RoundForceSelectionAppliedEvent(
            side,
            previous,
            ToForceId(platoon));
        RaiseLocalEvent(ref applied);
        return CMURoundSelectionMutationResult.Applied;
    }

    /// <summary>
    /// Applies a main ship to a side while the round selection is still mutable.
    /// </summary>
    internal CMURoundSelectionMutationResult TrySetMainShip(
        RoundSide side,
        string? shipId)
    {
        if (_state.Phase != CMURoundPhase.AwaitingSelection)
            return CMURoundSelectionMutationResult.SelectionFrozen;

        _round.ApplyMainShipSelection(side, shipId);
        return CMURoundSelectionMutationResult.Applied;
    }

    /// <summary>
    /// Applies a legacy planet projection while the round selection is still mutable.
    /// </summary>
    internal CMURoundSelectionMutationResult TrySetLegacyPlanet(string? planetId)
    {
        if (_state.Phase != CMURoundPhase.AwaitingSelection)
            return CMURoundSelectionMutationResult.SelectionFrozen;

        return _round.TryApplyLegacyPlanetSelection(planetId)
            ? CMURoundSelectionMutationResult.Applied
            : CMURoundSelectionMutationResult.InvalidSelection;
    }

    internal CMURoundSelectionMutationResult TrySetLegacyPlanet(
        string planetId,
        RMCPlanetMapPrototypeComponent planet)
    {
        if (_state.Phase != CMURoundPhase.AwaitingSelection)
            return CMURoundSelectionMutationResult.SelectionFrozen;

        _round.ApplyLegacyPlanetSelection(planetId, planet);
        return CMURoundSelectionMutationResult.Applied;
    }

    private static RoundForceId? ToForceId(PlatoonPrototype? platoon)
    {
        return platoon == null
            ? null
            : new RoundForceId(platoon.ID);
    }
}
