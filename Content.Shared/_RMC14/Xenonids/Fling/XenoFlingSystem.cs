using System.Numerics;
using Content.Shared._RMC14.CombatMode;
using Content.Shared._RMC14.Humanoid.Markings;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Heal;
using Content.Shared._RMC14.Xenonids.Rage;
using Content.Shared.CombatMode;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Xenonids.Fling;

public sealed partial class XenoFlingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private RMCPullingSystem _rmcPulling = default!;
    [Dependency] private RMCSlowSystem _rmcSlow = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private SharedXenoHealSystem _xenoHeal = default!;
    [Dependency] private XenoRageSystem _rage = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCDazedSystem _daze = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoFlingComponent, XenoFlingActionEvent>(OnXenoFlingAction);
    }

    private void OnXenoFlingAction(Entity<XenoFlingComponent> xeno, ref XenoFlingActionEvent args)
    {
        if (!_xeno.CanAbilityAttackTarget(xeno, args.Target))
            return;

        if (args.Handled)
            return;

        var harmIntent = !xeno.Comp.HasAltFling || _combatMode.IsInCombatMode(xeno);
        args.Handled = TryFling(xeno, args.Target, harmIntent);
    }

    private bool TryFling(Entity<XenoFlingComponent> xeno, EntityUid target, bool harmIntent)
    {
        var attempt = new XenoFlingAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);

        if (attempt.Cancelled)
            return false;

        if (_size.TryGetSize(target, out var size) && size >= RMCSizes.Big)
        {
            _popup.PopupClient(Loc.GetString("rmc-xeno-fling-too-big", ("target", target)), xeno, xeno,
                PopupType.MediumCaution);
            return false;
        }

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.Sound, xeno);

        var rage = _rage.GetRage(xeno.Owner);

        _rmcPulling.TryStopAllPullsFromAndOn(target);

        var damage = _damageable.TryChangeDamage(target,
            _xeno.TryApplyXenoSlashDamageMultiplier(target, xeno.Comp.Damage), origin: xeno, tool: xeno);
        if (damage?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(target, entityManager: EntityManager)
                .RemoveWhereAttachedEntity(o => o == xeno.Owner);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);
        }

        var healAmount = xeno.Comp.HealAmount;
        var throwRange = xeno.Comp.Range;
        var daze = false;

        if (rage >= 2)
        {
            throwRange += xeno.Comp.EnragedRange;
            healAmount += xeno.Comp.EnragedHealAmount;
            daze = true;
        }

        // Harm intent: fling away from xeno. Help intent: fling toward xeno.
        MapCoordinates origin;
        if (harmIntent)
        {
            origin = _transform.GetMapCoordinates(xeno);
        }
        else
        {
            var xenoPos = _transform.GetMapCoordinates(xeno).Position;
            var targetPos = _transform.GetMapCoordinates(target).Position;
            var diff = targetPos - xenoPos;
            var awayFromXeno = diff.LengthSquared() > 0.001f ? Vector2.Normalize(diff) : Vector2.UnitY;
            origin = new MapCoordinates(targetPos + awayFromXeno, _transform.GetMapCoordinates(xeno).MapId);
        }

        _rmcMelee.DoLunge(xeno, target);
        _xenoHeal.CreateHealStacks(xeno, healAmount, xeno.Comp.HealDelay, 1, xeno.Comp.HealDelay);

        if (!_net.IsServer)
            return true;

        _rmcSlow.TrySlowdown(target, xeno.Comp.SlowTime);
        _stun.TryParalyze(target, _xeno.TryApplyXenoDebuffMultiplier(target, xeno.Comp.ParalyzeTime), true);

        if (daze)
            _daze.TryDaze(target, xeno.Comp.DazeTime);

        _daze.TryDaze(target, xeno.Comp.DazeTime, true);
        _size.KnockBack(target, origin, throwRange, throwRange, xeno.Comp.ThrowSpeed);
        SpawnAttachedTo(xeno.Comp.Effect, target.ToCoordinates());

        return true;
    }
}
