using Content.Server.Chat.Managers;
using Content.Server.CMU14.Threats;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Ghost.Roles.Raffles;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Ghost.Roles.Raffles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

// the landing party's unclaimed bodies: Bad Blood text instead of the generic threat role, a raffle like the squad
// seats and hounds instead of first come, and this round's honourable hunters kept out like everywhere else
public sealed partial class BadBloodGhostRoleSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private YautjaPredatorRoundSystem _predatorRound = default!;

    private static readonly ProtoId<GhostRoleRaffleSettingsPrototype> RaffleSettings = "default";

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBadBloodComponent, ThreatGhostRoleMadeEvent>(OnThreatGhostRoleMade);
        SubscribeLocalEvent<YautjaBadBloodComponent, GhostRoleRequestAttemptEvent>(OnRequestAttempt);
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
    }

    private void OnRequestAttempt(Entity<YautjaBadBloodComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        if (args.Cancelled || !_predatorRound.WasHunterThisRound(args.Player.UserId))
            return;

        args.Cancelled = true;
        if (args.ExplicitRequest)
            _chat.DispatchServerMessage(args.Player, Loc.GetString("cmu-bad-blood-squad-seat-hunter"));
    }
}
