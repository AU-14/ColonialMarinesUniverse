using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared._CMU14.CLFSubverter;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Synth;
using Content.Shared.AU14.CLF;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Client.GameStates;
using Robust.Shared.Network;
using Content.Shared._CMU14.SynthRepairer;
using Robust.Shared.Audio;

namespace Content.Server._CMU14.CLFSubvertedSynth;

//bashed together from the revolutionary stuff

public sealed partial class CLFSubvertedSynthRuleSystem : GameRuleSystem<CLFSubvertedSynthRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private IAdminLogManager _adminLogManager = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedSynthSystem _synth = default!;




    public readonly ProtoId<NpcFactionPrototype> CLFNPCFaction = "CLF";

    public override void Initialize()
    {
        base.Initialize();
        //TargetBeforeDefibrillatorZapsEvent doesn't work for some godawful reason
        SubscribeLocalEvent<CLFSubverterComponent, RMCDefibrillatorDamageModifyEvent>(OnSynthRevive, after: [typeof(RMCDefibrillatorSystem)]);
        SubscribeLocalEvent<SynthRepairerComponent, RMCDefibrillatorDamageModifyEvent>(OnSynthRepair, after: [typeof(RMCDefibrillatorSystem)]);
    }

    private void OnSynthRevive(EntityUid uid, CLFSubverterComponent comp, ref RMCDefibrillatorDamageModifyEvent args)
    {
        if (!HasComp<SynthComponent>(args.Target))
            return;

        if (!_mind.TryGetMind(args.Target, out var mindId, out var mind))
            return;

        _npcFaction.AddFaction(args.Target, comp.Faction);
        var subvertedComp = EnsureComp<CLFSubvertedSynthComponent>(args.Target);
        subvertedComp.Faction = comp.Faction;
        subvertedComp.AdditionalComponents = comp.AdditionalComponents;
        EntityManager.AddComponents(args.Target, comp.AdditionalComponents);
        EnsureComp<CLFMemberComponent>(args.Target);
        _adminLogManager.Add(LogType.Mind,
            LogImpact.Medium,
            $"{ToPrettyString(args.Target)} had a CLF synth subverter used on them");

        if (!_role.MindHasRole<CLFSubvertedSynthRoleComponent>(mindId))
            _role.MindAddRole(mindId, comp.Role);

        if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out var session))
            _antag.SendBriefing(session, Loc.GetString(comp.Briefing), Color.Red, comp.Sound ?? subvertedComp.CLFSubversionSound);
    }

    private void OnSynthRepair(EntityUid uid, SynthRepairerComponent comp, ref RMCDefibrillatorDamageModifyEvent args)
    {
        if (TryComp<CLFSubvertedSynthComponent>(args.Target, out var subverted))
            {
                EntityManager.RemoveComponents(args.Target, subverted.AdditionalComponents);
                _npcFaction.RemoveFaction(args.Target, subverted.Faction);
            }
        if (!HasComp<SynthComponent>(args.Target) && !HasComp<CLFSubvertedSynthComponent>(args.Target))
            return;
        if (HasComp<CLFSubverterComponent>(uid)) //idk how to remove a component from a prototype so this is an un-necessary workaround
            return;

        AddSynthResetReviveHeal(args.Target, args.Heal);

        if (!_mind.TryGetMind(args.Target, out var mindId, out var mind))
            return;
        //_synth.SetGunRestriction(args.Target, false);
        //_synth.SetMeleeRestriction(args.Target, true);
        RemCompDeferred<CLFSubvertedSynthComponent>(args.Target);
        RemCompDeferred<CLFMemberComponent>(args.Target);
        _adminLogManager.Add(LogType.Mind, LogImpact.Medium,
            $"{ToPrettyString(args.Target)} has been repaired from subversion.");

        _role.MindRemoveRole(mindId, "MindRoleCLFSubvertedSynth");
        if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out var session))
            _antag.SendBriefing(session, Loc.GetString("clf-subverted-synth-repaired"), Color.CornflowerBlue, null);
    }

    private void AddSynthResetReviveHeal(EntityUid target, DamageSpecifier heal)
    {
        if (!HasComp<SynthComponent>(target) ||
            !_mobState.IsDead(target) ||
            heal.DamageDict.Count == 0)
        {
            return;
        }

        if (!_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var deadThreshold) ||
            !TryComp<DamageableComponent>(target, out var damageable))
        {
            return;
        }

        var damageAfterZap = GetProjectedDamageAfterHeal(damageable, heal);
        if (damageAfterZap < deadThreshold.Value)
            return;

        var extraHeal = damageAfterZap - deadThreshold.Value + FixedPoint2.New(1);
        AddHealingToExistingDamage(damageable, heal, extraHeal);
    }

    private static FixedPoint2 GetProjectedDamageAfterHeal(DamageableComponent damageable, DamageSpecifier heal)
    {
        var total = FixedPoint2.Zero;
        foreach (var (type, current) in damageable.Damage.DamageDict)
        {
            var next = current + heal.DamageDict.GetValueOrDefault(type);
            if (next > FixedPoint2.Zero)
                total += next;
        }

        foreach (var (type, change) in heal.DamageDict)
        {
            if (change > FixedPoint2.Zero &&
                !damageable.Damage.DamageDict.ContainsKey(type))
            {
                total += change;
            }
        }

        return total;
    }

    private static void AddHealingToExistingDamage(DamageableComponent damageable, DamageSpecifier heal, FixedPoint2 amount)
    {
        foreach (var (type, current) in damageable.Damage.DamageDict)
        {
            if (amount <= FixedPoint2.Zero)
                return;

            var existing = heal.DamageDict.GetValueOrDefault(type);
            var projected = FixedPoint2.Max(FixedPoint2.Zero, current + existing);
            if (projected <= FixedPoint2.Zero)
                continue;

            var toHeal = FixedPoint2.Min(projected, amount);
            heal.DamageDict[type] = existing - toHeal;
            amount -= toHeal;
        }
    }
}
