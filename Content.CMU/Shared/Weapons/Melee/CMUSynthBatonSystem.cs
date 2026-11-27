using Content.Shared._RMC14.Synth;
using Content.Shared.Damage.Events;
using Content.Shared.Jittering;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Weapons.Melee;

public sealed partial class CMUSynthBatonSystem : EntitySystem
{
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly ProtoId<StatusEffectPrototype> BatonShock = "CMUSynthBatonShock";

    public HashSet<EntityUid>? GetShockTargets(EntityUid weapon, IReadOnlyList<EntityUid> hits)
    {
        if (!HasComp<CMUSynthBatonComponent>(weapon))
            return null;

        HashSet<EntityUid>? targets = null;
        foreach (var hit in hits)
        {
            if (HasComp<SynthComponent>(hit))
                (targets ??= new()).Add(hit);
        }

        return targets;
    }

    public void ShockTargets(EntityUid weapon, HashSet<EntityUid>? targets)
    {
        if (targets == null || _net.IsClient)
            return;

        var baton = Comp<CMUSynthBatonComponent>(weapon);
        foreach (var target in targets)
            Shock(target, baton);
    }

    public bool TryHandleCollision(EntityUid weapon, EntityUid target)
    {
        if (!TryComp<CMUSynthBatonComponent>(weapon, out var baton) || !HasComp<SynthComponent>(target))
            return false;

        var attempt = new StaminaDamageOnHitAttemptEvent();
        RaiseLocalEvent(weapon, ref attempt);
        if (!attempt.Cancelled)
        {
            // Use the same charge cost and toggle checks as a melee hit.
            RaiseLocalEvent(weapon, new StaminaMeleeHitEvent([]));
            if (_net.IsServer)
                Shock(target, baton);
        }
        return true;
    }

    private void Shock(EntityUid target, CMUSynthBatonComponent baton)
    {
        _jitter.DoJitter(target, baton.JitterDuration, refresh: true, amplitude: 15, frequency: 8);
        if (_status.TryRemoveStatusEffect(target, BatonShock))
            _stun.TryKnockdown(target, baton.KnockdownDuration);
        else
            _status.TryAddStatusEffect(target, BatonShock, baton.JitterDuration, refresh: true);
    }
}
