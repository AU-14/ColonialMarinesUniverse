using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Access.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Database;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Timing; // CMU14

namespace Content.Shared._RMC14.Access;

public sealed partial class IdCardSystem : EntitySystem
{
    private static readonly TimeSpan OwnerSweepEvery = TimeSpan.FromSeconds(5); // CMU14

    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!; // CMU14

    private TimeSpan _nextOwnerSweep; // CMU14

    public override void Initialize()
    {
        SubscribeLocalEvent<IdCardComponent, UseInHandEvent>(OnUseInHand, before: [typeof(ClothingSystem)] );
        SubscribeLocalEvent<IdCardComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<MarineComponent, InteractUsingEvent>(OnInteractUsing);
    }

    // CMU14: cards outlive their owner; a dangling OriginalOwner/Id errors on every PVS state send until swept
    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextOwnerSweep)
            return;

        _nextOwnerSweep = _timing.CurTime + OwnerSweepEvery;
        var cards = EntityQueryEnumerator<IdCardComponent>();
        while (cards.MoveNext(out var uid, out var card))
        {
            if (card.OriginalOwner is { } owner && TerminatingOrDeleted(owner))
            {
                card.OriginalOwner = null;
                Dirty(uid, card);
            }

            if (TryComp<IdCardOwnerComponent>(uid, out var cardOwner) &&
                TerminatingOrDeleted(cardOwner.Id))
            {
                cardOwner.Id = EntityUid.Invalid;
                Dirty(uid, cardOwner);
            }
        }
    }

    private void OnInteractUsing(Entity<MarineComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(args.Used, out IdCardComponent? idCard) || idCard.OriginalOwner != null)
            return;
        idCard.OriginalOwner = args.Target;
        var popupMessage = $"{Name(args.User)} bound an ID to {Name(args.Target)}.";
        _popup.PopupPredicted(popupMessage, args.Target, args.User, PopupType.Small);
        _adminLogger.Add(LogType.RMCIdModify,
            LogImpact.High,
            $"{ToPrettyString(args.User):player} has bound the ID {ToPrettyString(args.Used):entity} to {ToPrettyString(args.Target):player}");
        args.Handled = true;
        Dirty(ent);
    }

    private void OnExamine(Entity<IdCardComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.OriginalOwner == null)
        {
            args.PushMarkup("[color=orange]To claim ownership, interact with the card or another person to bind it to them.[/color]");
        }
    }

    private void OnUseInHand(Entity<IdCardComponent> ent, ref UseInHandEvent args)
    {
        if (ent.Comp.OriginalOwner != null || args.Handled)
            return;

        ent.Comp.OriginalOwner = args.User;
        args.Handled = true;
        var popupMessage = $"Bound ID to yourself.";
        _popup.PopupClient(popupMessage, args.User, PopupType.Small);
        _adminLogger.Add(LogType.RMCIdModify,
            LogImpact.Medium,
            $"{ToPrettyString(args.User):player} has bound the ID {ToPrettyString(ent):entity} to themselves.");
        Dirty(ent);
    }
}
