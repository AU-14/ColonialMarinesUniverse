using Content.Server.Chat.Managers;
using Content.Server.CMU14.Threats;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Ghost.Roles.Raffles;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Roles.Raffles;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

// the landing party's unclaimed bodies: Bad Blood text instead of the generic threat role, a raffle like the squad
// seats and hounds instead of first come, and this round's honourable hunters kept out like everywhere else.
// FLOW-603: a freed body goes to the Blood Reserve first, for BadBloodCampRuleComponent.ReserveBodyPriority
public sealed partial class BadBloodGhostRoleSystem : EntitySystem
{
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private YautjaPredatorRoundSystem _predatorRound = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private ThreatSystem _threats = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<GhostRoleRaffleSettingsPrototype> RaffleSettings = "default";

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBadBloodComponent, ThreatGhostRoleMadeEvent>(OnThreatGhostRoleMade);
        SubscribeLocalEvent<YautjaBadBloodComponent, GhostRoleRequestAttemptEvent>(OnRequestAttempt);
        SubscribeLocalEvent<YautjaBadBloodComponent, MindRemovedMessage>(OnBadBloodMindRemoved);
        SubscribeLocalEvent<BadBloodHoundComponent, BeforeMindRemovedMessage>(OnHoundBeforeMindRemoved);
    }

    /// <summary>
    /// The player left a living body for a ghost (/ghost, not death or a body swap). Other threats leave a ghost
    /// role behind; a Bad Blood that was put in its body at round start or through a squad seat had none, so it
    /// is made one here. Bodies that already carry the takeover component are re-registered by GhostRoleSystem.
    /// </summary>
    private bool LeftForGhost(EntityUid body, MindRemovedMessage args)
    {
        return args.TransferEntity is { } to &&
               HasComp<GhostComponent>(to) &&
               !TerminatingOrDeleted(body) &&
               !_mobState.IsDead(body);
    }

    private void OnBadBloodMindRemoved(Entity<YautjaBadBloodComponent> ent, ref MindRemovedMessage args)
    {
        if (!LeftForGhost(ent.Owner, args))
            return;

        // CMU14: FLOW-603, a body GhostRoleSystem re-registers is offered again too
        MarkOffered(ent.Owner);

        if (HasComp<GhostTakeoverAvailableComponent>(ent))
            return;

        _threats.MakeThreatGhostRole(ent.Owner, _badBlood.IsBadBloodLeader(ent.Owner));
    }

    private void MarkOffered(EntityUid body)
    {
        EnsureComp<BadBloodOfferedBodyComponent>(body).BodyOfferedAt = _timing.CurTime;
    }

    // while anyone waits in the reserve, a freshly freed body is theirs: everyone else waits out the window
    private bool IsHeldForReserve(EntityUid body, NetUserId user)
    {
        return TryComp(body, out BadBloodOfferedBodyComponent? offered) &&
               _rule.TryGetRule(out var rule) &&
               rule.Comp.ReserveQueue.Count > 0 &&
               !rule.Comp.ReserveQueue.Contains(user) &&
               _timing.CurTime < offered.BodyOfferedAt + rule.Comp.ReserveBodyPriority;
    }

    // hounds carry their ghost role from the prototype; this covers one whose takeover was stripped. It runs on the
    // Before message so GhostRoleSystem's own MindRemovedMessage handler then frees and re-registers the role
    private void OnHoundBeforeMindRemoved(Entity<BadBloodHoundComponent> ent, ref BeforeMindRemovedMessage args)
    {
        if (args.TransferEntity is not { } to ||
            !HasComp<GhostComponent>(to) ||
            TerminatingOrDeleted(ent) ||
            _mobState.IsDead(ent) ||
            !HasComp<GhostRoleComponent>(ent))
        {
            return;
        }

        EnsureComp<GhostTakeoverAvailableComponent>(ent);
    }

    private void OnThreatGhostRoleMade(Entity<YautjaBadBloodComponent> ent, ref ThreatGhostRoleMadeEvent args)
    {
        var role = args.Role;
        role.RoleName = args.Leader ? "cmu-bad-blood-leader-ghost-name" : "cmu-bad-blood-grunt-ghost-name";
        role.RoleDescription = args.Leader
            ? "cmu-bad-blood-leader-ghost-description"
            : "cmu-bad-blood-grunt-ghost-description";
        role.RoleRules = "cmu-bad-blood-squad-ghost-rules";
        role.RaffleConfig = new GhostRoleRaffleConfig(_proto.Index(RaffleSettings).Settings);
        MarkOffered(ent.Owner); // CMU14: FLOW-603
    }

    private void OnRequestAttempt(Entity<YautjaBadBloodComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        string reason;
        if (_predatorRound.WasHunterThisRound(args.Player.UserId))
            reason = "cmu-bad-blood-squad-seat-hunter";
        else if (IsHeldForReserve(ent.Owner, args.Player.UserId))
            reason = "cmu-bad-blood-reserve-body-held";
        else
            return;

        args.Cancelled = true;
        if (args.ExplicitRequest)
            _chat.DispatchServerMessage(args.Player, Loc.GetString(reason));
    }
}
