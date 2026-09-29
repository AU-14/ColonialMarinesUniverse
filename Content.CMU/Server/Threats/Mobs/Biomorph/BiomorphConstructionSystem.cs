using System.Linq;
using Content.Shared.CMU14.Threats.Mobs.Biomorph.Abilities;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using BiomorphConstructionChooseActionEvent
    = Content.Shared.CMU14.Threats.Mobs.Biomorph.Abilities.BiomorphConstructionChooseActionEvent;
namespace Content.Server.CMU14.Threats.Mobs.Biomorph;

public sealed partial class BiomorphConstructionSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BiomorphConstructionComponent, BiomorphConstructionChooseActionEvent>(OnChooseAction);
        SubscribeLocalEvent<BiomorphConstructionComponent, BiomorphConstructionSecreteActionEvent>(
            OnSecreteAction);

        // Modern BUI subscription pattern — auto-filters by UI key.
        Subs.BuiEvents<BiomorphConstructionComponent>(BiomorphConstructionUiKey.Key, subs =>
        {
            subs.Event<BiomorphConstructionChooseMessage>(OnChooseMessage);
        });
    }

    private void OnChooseAction(Entity<BiomorphConstructionComponent> ent,
        ref BiomorphConstructionChooseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _ui.TryOpenUi(ent.Owner, BiomorphConstructionUiKey.Key, args.Performer);
        PushBuiState(ent);
    }

    private void OnChooseMessage(Entity<BiomorphConstructionComponent> ent,
        ref BiomorphConstructionChooseMessage args)
    {
        if (!ent.Comp.CanBuild.Contains(args.Structure))
            return;

        ent.Comp.BuildChoice = args.Structure;
        Dirty(ent);
        PushBuiState(ent);
    }

    private void OnSecreteAction(Entity<BiomorphConstructionComponent> ent,
        ref BiomorphConstructionSecreteActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.BuildChoice is not { } choice)
        {
            _popup.PopupClient(Loc.GetString("biomorph-secrete-no-choice"), ent, ent);

            return;
        }

        // Flesh nests have their own 40s cooldown on top of the action's
        // useDelay. Other structures (walls, etc.) are gated only by the action.
        if (choice == ent.Comp.NestProto && ent.Comp.NextNestAt is { } nestReady && _timing.CurTime < nestReady)
        {
            _popup.PopupClient(Loc.GetString("biomorph-secrete-nest-cooldown"), ent, ent);

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

    private void PushBuiState(Entity<BiomorphConstructionComponent> ent)
    {
        List<string> options = ent.Comp.CanBuild.Select(id => id.Id).ToList();
        _ui.SetUiState(ent.Owner, BiomorphConstructionUiKey.Key,
            new BiomorphConstructionBuiState(options, ent.Comp.BuildChoice?.Id));
    }
}
