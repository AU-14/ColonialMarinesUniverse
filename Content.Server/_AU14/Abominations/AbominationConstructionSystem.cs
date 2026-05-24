using System.Linq;
using Content.Shared._AU14.Abominations.Abilities;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Abominations;

public sealed partial class AbominationConstructionSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationConstructionComponent, AbominationConstructionChooseActionEvent>(OnChooseAction);
        SubscribeLocalEvent<AbominationConstructionComponent, AbominationConstructionSecreteActionEvent>(OnSecreteAction);

        // Modern BUI subscription pattern — auto-filters by UI key.
        Subs.BuiEvents<AbominationConstructionComponent>(AbominationConstructionUiKey.Key, subs =>
        {
            subs.Event<AbominationConstructionChooseMessage>(OnChooseMessage);
        });
    }

    private void OnChooseAction(Entity<AbominationConstructionComponent> ent, ref AbominationConstructionChooseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _ui.TryOpenUi(ent.Owner, AbominationConstructionUiKey.Key, args.Performer);
        PushBuiState(ent);
    }

    private void OnChooseMessage(Entity<AbominationConstructionComponent> ent, ref AbominationConstructionChooseMessage args)
    {
        if (!ent.Comp.CanBuild.Contains(args.Structure))
            return;

        ent.Comp.BuildChoice = args.Structure;
        Dirty(ent);
        PushBuiState(ent);
    }

    private void OnSecreteAction(Entity<AbominationConstructionComponent> ent, ref AbominationConstructionSecreteActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.BuildChoice is not { } choice)
        {
            _popup.PopupClient(Loc.GetString("abomination-secrete-no-choice"), ent, ent);
            return;
        }

        // Flesh nests have their own 40s cooldown on top of the action's
        // useDelay. Other structures (walls, etc.) are gated only by the action.
        if (choice == ent.Comp.NestProto &&
            ent.Comp.NextNestAt is { } nestReady &&
            _timing.CurTime < nestReady)
        {
            _popup.PopupClient(Loc.GetString("abomination-secrete-nest-cooldown"), ent, ent);
            return;
        }

        args.Handled = true;

        var target = _transform.ToMapCoordinates(args.Target);
        Spawn(choice, target);

        if (choice == ent.Comp.NestProto)
        {
            ent.Comp.NextNestAt = _timing.CurTime + ent.Comp.NestCooldown;
            Dirty(ent);
        }
    }

    private void PushBuiState(Entity<AbominationConstructionComponent> ent)
    {
        var options = ent.Comp.CanBuild.Select(id => id.Id).ToList();
        _ui.SetUiState(ent.Owner, AbominationConstructionUiKey.Key,
            new AbominationConstructionBuiState(options, ent.Comp.BuildChoice?.Id));
    }
}
