using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Marines.Orders;
using Content.Shared._RMC14.Marines.Squads;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Marines.Orders;

public sealed partial class MarineOrdersSystem : SharedMarineOrdersSystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MarineOrdersComponent, ComponentStartup>(OnOrdersStartup);
        SubscribeLocalEvent<MarineOrdersComponent, ComponentShutdown>(OnOrdersShutdown);
    }
    private void OnOrdersStartup(Entity<MarineOrdersComponent> ent, ref ComponentStartup ev)
    {
        var comp = ent.Comp;
        if (comp.MoveActionEntity != null || comp.HoldActionEntity != null || comp.FocusActionEntity != null) return;
        if (!HasComp<SquadLeaderComponent>(ent.Owner)
            && _skills.GetSkill(ent.Owner, comp.Skill) <= 0)
            return;

        EnsureOrderActions(ent);
    }

    private void OnOrdersShutdown(Entity<MarineOrdersComponent> ent, ref ComponentShutdown ev)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.FocusActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.HoldActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.MoveActionEntity);
    }

    protected override void OnAction(Entity<MarineOrdersComponent> ent, ref MoveActionEvent ev)
    {
        base.OnAction(ent, ref ev);
        OnAction(ent, ent.Comp.MoveCallouts);
    }

    protected override void OnAction(Entity<MarineOrdersComponent> ent, ref HoldActionEvent ev)
    {
        base.OnAction(ent, ref ev);
        OnAction(ent, ent.Comp.HoldCallouts);
    }

    protected override void OnAction(Entity<MarineOrdersComponent> ent, ref FocusActionEvent ev)
    {
        base.OnAction(ent, ref ev);
        OnAction(ent, ent.Comp.FocusCallouts);
    }

    private void OnAction(EntityUid uid, List<LocId> callouts)
    {
        if (callouts.Count == 0)
            return;

        var callout = _random.Next(0, callouts.Count);
        _chat.TrySendInGameICMessage(uid, Loc.GetString(callouts[callout]), InGameICChatType.Speak, false);
    }
}
