using Content.Shared._RMC14.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Map;

// ReSharper disable once CheckNamespace
namespace Content.Client.Construction;

public sealed partial class ConstructionSystem
{
    [Dependency] private RMCConstructionSystem _rmcConstruction = default!;

    private bool RMCUserCanConstruct(EntityUid user)
    {
        return _rmcConstruction.CanConstruct(user);
    }

    private bool RMCCheckConstructionAttempt(
        ConstructionPrototype prototype,
        EntityCoordinates location,
        EntityUid user,
        bool showPopup)
    {
        var attempt = new RMCConstructionAttemptEvent(location, prototype, User: user);
        RaiseLocalEvent(ref attempt);

        if (!attempt.Cancelled)
            return true;

        if (showPopup && attempt.Popup is { } popup)
            _popupSystem.PopupCoordinates(popup, location);

        return false;
    }
}
