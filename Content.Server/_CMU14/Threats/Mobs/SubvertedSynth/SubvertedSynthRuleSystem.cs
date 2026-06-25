using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared._CMU14.SynthRepairer;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._CMU14.Threats.Mobs.SubvertedSynth;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Synth;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Threats.Mobs.SubvertedSynth;

public sealed partial class SubvertedSynthRuleSystem : GameRuleSystem<SubvertedSynthRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLogManager = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedSynthSystem _synth = default!;
    public readonly ProtoId<NpcFactionPrototype> CLFNPCFaction = "CLF";

    public override void Initialize()
    {
        base.Initialize();

        // TargetBeforeDefibrillatorZapsEvent doesn't work for some godawful reason
        SubscribeLocalEvent<SynthSubverterComponent, RMCDefibrillatorDamageModifyEvent>(OnSynthRevive,
            after: [typeof(RMCDefibrillatorSystem)]);
        SubscribeLocalEvent<SynthRepairerComponent, RMCDefibrillatorDamageModifyEvent>(OnSynthRepair,
            after: [typeof(RMCDefibrillatorSystem)]);
    }

    private void OnSynthRevive(EntityUid uid, SynthSubverterComponent comp, ref RMCDefibrillatorDamageModifyEvent args)
    {
        if (!HasComp<SynthComponent>(args.Target))
            return;

        if (!_mind.TryGetMind(args.Target, out EntityUid mindId, out MindComponent? mind))
            return;

        _npcFaction.AddFaction(args.Target, comp.Faction);
        var subvertedComp = EnsureComp<SubvertedSynthComponent>(args.Target);
        subvertedComp.Faction = comp.Faction;
        subvertedComp.AdditionalComponents = comp.AdditionalComponents;
        EntityManager.AddComponents(args.Target, comp.AdditionalComponents);
        EnsureComp<CLFMemberComponent>(args.Target);
        _adminLogManager.Add(LogType.Mind,
            LogImpact.Medium,
            $"{ToPrettyString(args.Target)} had a CLF synth subverter used on them");

        if (!_role.MindHasRole<SubvertedSynthRoleComponent>(mindId))
            _role.MindAddRole(mindId, comp.Role);

        if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out ICommonSession? session))
        {
            _antag.SendBriefing(session, Loc.GetString(comp.Briefing), Color.Red,
                comp.Sound ?? subvertedComp.CLFSubversionSound);
        }
    }

    private void OnSynthRepair(EntityUid uid, SynthRepairerComponent comp, ref RMCDefibrillatorDamageModifyEvent args)
    {
        if (TryComp(args.Target, out SubvertedSynthComponent? subverted))
        {
            EntityManager.RemoveComponents(args.Target, subverted.AdditionalComponents);
            _npcFaction.RemoveFaction(args.Target, subverted.Faction);
        }

        if (!HasComp<SynthComponent>(args.Target) && !HasComp<SubvertedSynthComponent>(args.Target))
            return;
        if (HasComp<SynthSubverterComponent>(
            uid)) // idk how to remove a component from a prototype so this is an un-necessary workaround
            return;

        AddSynthResetReviveHeal(args.Target, args.Heal);

        if (!_mind.TryGetMind(args.Target, out EntityUid mindId, out MindComponent? mind))
            return;

        // _synth.SetGunRestriction(args.Target, false);
        // _synth.SetMeleeRestriction(args.Target, true);
        RemCompDeferred<SubvertedSynthComponent>(args.Target);
        RemCompDeferred<CLFMemberComponent>(args.Target);
        _adminLogManager.Add(LogType.Mind, LogImpact.Medium,
            $"{ToPrettyString(args.Target)} has been repaired from subversion.");

        _role.MindRemoveRole(mindId, "MindRoleCLFSubvertedSynth");
        if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out ICommonSession? session))
            _antag.SendBriefing(session, Loc.GetString("clf-subverted-synth-repaired"), Color.CornflowerBlue, null);
    }

    private void AddSynthResetReviveHeal(EntityUid target, DamageSpecifier heal)
    {
        if (!HasComp<SynthComponent>(target) || !_mobState.IsDead(target) || heal.DamageDict.Count == 0)
            return;

        if (!_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out FixedPoint2? deadThreshold)
            || !TryComp(target, out DamageableComponent? damageable))
            return;

        FixedPoint2 damageAfterZap = SubvertedSynthRuleSystem.GetProjectedDamageAfterHeal(damageable, heal);

        if (damageAfterZap < deadThreshold.Value)
            return;

        FixedPoint2 extraHeal = damageAfterZap - deadThreshold.Value + FixedPoint2.New(1);
        SubvertedSynthRuleSystem.AddHealingToExistingDamage(damageable, heal, extraHeal);
    }

    private static FixedPoint2 GetProjectedDamageAfterHeal(DamageableComponent damageable, DamageSpecifier heal)
    {
        FixedPoint2 total = FixedPoint2.Zero;
        foreach ((string type, FixedPoint2 current) in damageable.Damage.DamageDict)
        {
            FixedPoint2 next = current + heal.DamageDict.GetValueOrDefault(type);
            if (next > FixedPoint2.Zero)
                total += next;
        }

        foreach ((string type, FixedPoint2 change) in heal.DamageDict)
        {
            if (change > FixedPoint2.Zero && !damageable.Damage.DamageDict.ContainsKey(type))
                total += change;
        }

        return total;
    }

    private static void AddHealingToExistingDamage(DamageableComponent damageable, DamageSpecifier heal,
        FixedPoint2 amount)
    {
        foreach ((string type, FixedPoint2 current) in damageable.Damage.DamageDict)
        {
            if (amount <= FixedPoint2.Zero)
                return;

            FixedPoint2 existing = heal.DamageDict.GetValueOrDefault(type);
            FixedPoint2 projected = FixedPoint2.Max(FixedPoint2.Zero, current + existing);

            if (projected <= FixedPoint2.Zero)
                continue;

            FixedPoint2 toHeal = FixedPoint2.Min(projected, amount);
            heal.DamageDict[type] = existing - toHeal;
            amount -= toHeal;
        }
    }
}
