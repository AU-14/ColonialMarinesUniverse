using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Stealth;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Flash;
using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Medical;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

using Robust.Shared.Map;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// Level-0 Early Warning Joe abilities: innate armor (Sculpted Form), Test of Faith and Disrupt, plus
/// the body rules that come with the role (no reset-key revival, D3). Optical Zoom and the punch
/// effects are plain components granted by the level table.
/// </summary>
public sealed partial class EWJoeAbilitySystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedDirectionalAttackBlockSystem _facing = default!;
    [Dependency] private SharedFlashSystem _flash = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private readonly HashSet<EntityUid> _pulsed = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, CMGetArmorEvent>(OnGetArmor);
        SubscribeLocalEvent<EWJoeComponent, EWJoeTestOfFaithActionEvent>(OnTestOfFaith);
        SubscribeLocalEvent<EWJoeComponent, EWJoeDisruptActionEvent>(OnDisrupt);
        SubscribeLocalEvent<EWJoeComponent, TargetBeforeDefibrillatorZapsEvent>(OnBeforeDefibZap);
        SubscribeLocalEvent<EWJoeComponent, RMCDefibrillatorAttemptEvent>(OnDefibAttempt);
    }

    /// <summary>
    /// Sculpted Form: flat melee and bullet armor that grows with level (balance A-4), plus the bullet
    /// bonus of a digested body armor while Scour lasts (E4, A-6).
    /// </summary>
    private void OnGetArmor(Entity<EWJoeComponent> ent, ref CMGetArmorEvent args)
    {
        var bonus = ent.Comp.Level * ent.Comp.ArmorPerLevel;
        args.Melee += bonus;
        args.Bullet += bonus;

        if (TryComp(ent, out EWJoeScourComponent? scour))
            args.Bullet += scour.BulletArmor;
    }

    /// <summary>
    /// The Test of Faith flash on one target: the shared call Judgement reuses per target. The upstream
    /// flash popup (which names the flasher) is suppressed; the victim is told what happened to them in the
    /// signal's terms instead (text pass A): a fumble line when the stun knocked something out of their
    /// hands (a gun line for Judgement when it was a gun), otherwise a stun line, then an eye line
    /// (Test of Faith) or the signal's voice (Judgement) in their chat.
    /// </summary>
    public bool TryFlash(Entity<EWJoeComponent> joe, EntityUid target, bool judgement = false)
    {
        if (HasComp<EntityActiveInvisibleComponent>(target))
            return false;

        // a pull or a wielded off-hand is a virtual item, not something the victim can fumble
        var held = _hands.EnumerateHeld(target).Where(i => !HasComp<VirtualItemComponent>(i)).ToList();
        // ECS-11: per target, so xenofauna get the Joe's multiplier before RMC's own reduction
        var stun = EWJoeTargetRules.ScaleStun(EntityManager, target, joe.Comp.TestOfFaithStun, joe.Comp.XenofaunaStunMultiplier);
        var hit = _flash.Flash(target, joe, null, (float) joe.Comp.TestOfFaithFlash.TotalMilliseconds,
            displayPopup: false, stunDuration: stun);

        if (hit && _net.IsServer)
            Afflict(target, held, judgement);

        return hit;
    }

    private void Afflict(EntityUid victim, List<EntityUid> heldBefore, bool judgement)
    {
        var dropped = false;
        var droppedGun = false;
        foreach (var item in heldBefore)
        {
            if (_hands.IsHolding(victim, item))
                continue;

            dropped = true;
            droppedGun |= HasComp<GunComponent>(item);
        }

        _messages.Sense(victim, _messages.Pick(VictimLines(judgement, dropped, droppedGun)));
        if (judgement)
            _messages.Whisper(victim, _messages.Pick(EWJoeMessageSystem.JudgementVoice));
        else
            _messages.Sense(victim, _messages.Pick(EWJoeMessageSystem.FaithEyes), PopupType.MediumCaution);
    }

    /// <summary>
    /// Which lines a flashed victim is told, by what the stun really did: a gun leaving their hand
    /// (Judgement), anything leaving it, or nothing.
    /// </summary>
    public static EWJoeLines VictimLines(bool judgement, bool dropped, bool droppedGun)
    {
        if (judgement)
        {
            return droppedGun ? EWJoeMessageSystem.JudgementGun
                : dropped ? EWJoeMessageSystem.JudgementFumble
                : EWJoeMessageSystem.JudgementSenses;
        }

        return dropped ? EWJoeMessageSystem.FaithFumble : EWJoeMessageSystem.FaithJolt;
    }

    /// <summary>
    /// Test of Faith (playtest 3): one chosen, living, visible target in range that faces the Joe while
    /// the Joe faces it is flashed and briefly paralyzed. Flash immunity (all synths) cancels it. A
    /// refused target leaves the action unspent.
    /// </summary>
    private void OnTestOfFaith(Entity<EWJoeComponent> ent, ref EWJoeTestOfFaithActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!CanTestFaith(ent, target, out var reason))
        {
            if (_net.IsServer && reason != null)
                _popup.PopupEntity(Loc.GetString(reason, ("target", target)), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        var hit = TryFlash(ent, target);
        if (!_net.IsServer)
            return;

        _popup.PopupEntity(Loc.GetString(hit ? "cmu-ew-joe-test-of-faith-done" : "cmu-ew-joe-test-of-faith-resisted", ("target", target)), ent, ent);
    }

    /// <summary>The single-target rule: living, visible, in range and line of sight, and mutual facing.</summary>
    public bool CanTestFaith(Entity<EWJoeComponent> joe, EntityUid target, out string? reason)
    {
        reason = null;
        if (target == joe.Owner || !TryComp(target, out MobStateComponent? mob) || !_mobState.IsAlive(target, mob))
            return false;

        if (HasComp<EntityActiveInvisibleComponent>(target)
            || !_examine.InRangeUnOccluded(joe.Owner, target, joe.Comp.TestOfFaithRange))
        {
            reason = "cmu-ew-joe-test-of-faith-unseen";
            return false;
        }

        if (!_facing.IsFacingTarget(joe.Owner, target) || !_facing.IsFacingTarget(target, joe.Owner))
        {
            reason = "cmu-ew-joe-test-of-faith-not-facing";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Disrupt: the level-0 pulse. Lights, headsets and batteries respond; doors and sentries arrive
    /// with Disrupt-II (A-2). The Joe's own gear is shielded, otherwise a dead headset and drained
    /// cells would mark the one Joe that was not hit.
    /// </summary>
    private void OnDisrupt(Entity<EWJoeComponent> ent, ref EWJoeDisruptActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var coordinates = Transform(ent).Coordinates;
        EmpAround(ent, coordinates, ent.Comp.DisruptRange, ent.Comp.DisruptEnergy, ent.Comp.DisruptDuration);

        if (_net.IsServer)
        {
            SpawnAtPosition(SharedEmpSystem.EmpPulseEffectPrototype, coordinates);

            // Disrupt-II (E5, A-2): doors, sapper traps and sentries are server-side systems
            var pulse = new EWJoeDisruptPulseEvent(ent, coordinates, ent.Comp.DisruptRange, ent.Comp.Level);
            RaiseLocalEvent(ref pulse);
        }

        _audio.PlayPredicted(SharedEmpSystem.EmpSound, coordinates, ent);
    }

    /// <summary>
    /// EMPs everything within <paramref name="range"/> of <paramref name="coordinates"/> except the Joe
    /// and what it carries (Disrupt, and the respawn's pulse).
    /// </summary>
    public void EmpAround(EntityUid joe, EntityCoordinates coordinates, float range, float energy, TimeSpan duration)
    {
        _pulsed.Clear();
        _lookup.GetEntitiesInRange(coordinates, range, _pulsed);

        foreach (var uid in _pulsed)
        {
            if (uid == joe || IsCarriedBy(joe, uid))
                continue;

            _emp.TryEmpEffects(uid, energy, duration, joe);
        }
    }

    /// <summary>A reset key or defibrillator does nothing to an Early Warning Joe (D3).</summary>
    private void OnBeforeDefibZap(Entity<EWJoeComponent> ent, ref TargetBeforeDefibrillatorZapsEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("working-joe-reboot-refused-ew"), ent, args.EntityUsingDefib);
    }

    /// <summary>
    /// Defence in depth for D3: the RMC revival attempt is shared by the zap and the chemical path,
    /// so any future non-zap revive is refused as well.
    /// </summary>
    private void OnDefibAttempt(Entity<EWJoeComponent> ent, ref RMCDefibrillatorAttemptEvent args)
    {
        args.Cancel();
    }

    /// <summary>True when <paramref name="uid"/> sits anywhere inside <paramref name="holder"/>'s transform tree.</summary>
    public bool IsCarriedBy(EntityUid holder, EntityUid uid)
    {
        var parent = Transform(uid).ParentUid;
        var guard = 0;
        while (parent.IsValid() && guard++ < 16)
        {
            if (parent == holder)
                return true;

            parent = Transform(parent).ParentUid;
        }

        return false;
    }
}
