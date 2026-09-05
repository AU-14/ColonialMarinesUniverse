using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.CMU14.SynthRepairer;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.CMU14.Threats.Mobs.SubvertedSynth;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Synth;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.CMU14.Threats.Mobs.SubvertedSynth;

public sealed partial class SubvertedSynthRuleSystem : GameRuleSystem<SubvertedSynthRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLogManager = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedSynthSystem _synth = default!;
    public readonly ProtoId<NpcFactionPrototype> CLFNPCFaction = "CLF";
    private readonly HashSet<EntityUid> _changingSubversion = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SynthSubverterComponent, RMCDefibrillatorRevivedEvent>(OnSynthRevive);
        SubscribeLocalEvent<SynthSubverterComponent, RMCDefibrillatorDamageModifyEvent>(OnValidateSubversion,
            after: [typeof(RMCDefibrillatorSystem)]);
        SubscribeLocalEvent<SynthRepairerComponent, RMCDefibrillatorRevivedEvent>(OnSynthRepair);
        SubscribeLocalEvent<SynthRepairerComponent, RMCDefibrillatorDamageModifyEvent>(OnSynthRepairHeal,
            after: [typeof(RMCDefibrillatorSystem)]);
    }

    private void OnValidateSubversion(EntityUid uid, SynthSubverterComponent comp,
        ref RMCDefibrillatorDamageModifyEvent args)
    {
        // Reject an invalid configuration before shock trauma or revival commits.
        // Reset-key inheritance must not turn an invalid subversion into a reset.
        if (_changingSubversion.Contains(args.Target) || !IsSubversionRole(comp.Role))
            args.Cancelled = true;
    }

    private bool IsSubversionRole(string role)
        => _prototypes.TryIndex<EntityPrototype>(role, out var prototype)
           && prototype.Components.ContainsKey("SubvertedSynthRole");

    private void OnSynthRevive(EntityUid uid, SynthSubverterComponent comp, ref RMCDefibrillatorRevivedEvent args)
    {
        if (!_changingSubversion.Add(args.Target))
            return;

        try
        {
            ApplySubversion(comp, args.Target);
        }
        finally
        {
            _changingSubversion.Remove(args.Target);
        }
    }

    private void ApplySubversion(SynthSubverterComponent comp, EntityUid target)
    {
        if (TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target)
            || !HasComp<SynthComponent>(target) || !IsSubversionRole(comp.Role))
            return;

        if (!_mind.TryGetMind(target, out EntityUid mindId, out MindComponent? mind))
            return;

        var subvertedComp = EnsureComp<SubvertedSynthComponent>(target);
        // Always retire the previous overlay before taking a new snapshot. In
        // particular, repeated keys must not remember their own radio/access
        // configuration as the patient's independent state.
        if (!RestoreSubversionOverlay((target, subvertedComp)))
            return;

        var alreadyMember = _npcFaction.IsMember(target, comp.Faction);
        _npcFaction.AddFaction(target, comp.Faction);
        if (!IsCurrentSubversion((target, subvertedComp)))
            return;
        subvertedComp.Faction = comp.Faction;
        subvertedComp.AddedFactionTo = alreadyMember ? null : Comp<NpcFactionMemberComponent>(target);
        subvertedComp.AdditionalComponents = comp.AdditionalComponents;
        if (!ApplySubversionOverlay((target, subvertedComp), comp.AdditionalComponents))
            return;
        if (comp.Faction == CLFNPCFaction && !HasComp<CLFMemberComponent>(target))
        {
            var member = new CLFMemberComponent();
            subvertedComp.AddedClfMember = member;
            AddComp(target, member);
        }
        if (!IsCurrentSubversion((target, subvertedComp))
            || !_mind.TryGetMind(target, out var currentMind, out _) || currentMind != mindId)
            return;

        _adminLogManager.Add(LogType.Mind,
            LogImpact.Medium,
            $"{ToPrettyString(target)} had a {comp.Faction} synth subverter used on them");

        var markedRoles = 0;
        var matchingRole = false;
        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            if (!HasComp<SubvertedSynthRoleComponent>(role))
                continue;

            markedRoles++;
            matchingRole |= MetaData(role).EntityPrototype?.ID == comp.Role;
        }

        // Retain the exact role on repeated use. A different key, or duplicate
        // roles from older content, replaces only roles owned by subversion.
        if (markedRoles != 1 || !matchingRole)
        {
            if (markedRoles > 0)
                _role.MindRemoveRole<SubvertedSynthRoleComponent>(mindId);
            if (!IsCurrentSubversion((target, subvertedComp))
                || !_mind.TryGetMind(target, out currentMind, out _) || currentMind != mindId)
                return;
            _role.MindAddRole(mindId, comp.Role);
        }

        if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out ICommonSession? session))
        {
            _antag.SendBriefing(session, Loc.GetString(comp.Briefing), Color.Red,
                comp.Sound ?? subvertedComp.CLFSubversionSound);
        }
    }

    private void OnSynthRepairHeal(EntityUid uid, SynthRepairerComponent comp, ref RMCDefibrillatorDamageModifyEvent args)
    {
        if (_changingSubversion.Contains(args.Target))
        {
            args.Cancelled = true;
            return;
        }
        if (!args.Cancelled && !HasComp<SynthSubverterComponent>(uid))
            AddSynthResetReviveHeal(args.Target, args.Heal);
    }

    private void OnSynthRepair(EntityUid uid, SynthRepairerComponent comp, ref RMCDefibrillatorRevivedEvent args)
    {
        // Subversion keys inherit the normal reset-key component. They must never
        // run repair cleanup on the faction/components just added by subversion.
        if (HasComp<SynthSubverterComponent>(uid) || !_changingSubversion.Add(args.Target))
            return;
        try
        {
            ResetSubversion(args.Target);
        }
        finally
        {
            _changingSubversion.Remove(args.Target);
        }
    }

    private void ResetSubversion(EntityUid target)
    {
        if (TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target)
            || !HasComp<SynthComponent>(target) && !HasComp<SubvertedSynthComponent>(target))
            return;

        if (TryComp(target, out SubvertedSynthComponent? subverted))
        {
            if (!RestoreSubversionOverlay((target, subverted)))
                return;
            // Synchronous retirement prevents a later key in this tick from
            // inheriting a component already queued for removal by this reset.
            RemComp(target, subverted);
        }

        if (!_mind.TryGetMind(target, out EntityUid mindId, out MindComponent? mind))
            return;

        _adminLogManager.Add(LogType.Mind, LogImpact.Medium,
            $"{ToPrettyString(target)} has been repaired from subversion.");

        if (_role.MindHasRole<SubvertedSynthRoleComponent>(mindId))
            _role.MindRemoveRole<SubvertedSynthRoleComponent>(mindId);
        if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out ICommonSession? session))
            _antag.SendBriefing(session, Loc.GetString("clf-subverted-synth-repaired"), Color.CornflowerBlue, null);
    }

    private bool IsCurrentSubversion(Entity<SubvertedSynthComponent> ent)
        => !TerminatingOrDeleted(ent.Owner) && !EntityManager.IsQueuedForDeletion(ent.Owner)
           && TryComp<SubvertedSynthComponent>(ent.Owner, out var current) && ReferenceEquals(current, ent.Comp);

    private bool ApplySubversionOverlay(Entity<SubvertedSynthComponent> ent, ComponentRegistry registry)
    {
        foreach (var entry in registry.Values)
        {
            if (!IsCurrentSubversion(ent))
                return false;

            var type = entry.Component.GetType();
            Component? previous = null;
            if (EntityManager.TryGetComponent(ent.Owner, type, out var existing))
            {
                previous = (Component) _serialization.CreateCopy(existing, notNullableOverride: true);
                RemComp(ent.Owner, existing);
                if (!IsCurrentSubversion(ent))
                    return false;
                // A shutdown callback may have installed another owner's
                // component. This operation has no authority over that instance.
                if (HasComp(ent.Owner, type))
                    continue;
            }

            var applied = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
            ent.Comp.ComponentOverlays[type] = (applied, previous);
            AddComp(ent.Owner, applied);
        }
        return IsCurrentSubversion(ent);
    }

    private bool RestoreSubversionOverlay(Entity<SubvertedSynthComponent> ent)
    {
        if (!IsCurrentSubversion(ent))
            return false;

        // Detach the ownership record before component lifecycle callbacks run.
        var overlays = ent.Comp.ComponentOverlays;
        ent.Comp.ComponentOverlays = new();
        var faction = ent.Comp.AddedFactionTo;
        ent.Comp.AddedFactionTo = null;
        var member = ent.Comp.AddedClfMember;
        ent.Comp.AddedClfMember = null;
        foreach (var (type, overlay) in overlays)
        {
            if (!IsCurrentSubversion(ent))
                return false;
            if (!EntityManager.TryGetComponent(ent.Owner, type, out var current)
                || !ReferenceEquals(current, overlay.Applied))
                continue;

            RemComp(ent.Owner, current);
            if (!IsCurrentSubversion(ent))
                return false;
            if (overlay.Previous != null && !HasComp(ent.Owner, type))
                AddComp(ent.Owner, overlay.Previous);
        }

        if (!IsCurrentSubversion(ent))
            return false;
        if (faction != null && TryComp<NpcFactionMemberComponent>(ent.Owner, out var currentFaction)
            && ReferenceEquals(faction, currentFaction))
            _npcFaction.RemoveFaction(ent.Owner, ent.Comp.Faction);
        if (!IsCurrentSubversion(ent))
            return false;
        if (member != null && TryComp<CLFMemberComponent>(ent.Owner, out var currentMember)
            && ReferenceEquals(member, currentMember))
            RemComp(ent.Owner, member);
        return IsCurrentSubversion(ent);
    }

    private void AddSynthResetReviveHeal(EntityUid target, DamageSpecifier heal)
    {
        if (!HasComp<SynthComponent>(target) || !_mobState.IsDead(target) || heal.DamageDict.Count == 0)
            return;

        if (!_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out FixedPoint2? deadThreshold)
            || !TryComp(target, out DamageableComponent? damageable))
            return;

        var damage = _damageable.GetAllDamage((target, damageable));
        FixedPoint2 damageAfterZap = SubvertedSynthRuleSystem.GetProjectedDamageAfterHeal(damage, heal);

        if (damageAfterZap < deadThreshold.Value)
            return;

        FixedPoint2 extraHeal = damageAfterZap - deadThreshold.Value + FixedPoint2.New(1);
        SubvertedSynthRuleSystem.AddHealingToExistingDamage(damage, heal, extraHeal);
    }

    private static FixedPoint2 GetProjectedDamageAfterHeal(DamageSpecifier damage, DamageSpecifier heal)
    {
        FixedPoint2 total = FixedPoint2.Zero;
        foreach (var (type, current) in damage.DamageDict)
        {
            FixedPoint2 next = current + heal.DamageDict.GetValueOrDefault(type);
            if (next > FixedPoint2.Zero)
                total += next;
        }

        foreach (var (type, change) in heal.DamageDict)
        {
            if (change > FixedPoint2.Zero && !damage.DamageDict.ContainsKey(type))
                total += change;
        }

        return total;
    }

    private static void AddHealingToExistingDamage(DamageSpecifier damage, DamageSpecifier heal,
        FixedPoint2 amount)
    {
        foreach (var (type, current) in damage.DamageDict)
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
