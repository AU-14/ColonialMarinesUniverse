using Content.Shared.CMU14.Yautja;
using Content.Shared.Actions;
using Content.Shared.Mobs.Systems;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaAbilitySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private YautjaMarkSystem _marks = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private YautjaTrophySystem _trophies = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, YautjaButcherActionEvent>(OnButcher);
        SubscribeLocalEvent<YautjaComponent, YautjaMarkForHuntActionEvent>(OnMarkForHunt);
    }

    public void GrantActions(Entity<YautjaComponent> ent)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.MarkForHuntAction, ent.Comp.MarkForHuntActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.ButcherAction, ent.Comp.ButcherActionId);
    }

    public void RemoveActions(Entity<YautjaComponent> ent)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.MarkForHuntAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.ButcherAction);
    }

    private void OnButcher(Entity<YautjaComponent> ent, ref YautjaButcherActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner || _mob.IsIncapacitated(ent.Owner))
            return;

        args.Handled = _trophies.TryOpenButcherDialog(ent.Owner);
    }

    private void OnMarkForHunt(Entity<YautjaComponent> ent, ref YautjaMarkForHuntActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        if (!_power.TryGetWornBracer(ent.Owner, out var bracer))
            return;

        if (_marks.IsMarkedBy(args.Target, YautjaMarkKind.Prey, ent.Owner))
        {
            args.Handled = _marks.TryClearMark(args.Target, YautjaMarkKind.Prey, ent.Owner, showPreyRemoved: true);
            return;
        }

        args.Handled = _marks.TryMark(bracer, ent.Owner, args.Target, YautjaMarkKind.Prey, null);
    }
}
