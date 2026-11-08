using System.Collections.Immutable;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Actions;

/// <summary>
/// CMU14: the Early Warning Joe's Signal wheels and RMC's saved hotbar order. A wheel ability belongs on the bar
/// only when the player put it there, and that choice is recorded in the saved order; so when the bar is
/// rebuilt from the order, every wheel ability the order does not place is treated as hidden. Pins therefore
/// survive re-sorts, re-attach and reconnects; everything else stays in its wheel.
/// </summary>
public sealed partial class RMCActionsSystem
{
    /// <summary>
    /// Rebuild the bar from the saved order on the next update. Called when the HUD reloads the bar's defaults
    /// (which leave wheel abilities out), so a pinned one comes back.
    /// </summary>
    public void CMURequestResort()
    {
        _sortEnt = null;
    }

    /// <summary>The same rule the re-sort applies, for tests.</summary>
    public ImmutableArray<EntProtoId> CMUHideUnpinnedWheelActions(
        IReadOnlyCollection<Entity<ActionComponent>> actions,
        ImmutableArray<EntProtoId> order,
        ImmutableArray<EntProtoId> hidden)
    {
        return HideUnpinnedWheelActions(actions, order, hidden);
    }

    private ImmutableArray<EntProtoId> HideUnpinnedWheelActions(
        IReadOnlyCollection<Entity<ActionComponent>> actions,
        ImmutableArray<EntProtoId> order,
        ImmutableArray<EntProtoId> hidden)
    {
        List<EntProtoId>? unpinned = null;
        foreach (var action in actions)
        {
            if (!HasComp<EWJoeAbilityGroupComponent>(action) || Prototype(action)?.ID is not { } id)
                continue;

            if (order.Contains(id) || hidden.Contains(id))
                continue;

            (unpinned ??= new List<EntProtoId>()).Add(id);
        }

        return unpinned == null ? hidden : hidden.AddRange(unpinned);
    }
}
