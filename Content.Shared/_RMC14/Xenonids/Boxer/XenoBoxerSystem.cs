using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Boxer;

public sealed partial class XenoBoxerSystem : EntitySystem
{
    private static readonly SoundSpecifier PunchSound = new SoundCollectionSpecifier("Punch");

    private static readonly HashSet<string> ClearHeadStatuses = new()
    {
        "Dazed",
        "Stun",
        "KnockedDown",
        "Unconscious",
    };

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private RMCObstacleSlammingSystem _obstacleSlamming = default!;
    [Dependency] private RMCPullingSystem _rmcPulling = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private RMCDazedSystem _daze = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MobThresholdSystem _mobThresholds = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoBoxerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<XenoBoxerComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<XenoBoxerComponent, BeforeStatusEffectAddedEvent>(OnBeforeStatusAdded);
        SubscribeLocalEvent<XenoBoxerComponent, XenoBoxerPunchActionEvent>(OnPunchAction);
        SubscribeLocalEvent<XenoBoxerComponent, XenoBoxerJabActionEvent>(OnJabAction);
        SubscribeLocalEvent<XenoBoxerComponent, XenoBoxerUppercutActionEvent>(OnUppercutAction);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<XenoBoxerComponent>();
        while (query.MoveNext(out var uid, out var boxer))
        {
            boxer.ClearHeadConsumedThisTick = false;

            if (_net.IsClient)
                continue;

            if (boxer.KoTarget is not null &&
                XenoBoxerRules.IsKoExpired(_timing.CurTime, boxer.LastKoHitAt))
            {
                ResetKo((uid, boxer));
            }

            if (!XenoBoxerRules.IsClearHeadRegenDue(_timing.CurTime, boxer.NextClearHeadRegenAt, boxer.ClearHeadCharges))
                continue;

            boxer.ClearHeadCharges++;
            boxer.NextClearHeadRegenAt = XenoBoxerRules.GetNextClearHeadRegenAt(_timing.CurTime);
            Dirty(uid, boxer);
            _popup.PopupEntity(
                Loc.GetString("cm-xeno-boxer-clear-head-charge", ("charges", boxer.ClearHeadCharges)),
                uid,
                uid);
        }
    }

    private void OnMapInit(Entity<XenoBoxerComponent> boxer, ref MapInitEvent args)
    {
        boxer.Comp.ClearHeadCharges = Math.Clamp(boxer.Comp.ClearHeadCharges, 0, XenoBoxerRules.ClearHeadMaxCharges);
        boxer.Comp.NextClearHeadRegenAt = XenoBoxerRules.GetNextClearHeadRegenAt(_timing.CurTime);
        Dirty(boxer);
    }

    private void OnBeforeStatusAdded(Entity<XenoBoxerComponent> boxer, ref BeforeStatusEffectAddedEvent args)
    {
        if (!ClearHeadStatuses.Contains(args.Effect.Id) || boxer.Comp.ClearHeadConsumedThisTick)
            return;

        if (!XenoBoxerRules.TryConsumeClearHead(ref boxer.Comp.ClearHeadCharges, forced: false))
            return;

        boxer.Comp.ClearHeadConsumedThisTick = true;
        args.Cancelled = true;
        Dirty(boxer);
        _popup.PopupClient(
            Loc.GetString("cm-xeno-boxer-clear-head-charge", ("charges", boxer.Comp.ClearHeadCharges)),
            boxer.Owner,
            boxer.Owner);
    }

    private void OnMeleeHit(Entity<XenoBoxerComponent> boxer, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (_xeno.CanAbilityAttackTarget(boxer.Owner, target))
                AddKo(boxer, target, 0.5f);
        }
    }

    private void OnPunchAction(Entity<XenoBoxerComponent> boxer, ref XenoBoxerPunchActionEvent args)
    {
        if (args.Handled || !_xeno.CanAbilityAttackTarget(boxer.Owner, args.Target))
            return;

        if (!_interaction.InRangeUnobstructed(boxer.Owner, args.Target, boxer.Comp.PunchRange))
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        PlayPunchSound(boxer);
        _rmcMelee.DoLunge(boxer.Owner, args.Target);
        _rmcPulling.TryStopAllPullsFromAndOn(args.Target);
        _obstacleSlamming.MakeImmune(args.Target);

        var damage = new DamageSpecifier
        {
            DamageDict = { ["Blunt"] = GetPunchDamage(boxer.Comp, args.Target) },
        };
        var changed = _damageable.TryChangeDamage(
            args.Target,
            _xeno.TryApplyXenoSlashDamageMultiplier(args.Target, damage),
            origin: boxer,
            tool: boxer);
        if (changed?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(args.Target, entityManager: EntityManager)
                .RemoveWhereAttachedEntity(entity => entity == boxer.Owner);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { args.Target }, filter);
        }

        _size.KnockBack(
            args.Target,
            _transform.GetMapCoordinates(boxer),
            boxer.Comp.PunchKnockBackDistance,
            boxer.Comp.PunchKnockBackDistance,
            boxer.Comp.PunchThrowSpeed);
        if (_random.Prob(boxer.Comp.PunchSecondKnockBackChance))
        {
            _size.KnockBack(
                args.Target,
                _transform.GetMapCoordinates(boxer),
                boxer.Comp.PunchKnockBackDistance,
                boxer.Comp.PunchKnockBackDistance,
                boxer.Comp.PunchThrowSpeed);
        }

        AddKo(boxer, args.Target, 1f);
        ReduceCooldown<XenoBoxerJabActionEvent>(boxer.Owner, args.Target, boxer.Comp.JabCooldown);

        if (!Deleted(args.Target))
            SpawnAttachedTo("CMEffectPunch", args.Target.ToCoordinates());
    }

    private void OnJabAction(Entity<XenoBoxerComponent> boxer, ref XenoBoxerJabActionEvent args)
    {
        if (args.Handled || !_xeno.CanAbilityAttackTarget(boxer.Owner, args.Target))
            return;

        if (!_interaction.InRangeUnobstructed(boxer.Owner, args.Target, boxer.Comp.JabRange))
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        PlayPunchSound(boxer);
        _rmcMelee.DoLunge(boxer.Owner, args.Target);
        _daze.TryDaze(args.Target, boxer.Comp.JabDazeDuration, true);
        _stun.TrySlowdown(
            args.Target,
            boxer.Comp.JabSlowDuration,
            true,
            boxer.Comp.JabSlowMultiplier,
            boxer.Comp.JabSlowMultiplier);

        AddKo(boxer, args.Target, 1f);
        ReduceCooldown<XenoBoxerPunchActionEvent>(boxer.Owner, args.Target, boxer.Comp.PunchCooldown);
    }

    private void OnUppercutAction(Entity<XenoBoxerComponent> boxer, ref XenoBoxerUppercutActionEvent args)
    {
        if (args.Handled || !_xeno.CanAbilityAttackTarget(boxer.Owner, args.Target))
            return;

        if (boxer.Comp.KoTarget != args.Target ||
            XenoBoxerRules.IsKoExpired(_timing.CurTime, boxer.Comp.LastKoHitAt) ||
            boxer.Comp.KoMeter <= 0 ||
            !_interaction.InRangeUnobstructed(boxer.Owner, args.Target, boxer.Comp.UppercutRange))
        {
            return;
        }

        if (_size.TryGetSize(args.Target, out var size) && size >= RMCSizes.Big)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        PlayPunchSound(boxer);
        SetCooldown<XenoBoxerPunchActionEvent>(boxer.Owner, boxer.Comp.PunchCooldown);
        SetCooldown<XenoBoxerJabActionEvent>(boxer.Owner, boxer.Comp.JabCooldown);

        var ko = boxer.Comp.KoMeter;
        var targetIsXeno = HasComp<XenoComponent>(args.Target);

        var damage = XenoBoxerRules.GetUppercutDamage(ko, boxer.Comp.UppercutDamagePerKo);
        if (damage > 0)
        {
            _damageable.TryChangeDamage(
                args.Target,
                _xeno.TryApplyXenoSlashDamageMultiplier(args.Target, new DamageSpecifier
                {
                    DamageDict = { ["Blunt"] = damage },
                }),
                origin: boxer,
                tool: boxer);
        }

        var knockBackPower = XenoBoxerRules.GetUppercutKnockBackPower(ko, boxer.Comp.UppercutKnockBackPowerPerKo);
        if (knockBackPower > 0)
        {
            _size.KnockBack(
                args.Target,
                _transform.GetMapCoordinates(boxer),
                knockBackPower,
                knockBackPower,
                boxer.Comp.UppercutKnockBackSpeed);
        }

        var knockDownDuration = XenoBoxerRules.GetUppercutKnockDownDuration(ko);
        if (knockDownDuration > TimeSpan.Zero)
            _stun.TryKnockdown(args.Target, knockDownDuration, true, force: true);

        if (XenoBoxerRules.GetUppercutStage(ko) >= XenoBoxerUppercutStage.KnockOut)
            _size.TryKnockOut(args.Target, boxer.Comp.UppercutKnockOutDuration);

        HealFromUppercut(boxer, ko, targetIsXeno);
        ResetKo(boxer);
        SpawnAttachedTo("CMEffectPunch", args.Target.ToCoordinates());
    }

    private void PlayPunchSound(Entity<XenoBoxerComponent> boxer)
    {
        if (_net.IsServer)
            _audio.PlayPvs(PunchSound, boxer);
    }

    private void HealFromUppercut(Entity<XenoBoxerComponent> boxer, float ko, bool targetIsXeno)
    {
        if (!_mobThresholds.TryGetDeadThreshold(boxer.Owner, out var maxHealth))
            return;

        var healAmount = XenoBoxerRules.GetUppercutHealAmount(
            ko,
            maxHealth.Value.Float(),
            boxer.Comp.UppercutHealPercentPerKo,
            boxer.Comp.XenoVsXenoHealMultiplier,
            targetIsXeno);
        if (healAmount <= 0)
            return;

        _xeno.HealDamage((boxer.Owner, null), FixedPoint2.New(healAmount));
    }

    private float GetPunchDamage(XenoBoxerComponent boxer, EntityUid target)
    {
        var min = boxer.PunchDamageMin;
        var max = boxer.PunchDamageMax;

        if (HasComp<YautjaComponent>(target))
        {
            min = boxer.PunchYautjaDamageMin;
            max = boxer.PunchYautjaDamageMax;
        }
        else if (HasComp<SynthComponent>(target))
        {
            min = boxer.PunchSynthDamageMin;
            max = boxer.PunchSynthDamageMax;
        }

        return _random.NextFloat(min, max);
    }

    private void AddKo(Entity<XenoBoxerComponent> boxer, EntityUid target, float amount)
    {
        if (XenoBoxerRules.IsDifferentTarget(boxer.Comp.KoTarget, target))
            ResetKo(boxer);

        boxer.Comp.KoTarget = target;
        boxer.Comp.KoMeter = XenoBoxerRules.AddKo(boxer.Comp.KoMeter, amount);
        boxer.Comp.LastKoHitAt = _timing.CurTime;
        Dirty(boxer);
        _popup.PopupClient(
            Loc.GetString("cm-xeno-boxer-ko-meter", ("meter", boxer.Comp.KoMeter)),
            boxer.Owner,
            boxer.Owner);
    }

    private void ResetKo(Entity<XenoBoxerComponent> boxer)
    {
        boxer.Comp.KoTarget = null;
        boxer.Comp.KoMeter = 0;
        boxer.Comp.LastKoHitAt = TimeSpan.Zero;
        Dirty(boxer);
    }

    private void SetCooldown<TAction>(EntityUid user, TimeSpan cooldown) where TAction : BaseActionEvent
    {
        foreach (var action in _rmcActions.GetActionsWithEvent<TAction>(user))
            _actions.SetCooldown(action.AsNullable(), cooldown);
    }

    private void ReduceCooldown<TAction>(EntityUid user, EntityUid target, TimeSpan baseCooldown)
        where TAction : BaseActionEvent
    {
        var targetIsXeno = HasComp<XenoComponent>(target);
        foreach (var action in _rmcActions.GetActionsWithEvent<TAction>(user))
        {
            if (action.Comp.Cooldown is not { } cooldown || cooldown.End <= _timing.CurTime)
                continue;

            if (!targetIsXeno)
            {
                _actions.ClearCooldown(action.AsNullable());
                continue;
            }

            var reducedEnd = cooldown.End - baseCooldown / 2;
            _actions.SetCooldown(
                action.AsNullable(),
                cooldown.Start,
                reducedEnd < _timing.CurTime ? _timing.CurTime : reducedEnd);
        }
    }
}
