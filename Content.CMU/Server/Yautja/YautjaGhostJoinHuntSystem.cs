using Content.Shared._RMC14.Dialog;
using Content.Shared.Actions;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Ghost.Components;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja;

// cmss13 observer "Join the Hunt": whitelisted ghosts get the button while a hunt is running
public sealed partial class YautjaGhostJoinHuntSystem : EntitySystem
{
    // the hunt can be forced mid-round and whitelists get refreshed, so just recheck the few ghosts now and then
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private YautjaPredatorRoundSystem _round = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaGhostJoinHuntComponent, YautjaJoinHuntActionEvent>(OnJoinHuntAction);
        SubscribeLocalEvent<YautjaGhostJoinHuntComponent, YautjaJoinHuntConfirmEvent>(OnJoinHuntConfirm);
        SubscribeLocalEvent<YautjaGhostJoinHuntComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;
        var query = EntityQueryEnumerator<GhostComponent, ActorComponent>();
        while (query.MoveNext(out var ghost, out _, out var actor))
        {
            Refresh(ghost, actor.PlayerSession);
        }
    }

    public void Refresh(EntityUid ghost, ICommonSession session)
    {
        var show = _round.CanSeeGhostJoinHunt(session) && !_round.WasHunterThisRound(session.UserId);
        var has = HasComp<YautjaGhostJoinHuntComponent>(ghost);
        if (show == has)
            return;

        if (!show)
        {
            RemComp<YautjaGhostJoinHuntComponent>(ghost);
            return;
        }

        var comp = EnsureComp<YautjaGhostJoinHuntComponent>(ghost);
        _actions.AddAction(ghost, ref comp.Action, comp.ActionId);
        Dirty(ghost, comp);
    }

    private void OnShutdown(Entity<YautjaGhostJoinHuntComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Action is { } action)
            _actions.RemoveAction(ent.Owner, action);
    }

    private void OnJoinHuntAction(Entity<YautjaGhostJoinHuntComponent> ent, ref YautjaJoinHuntActionEvent args)
    {
        args.Handled = true;
        if (!TryComp(args.Performer, out ActorComponent? actor))
            return;

        if (!_round.CanGhostJoinHunt(actor.PlayerSession, out var reason))
        {
            _popup.PopupEntity(reason, args.Performer, args.Performer, PopupType.MediumCaution);
            return;
        }

        var rank = _round.ResolveRankForSession(actor.PlayerSession);
        _dialog.OpenConfirmation(
            ent.Owner,
            args.Performer,
            Loc.GetString("cmu-yautja-ghost-join-hunt-title"),
            Loc.GetString("cmu-yautja-ghost-join-hunt-confirm", ("rank", Loc.GetString(YautjaRankMetadata.For(rank).LocalizedName))),
            new YautjaJoinHuntConfirmEvent());
    }

    private void OnJoinHuntConfirm(EntityUid uid, YautjaGhostJoinHuntComponent comp, YautjaJoinHuntConfirmEvent args)
    {
        if (!TryComp(uid, out ActorComponent? actor))
            return;

        if (!_round.TryGhostJoinHunt(actor.PlayerSession, out var reason))
            _popup.PopupEntity(reason, uid, uid, PopupType.MediumCaution);
    }
}
