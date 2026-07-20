using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Construction.Prototypes;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Stacks;
using Robust.Shared.Map;

// ReSharper disable once CheckNamespace
namespace Content.Server.Construction;

public sealed partial class ConstructionSystem
{
    [Dependency] private RMCConstructionSystem _rmcConstruction = default!;

    private bool TryRMCConstructionPrototype(
        string id,
        [NotNullWhen(true)] out ConstructionPrototype? prototype)
    {
        return ProtoMan.TryCM(id, out prototype);
    }

    private bool TryRMCConstructionGraph(
        string id,
        [NotNullWhen(true)] out ConstructionGraphPrototype? graph)
    {
        return ProtoMan.TryCM(id, out graph);
    }

    private bool RMCUserCanConstruct(EntityUid user)
    {
        return _rmcConstruction.CanConstruct(user);
    }

    /// <summary>
    /// Routes construction-menu recipes backed by an RMC material recipe through the RMC build lifecycle.
    /// </summary>
    /// <returns>Whether this was an RMC recipe. <paramref name="result"/> is the build result when handled.</returns>
    private bool TryStartRMCConstruction(
        ConstructionPrototype construction,
        EntityUid user,
        out bool result)
    {
        result = false;
        if (construction.RMCPrototype is not { } rmcPrototype)
            return false;

        Entity<RMCConstructionItemComponent>? bestItem = null;
        var bestStackAmount = -1;

        foreach (var held in _handsSystem.EnumerateHeld(user))
        {
            if (!TryComp(held, out RMCConstructionItemComponent? constructionItem))
                continue;

            if (constructionItem.Buildable is { } buildable && !buildable.Contains(rmcPrototype))
                continue;

            var stackAmount = TryComp(held, out StackComponent? stack)
                ? stack.Count
                : int.MaxValue;

            if (bestItem != null && stackAmount <= bestStackAmount)
                continue;

            bestItem = (held, constructionItem);
            bestStackAmount = stackAmount;
        }

        if (bestItem is { } item)
        {
            result = _rmcConstruction.Build(item, user, rmcPrototype, 1);
            return true;
        }

        _popup.PopupEntity(Loc.GetString("construction-system-construct-no-materials"), user, user);
        return true;
    }

    private bool RMCCheckConstructionAttempt(
        ConstructionPrototype construction,
        EntityCoordinates location,
        EntityUid user)
    {
        var attempt = new RMCConstructionAttemptEvent(location, construction, User: user);
        RaiseLocalEvent(ref attempt);

        if (!attempt.Cancelled)
            return true;

        if (attempt.Popup is { } popup)
            _popup.PopupCoordinates(popup, location, user);

        return false;
    }
}
