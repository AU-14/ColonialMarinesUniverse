using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed partial class CMUZLevelLadderSystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    private static readonly TimeSpan ThrowDelay = TimeSpan.FromSeconds(1);

    private void InitializeThrow()
    {
        SubscribeLocalEvent<CMUZLevelLadderComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUZLevelLadderComponent, CMUZLevelLadderThrowDoAfterEvent>(OnThrowDoAfter);
    }

    private void OnInteractUsing(Entity<CMUZLevelLadderComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || HasComp<GunComponent>(args.Used))
            return;

        if (!TryGetDefaultMovementOffset(ent.Comp, out var offset))
            return;

        args.Handled = true;

        var user = args.User;
        var item = args.Used;
        var doAfter = new DoAfterArgs(EntityManager, user, ThrowDelay, new CMUZLevelLadderThrowDoAfterEvent(offset), ent, ent, item)
        {
            BreakOnMove = true,
            BreakOnDropItem = true,
            NeedHand = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        var direction = ThrowDirection(offset);
        _popup.PopupEntity(
            Loc.GetString("cmu-zlevel-ladder-throw-start-self", ("item", item), ("direction", direction)),
            Loc.GetString("cmu-zlevel-ladder-throw-start-others", ("user", user), ("item", item), ("direction", direction)),
            user,
            user);
    }

    private void OnThrowDoAfter(Entity<CMUZLevelLadderComponent> ent, ref CMUZLevelLadderThrowDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } item)
            return;

        args.Handled = true;

        var user = args.User;
        if (!_hands.IsHolding(user, item) ||
            !_interaction.InRangeUnobstructed(user, ent.Owner, ent.Comp.Range) ||
            !_hands.TryDrop(user, item, Transform(ent).Coordinates))
        {
            return;
        }

        if (!_zLevels.TryMove(item, args.Offset, worldPosition: _transform.GetWorldPosition(ent)))
        {
            _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-no-level"), ent, user, PopupType.SmallCaution);
            return;
        }

        var direction = ThrowDirection(args.Offset);
        _popup.PopupEntity(
            Loc.GetString("cmu-zlevel-ladder-throw-finish-self", ("item", item), ("direction", direction)),
            user,
            user);
        _popup.PopupEntity(
            Loc.GetString("cmu-zlevel-ladder-throw-finish-others", ("user", user), ("item", item), ("direction", direction)),
            user,
            Filter.PvsExcept(user),
            true);
    }

    private string ThrowDirection(int offset)
    {
        return Loc.GetString(offset > 0 ? "cmu-zlevel-ladder-throw-up" : "cmu-zlevel-ladder-throw-down");
    }
}
