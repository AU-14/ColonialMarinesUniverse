using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Finesse;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Impale;

public sealed partial class XenoImpaleSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedColorFlashEffectSystem _flash = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;

    private EntityQuery<RMCCooldownOnMissComponent> _missCooldownQuery;

    public override void Initialize()
    {
        base.Initialize();

        _missCooldownQuery = GetEntityQuery<RMCCooldownOnMissComponent>();

        SubscribeLocalEvent<XenoImpaleComponent, XenoImpaleActionEvent>(OnXenoImpaleAction);
    }

    private void OnXenoImpaleAction(Entity<XenoImpaleComponent> xeno, ref XenoImpaleActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Entity;
        if (target == null ||
            TerminatingOrDeleted(target.Value) ||
            !_xeno.CanAbilityAttackTarget(xeno, target.Value, true))
        {
            ImpaleMiss(xeno, xeno.Comp.Animation, xeno.Comp.Sound, args.Target, args.Action);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        var targetId = target.Value;

        if (HasComp<XenoMarkedComponent>(targetId))
        {
            if (xeno.Comp.Emote is { } emote)
                _emote.TryEmoteWithChat(xeno, emote, cooldown: xeno.Comp.EmoteCooldown);

            var secondHit = EnsureComp<XenoSecondImpaleComponent>(targetId);
            secondHit.AP = xeno.Comp.AP;
            secondHit.Animation = xeno.Comp.Animation;
            secondHit.Damage = xeno.Comp.Damage;
            secondHit.ImpaleAt = _timing.CurTime + xeno.Comp.SecondImpaleTime;
            secondHit.Origin = xeno;
            secondHit.Sound = xeno.Comp.Sound;

            RemCompDeferred<XenoMarkedComponent>(targetId);
        }

        Impale(xeno.Comp.Damage, xeno.Comp.AP, xeno.Comp.Animation, xeno.Comp.Sound, targetId, xeno);

    }

    private void ImpaleMiss(Entity<XenoImpaleComponent> xeno, EntProtoId animation, SoundSpecifier sound, EntityCoordinates target, EntityUid action)
    {
        if (_net.IsClient)
            return;

        if (_missCooldownQuery.TryComp(action, out var cooldown))
            _actions.SetIfBiggerCooldown(action, cooldown.MissCooldown);

        DoMissLunge(xeno, target);

        _audio.PlayPvs(sound, xeno);
        SpawnAttachedTo(animation, target);
    }

    private void DoMissLunge(EntityUid xeno, EntityCoordinates target)
    {
        var xform = Transform(xeno);
        var targetMap = _transform.ToMapCoordinates(target);
        if (targetMap.MapId == MapId.Nullspace ||
            targetMap.MapId != _transform.GetMapCoordinates(xeno, xform).MapId)
        {
            return;
        }

        var localPos = Vector2.Transform(targetMap.Position, _transform.GetInvWorldMatrix(xform));
        localPos = xform.LocalRotation.RotateVec(localPos);
        if (localPos.LengthSquared() <= 0.001f)
            localPos = _transform.GetWorldRotation(xform).ToWorldVec();

        _melee.DoLunge(xeno, xeno, Angle.Zero, localPos, null);
    }

    private void Impale(DamageSpecifier damage, int aP, EntProtoId animation, SoundSpecifier sound, EntityUid target, EntityUid xeno)
    {
        //TODO RMC14 targets chest
        var finalDamage = _xeno.TryApplyXenoSlashDamageMultiplier(target, damage);
        var damageTaken = _damage.TryChangeDamage(
            target,
            finalDamage,
            armorPiercing: aP,
            origin: xeno,
            tool: xeno,
            impact: DamageImpact.XenoRendingSlash(3) with
            {
                Contact = DamageImpactContact.Stab,
                Penetration = DamageImpactPenetration.High,
            });
        if (damageTaken?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(target, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno);
            _flash.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);
        }

        _rmcMelee.DoLunge(xeno, target);

        if (_net.IsClient)
            return;

        _audio.PlayPvs(sound, xeno);
        SpawnAttachedTo(animation, target.ToCoordinates());
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var impaleQuery = EntityQueryEnumerator<XenoSecondImpaleComponent>();

        while (impaleQuery.MoveNext(out var uid, out var impale))
        {
            if (impale.ImpaleAt > time)
                continue;

            Impale(impale.Damage, impale.AP, impale.Animation, impale.Sound, uid, impale.Origin);
            RemCompDeferred<XenoSecondImpaleComponent>(uid);
        }
    }
}
