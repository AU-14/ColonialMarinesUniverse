using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Flash;
using Content.Shared.Medical;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

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
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private readonly HashSet<Entity<MobStateComponent>> _targets = new();
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

    /// <summary>Sculpted Form: flat melee and bullet armor that grows with level (balance A-4).</summary>
    private void OnGetArmor(Entity<EWJoeComponent> ent, ref CMGetArmorEvent args)
    {
        var bonus = ent.Comp.Level * ent.Comp.ArmorPerLevel;
        args.Melee += bonus;
        args.Bullet += bonus;
    }

    /// <summary>
    /// Test of Faith: every living, uncontained target in range that the Joe can see, that faces the
    /// Joe while the Joe faces it, is flashed and briefly paralyzed. Flash immunity (all synths)
    /// cancels it per target. The hit count is reported from the server, which is the only side
    /// that sees every target.
    /// </summary>
    private void OnTestOfFaith(Entity<EWJoeComponent> ent, ref EWJoeTestOfFaithActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var range = ent.Comp.TestOfFaithRange;
        _targets.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, range, _targets, LookupFlags.Uncontained);

        var hit = 0;
        foreach (var target in _targets)
        {
            if (target.Owner == ent.Owner || !_mobState.IsAlive(target, target.Comp))
                continue;

            if (HasComp<EntityActiveInvisibleComponent>(target))
                continue;

            if (!_examine.InRangeUnOccluded(ent, target, range))
                continue;

            if (!_facing.IsFacingTarget(ent, target) || !_facing.IsFacingTarget(target, ent))
                continue;

            if (_flash.Flash(target, ent, null, (float) ent.Comp.TestOfFaithFlash.TotalMilliseconds, stunDuration: ent.Comp.TestOfFaithStun))
                hit++;
        }

        if (!_net.IsServer)
            return;

        var message = hit == 0
            ? Loc.GetString("ew-joe-test-of-faith-none")
            : Loc.GetString("ew-joe-test-of-faith-hit", ("count", hit));
        _popup.PopupEntity(message, ent, ent);
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
        _pulsed.Clear();
        _lookup.GetEntitiesInRange(coordinates, ent.Comp.DisruptRange, _pulsed);

        foreach (var uid in _pulsed)
        {
            if (uid == ent.Owner || IsCarriedBy(ent, uid))
                continue;

            _emp.TryEmpEffects(uid, ent.Comp.DisruptEnergy, ent.Comp.DisruptDuration, ent);
        }

        if (_net.IsServer)
            SpawnAtPosition(SharedEmpSystem.EmpPulseEffectPrototype, coordinates);

        _audio.PlayPredicted(SharedEmpSystem.EmpSound, coordinates, ent);
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
    private bool IsCarriedBy(EntityUid holder, EntityUid uid)
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
