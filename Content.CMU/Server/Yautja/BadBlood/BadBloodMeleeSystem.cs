using Content.Shared._RMC14.Slow;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-604, ECS-609, STATE-603: what a Bad Blood melee weapon (<see cref="BadBloodMeleeComponent"/>) does on a
/// hit, so fighting up close pays. A living human who is not of the clan is staggered (rate-limited per target) and
/// pays charge back into the wielder's bracer. Every living target is marked; if a worthy one dies within the rule's
/// Blooded kill window, the striker earns Honour. CMU14 ARCH-605: a Berserker's stagger lasts StaggerMultiplier times
/// as long. CMU14 ECS-703: a Blood Rage adds RageMeleeMultiplier. CMU14 STATE-701: a grunt's Blooded kill pays
/// GruntBloodedKillBonus more.
/// </summary>
public sealed partial class BadBloodMeleeSystem : EntitySystem
{
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private BadBloodProvenanceSystem _provenance = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodMeleeComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<BadBloodMeleeMarkComponent, MobStateChangedEvent>(OnMarkedMobStateChanged);
    }

    private void OnMeleeHit(Entity<BadBloodMeleeComponent> ent, ref MeleeHitEvent args)
    {
        // examining the weapon raises an IsHit = false copy of this event
        if (!args.IsHit || args.Handled || !_badBlood.IsBadBlood(args.User))
            return;

        var now = _timing.CurTime;

        // CMU14 ARCH-605: a Berserker's stagger lasts longer
        var stagger = ent.Comp.StaggerSlow;
        if (TryComp(args.User, out BadBloodBerserkerComponent? berserker))
        {
            stagger *= berserker.StaggerMultiplier;

            // CMU14 ECS-703: a Blood Rage hits harder
            if (HasComp<BadBloodBloodRageComponent>(args.User) && berserker.RageMeleeMultiplier > 1f)
                args.BonusDamage += args.BaseDamage * (berserker.RageMeleeMultiplier - 1f);
        }

        foreach (var target in args.HitEntities)
        {
            if (target == args.User ||
                !TryComp(target, out MobStateComponent? mobState) ||
                _mobState.IsDead(target, mobState))
            {
                continue;
            }

            // STATE-603: the damage lands after this event, so a killing blow still finds its mark
            var mark = EnsureComp<BadBloodMeleeMarkComponent>(target);
            mark.Striker = args.User;
            mark.At = now;

            // a downed marine is no longer a fight: no stagger, and no charge to farm from them
            if (!HasComp<HumanoidProfileComponent>(target) || _provenance.IsClanKind(target) ||
                !_mobState.IsAlive(target, mobState))
            {
                continue;
            }

            // ECS-604: one stagger per target per cooldown, so a second quick hit does not extend it
            if (now >= mark.NextStagger)
            {
                _slow.TrySlowdown(target, stagger);
                mark.NextStagger = now + ent.Comp.StaggerCooldown;
            }

            // ECS-609: blood for charge
            if (ent.Comp.ChargeRefund > 0f && _power.TryGetWornBracer(args.User, out var bracer))
                _power.RegenPower(bracer, ent.Comp.ChargeRefund);
        }
    }

    private void OnMarkedMobStateChanged(Entity<BadBloodMeleeMarkComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var striker = ent.Comp.Striker;
        var at = ent.Comp.At;
        RemCompDeferred<BadBloodMeleeMarkComponent>(ent);

        if (TerminatingOrDeleted(striker) ||
            !_rule.TryGetRule(out var rule) ||
            _timing.CurTime - at > rule.Comp.BloodedKillWindow ||
            !_provenance.IsWorthyPrey(ent))
        {
            return;
        }

        var honour = rule.Comp.BloodedKillHonour;

        // CMU14 STATE-701: a grunt (no caste, not the Hunt Leader) earns a little more for a Blooded kill
        if (!HasComp<BadBloodCasteComponent>(striker) && !HasComp<BadBloodLeaderComponent>(striker))
            honour += rule.Comp.GruntBloodedKillBonus;
        if (!_rule.AddHonour(striker, honour))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-blooded-kill", ("honour", honour.ToString())),
            striker, striker, PopupType.Medium);
    }
}
