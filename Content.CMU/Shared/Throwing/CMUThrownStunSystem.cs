using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;

namespace Content.Shared.CMU14.Throwing;

public sealed partial class CMUThrownStunSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ThrowDoHitEvent>(OnThrownHit);
    }

    private void OnThrownHit(ref ThrowDoHitEvent ev)
    {
        if (ev.Component.Thrower is not { } thrower)
            return;

        if (!TryComp<CMUThrownStunComponent>(thrower, out var stun))
            return;

        if (ev.Target == thrower || !HasComp<MobStateComponent>(ev.Target))
            return;

        if (_npcFaction.IsEntityFriendly(thrower, ev.Target))
            return;

        _stun.TryStun(ev.Target, stun.StunTime, true);
        _stun.TryKnockdown(ev.Target, stun.KnockdownTime, true);
    }
}
