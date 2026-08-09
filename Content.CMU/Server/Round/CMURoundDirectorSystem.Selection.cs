using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;

namespace Content.Server.AU14.Round;

/// <summary>
/// Result of attempting to change the candidate round selection.
/// </summary>
internal enum CMURoundSelectionMutationResult : byte
{
    Applied,
    SelectionFrozen,
}

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

        _round.ApplyLegacyForceSelection(side, platoon);
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
}
