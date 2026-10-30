using Content.Server.Popups;
using Content.Shared.CMU14.Dropship;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Dropship;

/// <summary>
///     Toggles <see cref="DropshipComponent.FireAlarmActive"/> when a
///     <see cref="CMUShipFireAlarmComponent"/> alarm is used. The alarm is
///     craft-wide: the button resolves its ship through its own grid, or
///     through the deck link on multi-deck ships.
/// </summary>
public sealed class CMUShipFireAlarmSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private EntityQuery<DropshipComponent> _shipQuery;
    private EntityQuery<DropshipDeckComponent> _deckQuery;

    public override void Initialize()
    {
        _shipQuery = GetEntityQuery<DropshipComponent>();
        _deckQuery = GetEntityQuery<DropshipDeckComponent>();
        SubscribeLocalEvent<CMUShipFireAlarmComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(Entity<CMUShipFireAlarmComponent> ent, ref InteractHandEvent args)
    {
        args.Handled = true;

        if (TryFindShip(Transform(ent).GridUid) is not { } ship)
        {
            _popup.PopupPredicted(Loc.GetString("cmu-ship-fire-alarm-not-installed"), ent, args.User);
            return;
        }

        var dropship = _shipQuery.Get(ship);
        dropship.Comp.FireAlarmActive = !dropship.Comp.FireAlarmActive;

        ent.Comp.Active = dropship.Comp.FireAlarmActive;
        _appearance.SetData(ent, CMUShipFireAlarmVisuals.Active, ent.Comp.Active);
        Dirty(ent, ent.Comp);

        _popup.PopupPredicted(
            Loc.GetString(dropship.Comp.FireAlarmActive ? "cmu-ship-fire-alarm-pulled" : "cmu-ship-fire-alarm-reset"),
            ent,
            args.User);
    }

    private EntityUid? TryFindShip(EntityUid? grid)
    {
        if (grid is not { } gridUid)
            return null;

        if (_shipQuery.TryComp(gridUid, out _))
            return gridUid;

        if (_deckQuery.TryComp(gridUid, out var deck)
            && _shipQuery.TryComp(deck.Ship, out _))
            return deck.Ship;

        return null;
    }
}
