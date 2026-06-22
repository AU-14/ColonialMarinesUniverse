using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Marines.Orders;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameObjects;
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

        SubscribeLocalEvent<SquadLeaderComponent, ComponentInit>(OnSquadLeaderInit);
        SubscribeLocalEvent<SquadLeaderComponent, ComponentShutdown>(OnSquadLeaderShutdown);
        SubscribeLocalEvent<MarineOrdersComponent, SkillChangedEvent>(OnSkillChanged);
    }

    private void OnOrdersStartup(Entity<MarineOrdersComponent> ent, ref ComponentStartup ev)
    {
        SyncOrderActions(ent);
    }

    private void OnOrdersShutdown(Entity<MarineOrdersComponent> ent, ref ComponentShutdown ev)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.FocusActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.HoldActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.MoveActionEntity);
    }

    private void OnSkillChanged(Entity<MarineOrdersComponent> ent, ref SkillChangedEvent args)
    {
        if (args.Skill != ent.Comp.Skill)
            return;

        SyncOrderActions(ent);
    }

    private void OnSquadLeaderInit(Entity<SquadLeaderComponent> ent, ref ComponentInit args)
    {
        if (TryComp<MarineOrdersComponent>(ent, out var orders))
            SyncOrderActions((ent, orders));
    }

    private void OnSquadLeaderShutdown(Entity<SquadLeaderComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<MarineOrdersComponent>(ent, out var orders))
            SyncOrderActions((ent, orders));
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
