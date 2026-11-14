using System.Numerics;
using Content.Shared._RMC14.Stun;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

// ECS-01/ECS-03 (W7, node N10): the xenofauna branches of the Joe's close-quarters kit. Every branch
// tests EWJoeTargetRules.IsXenofauna and the matching level-table component; humans are untouched.
//   0A Industrial Grip: unarmed hits +50% and a 1 s slow (EWJoeIndustrialGripComponent).
//   1B Overpressure:    Drown on xenofauna is a burst and a shove (EWJoeOverpressureComponent).
//   2A Chitin Crack:    Slam on xenofauna shreds armor (EWJoeChitinCrackComponent, EWJoeArmorShredSystem).
//   2B Pin:             Choke on xenofauna roots and bruises (EWJoePinComponent, EWJoePinningComponent).
// Subscriptions stay in EWJoeCombatSystem.cs.
public sealed partial class EWJoeCombatSystem
{
    private static readonly EWJoeOverpressureComponent DefaultOverpressure = new();
    private static readonly EWJoePinComponent DefaultPin = new();

    #region Xenofauna

    /// <summary>Xenofauna (W4/R1): the one rule the anti-xeno branches share.</summary>
    public bool IsXenofauna(EntityUid target)
    {
        return EWJoeTargetRules.IsXenofauna(EntityManager, target);
    }

    /// <summary>
    /// Xenofauna RMC treats as large (<see cref="RMCSizes.Big"/> and up: boiler, carrier, praetorian,
    /// ravager, crusher, queen, king). The same threshold <see cref="RMCSizeStunSystem.KnockBack"/>
    /// refuses without <c>ignoreSize</c>. Joe knockbacks do not move them.
    /// </summary>
    public bool IsLargeXenofauna(EntityUid target)
    {
        return IsXenofauna(target) && _sizeStun.TryGetSize(target, out var size) && size >= RMCSizes.Big;
    }

    /// <summary>
    /// Shoves <paramref name="target"/> <paramref name="distance"/> tiles straight away from the Joe
    /// (along its facing if they share a spot). Uncompensated like RMC's own slams, so the impact speed is
    /// the nominal one. Large xenofauna are not moved; returns whether a throw started.
    /// </summary>
    private bool KnockBackFromJoe(EntityUid joe, EntityUid target, float distance, float speed)
    {
        if (distance <= 0 || IsLargeXenofauna(target))
            return false;

        var joePos = _transform.GetMapCoordinates(joe);
        var targetPos = _transform.GetMapCoordinates(target);
        var offset = targetPos.MapId == joePos.MapId ? targetPos.Position - joePos.Position : Vector2.Zero;
        var direction = offset.LengthSquared() < 0.01f ? _transform.GetWorldRotation(joe).ToWorldVec() : offset;
        if (direction.LengthSquared() < 0.01f)
            direction = Vector2.UnitY;

        return _throwing.TryThrow(target, direction.Normalized() * distance, speed, user: joe, compensateFriction: false);
    }

    /// <summary>Lets go of <paramref name="target"/> if the Joe is pulling it, ending any choke or pin.</summary>
    private void ReleaseIfHeld(EntityUid joe, EntityUid target)
    {
        if (TryComp(joe, out EWJoeChokeComponent? choke) && choke.Target == target)
            StopChoke(joe, "released for a strike");

        _rmcPulling.TryStopUserPullIfPulling(joe, target);
    }

    #endregion

    #region 0A Industrial Grip

    /// <summary>
    /// Unarmed hits only: the event is raised on the weapon, and a bare-handed Joe is its own weapon (the
    /// machete raises it on itself). The bonus lands per target, so a wide swing through a human and a
    /// xeno only hurts the xeno more.
    /// </summary>
    private void OnIndustrialGripHit(Entity<EWJoeIndustrialGripComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Weapon != ent.Owner || args.BaseDamage.Empty || ent.Comp.BonusMultiplier <= 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (hit == ent.Owner || !IsXenofauna(hit))
                continue;

            IndustrialGripStrike(ent, hit, args.BaseDamage);
        }
    }

    /// <summary>The 0A bonus for one xenofauna target: extra damage and the short slow.</summary>
    public void IndustrialGripStrike(Entity<EWJoeIndustrialGripComponent> joe, EntityUid target, DamageSpecifier baseDamage)
    {
        _damageable.TryChangeDamage(target, baseDamage * joe.Comp.BonusMultiplier, origin: joe, tool: joe);
        _slow.TrySlowdown(target, joe.Comp.Slow);
    }

    #endregion

    #region 1B Overpressure

    /// <summary>
    /// Drown on xenofauna with the 1B kit: no hold and no grab (xenos cannot be grabbed before level 2),
    /// just a burst of damage and a shove. A held body is let go first so it can fly.
    /// </summary>
    public void Overpressure(Entity<EWJoeComponent> joe, EntityUid target)
    {
        var over = CompOrNull<EWJoeOverpressureComponent>(joe) ?? DefaultOverpressure;

        ReleaseIfHeld(joe, target);
        _damageable.TryChangeDamage(target, new DamageSpecifier(_proto.Index(Blunt), over.Damage), origin: joe);
        KnockBackFromJoe(joe, target, over.KnockbackDistance, over.KnockbackSpeed);

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-overpressure-done", ("target", target)), target, joe, PopupType.MediumCaution);
    }

    #endregion

    #region 2A Chitin Crack

    /// <summary>Slam's xeno branch: cracks the carapace when the Joe has the 2A kit.</summary>
    private void TryChitinCrack(EntityUid joe, EntityUid victim)
    {
        if (!IsXenofauna(victim) || !TryComp(joe, out EWJoeChitinCrackComponent? crack))
            return;

        _shred.Shred(victim, crack.ArmorShred, crack.Duration);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-chitin-crack", ("target", victim)), victim, joe, PopupType.MediumCaution);
    }

    #endregion

    #region 2B Pin

    /// <summary>
    /// Starts a pin on <paramref name="target"/>: the choke marker plus the pin state, and the first root.
    /// No grab check (the Choke action and <see cref="StartChoke"/> check it); without a grab the next
    /// tick ends the hold.
    /// </summary>
    public void Pin(Entity<EWJoeComponent> joe, EntityUid target)
    {
        var kit = CompOrNull<EWJoePinComponent>(joe) ?? DefaultPin;

        var choke = EnsureComp<EWJoeChokeComponent>(joe);
        choke.Target = target;

        var pinning = EnsureComp<EWJoePinningComponent>(joe);
        pinning.Target = target;
        pinning.DamageTaken = 0;

        _slow.TryRoot(target, kit.RootDuration);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-pin-start", ("target", target)), target, joe, PopupType.MediumCaution);
    }

    /// <summary>One second of a pin: refresh the root and deal the damage over time.</summary>
    public void PinTick(Entity<EWJoeComponent> joe, EntityUid target)
    {
        var kit = CompOrNull<EWJoePinComponent>(joe) ?? DefaultPin;

        _slow.TryRoot(target, kit.RootDuration);
        if (kit.DamagePerSecond > 0)
            _damageable.TryChangeDamage(target, new DamageSpecifier(_proto.Index(Blunt), kit.DamagePerSecond), origin: joe);
    }

    private void OnPinnerDamaged(Entity<EWJoePinningComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        var taken = delta.GetTotal();
        if (taken <= 0)
            return;

        ent.Comp.DamageTaken += taken;
        var kit = CompOrNull<EWJoePinComponent>(ent) ?? DefaultPin;
        if (ent.Comp.DamageTaken < kit.BreakDamage)
            return;

        var target = ent.Comp.Target;
        StopChoke(ent, "pin broken");
        if (Exists(target))
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-pin-broken", ("target", target)), ent, ent, PopupType.SmallCaution);
    }

    #endregion
}
