using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;

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
