using System.Runtime.InteropServices;
using Content.Server.Body.Systems;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Medical.Wounds;

public sealed class WoundsSystem : SharedWoundsSystem
{
    private static readonly TimeSpan DeadWoundUpdateCooldown = TimeSpan.FromSeconds(10);

    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly List<int> _toRemove = new();
    private DamageSpecifier _passiveDamage = new();

    private EntityQuery<BloodstreamComponent> _bloodstreamQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundedComponent, MobStateChangedEvent>(OnWoundedMobStateChanged);

        _bloodstreamQuery = GetEntityQuery<BloodstreamComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
    }

    private void OnWoundedMobStateChanged(Entity<WoundedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            return;

        ent.Comp.UpdateAt = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var wounded = EntityQueryEnumerator<WoundedComponent>();
        while (wounded.MoveNext(out var uid, out var comp))
        {
            if (time < comp.UpdateAt)
                continue;

            var dead = _mobStateQuery.TryComp(uid, out var mobState) &&
                       mobState.CurrentState == MobState.Dead;
            comp.UpdateAt = time + (dead ? DeadWoundUpdateCooldown : comp.UpdateCooldown);

            if (dead)
            {
                if (_bloodstreamQuery.TryComp(uid, out var deadBloodstream) &&
                    deadBloodstream.BleedAmount > 0)
                {
                    _bloodstream.TryModifyBleedAmount(uid, -deadBloodstream.BleedAmount);
                }

                continue;
            }

            _passiveDamage.DamageDict.Clear();
            _toRemove.Clear();

            var bleedEv = new CMBleedAttemptEvent();
            RaiseLocalEvent(uid, ref bleedEv);

            _damageableQuery.TryComp(uid, out var damageable);
            var damageableEnt = new Entity<DamageableComponent?>(uid, damageable);

            var toHeal = comp.PassiveHealing;
            float bloodloss = 0;
            var wounds = CollectionsMarshal.AsSpan(comp.Wounds);
            for (var i = 0; i < wounds.Length; i++)
            {
                ref var wound = ref wounds[i];
                if (wound.Healed >= wound.Damage)
                {
                    _toRemove.Add(i);
                    continue;
                }

                if (damageable != null &&
                    toHeal < FixedPoint2.Zero &&
                    wound.Treated)
                {
                    var group = wound.Type switch
                    {
                        WoundType.Brute => comp.BruteWoundGroup,
                        WoundType.Burn => comp.BurnWoundGroup,
                        _ => default(ProtoId<DamageGroupPrototype>?)
                    };

                    if (group != null)
                    {
                        var amount = -FixedPoint2.Min(-toHeal, wound.Damage - wound.Healed);
                        toHeal -= amount;
                        _passiveDamage = _rmcDamageable.DistributeDamageCached(damageableEnt, group.Value, amount, _passiveDamage);
                    }
                }

                if (!bleedEv.Cancelled && !wound.Treated)
                    bloodloss += wound.Bloodloss;
            }

            if (toHeal > comp.PassiveHealing)
            {
                _damageable.TryChangeDamage(uid, _passiveDamage, true, false, damageable, uid);
            }

            for (var i = _toRemove.Count - 1; i >= 0; i--)
            {
                var remove = _toRemove[i];
                comp.Wounds.RemoveAt(remove);
            }

            if (comp.Wounds.Count == 0)
            {
                RemCompDeferred<WoundedComponent>(uid);
            }
            else if (_toRemove.Count > 0)
            {
                Dirty(uid, comp);
            }

            if (_bloodstreamQuery.TryComp(uid, out var bloodstream))
            {
                var delta = bloodloss - bloodstream.BleedAmount;
                _bloodstream.TryModifyBleedAmount(uid, delta);
            }
        }
    }
}
