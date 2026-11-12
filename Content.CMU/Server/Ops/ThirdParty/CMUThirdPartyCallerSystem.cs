using Content.Server.Chat.Managers;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Ops.ThirdParty;

/// <summary>
/// Tells third-party members who called them in: in the ghost role's description, and in chat once a player
/// takes the body (including players placed straight into it by the interest queue, who never see the menu).
/// </summary>
public sealed partial class CMUThirdPartyCallerSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUThirdPartyCallerComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    public void ApplyCaller(EntityUid member, ThirdPartyCaller caller)
    {
        var comp = EnsureComp<CMUThirdPartyCallerComponent>(member);
        comp.CallerName = caller.Name;
        comp.CallerFaction = caller.Faction;

        if (TryComp<GhostRoleComponent>(member, out var ghostRole))
        {
            var line = GetMessage(comp);
            ghostRole.RoleDescription = string.IsNullOrWhiteSpace(ghostRole.RoleDescription)
                ? line
                : $"{ghostRole.RoleDescription}\n{line}";
        }
    }

    private void OnPlayerAttached(Entity<CMUThirdPartyCallerComponent> ent, ref PlayerAttachedEvent args)
    {
        if (ent.Comp.Notified)
            return;

        ent.Comp.Notified = true;
        _chat.DispatchServerMessage(args.Player, GetMessage(ent.Comp));
    }

    private string GetMessage(CMUThirdPartyCallerComponent comp)
    {
        return Loc.GetString("cmu-third-party-called-by",
            ("name", comp.CallerName),
            ("faction", comp.CallerFaction));
    }
}
