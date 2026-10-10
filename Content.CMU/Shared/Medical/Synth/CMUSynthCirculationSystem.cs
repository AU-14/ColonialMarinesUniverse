using System.Linq;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.IV;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.Synth;

public sealed partial class CMUSynthCirculationSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> ConstructionSkill = "RMCSkillConstruction";
    private const string SynthBloodReagent = "RMCSynthBlood";

    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private RMCRepairableSystem _repairable = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedOrganHealthSystem _organHealth = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSynthSystem _synth = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SkillsSystem _skills = default!;

    public override void Initialize()
    {
        base.Initialize();

        // MapInit lands after ComponentStartup (where MakeSynth strips IVDripTarget), both for
        // prototype synths and for jobs that AddComponentSpecial the synth on later
        SubscribeLocalEvent<SynthComponent, MapInitEvent>(OnSynthMapInit);
        SubscribeLocalEvent<CMUSynthCirculationComponent, RMCSynthRepairToolUseAttemptEvent>(OnRepairToolUseAttempt);
        SubscribeLocalEvent<CMUSynthCirculationComponent, CMUSynthOrganRepairDoAfterEvent>(OnOrganRepairDoAfter);
        SubscribeLocalEvent<BloodPackComponent, BeforeRangedInteractEvent>(OnBloodPackBeforeInteract);
    }

    private void OnSynthMapInit(Entity<SynthComponent> ent, ref MapInitEvent args)
    {
        if (!HasComp<BloodstreamComponent>(ent))
            return;

        // RMC strips IV targeting, so blood lost to a severed limb was gone for good. packs only move
        // whitelisted reagents, and the blood type check below keeps that to synth blood
        EnsureComp<IVDripTargetComponent>(ent);
        EnsureComp<CMUSynthCirculationComponent>(ent);
    }

    private void OnRepairToolUseAttempt(Entity<CMUSynthCirculationComponent> ent, ref RMCSynthRepairToolUseAttemptEvent args)
    {
        if (args.Handled || !TryComp<SynthComponent>(ent, out var synth))
            return;

        var used = args.Used;
        if (!HasComp<BlowtorchComponent>(used) || !_tool.HasQuality(used, synth.RepairQuality))
            return;

        // plating first, RMC's own brute repair handles that. limb reattach surgery also wants the welder
        if (_synth.HasDamage(ent, synth.WelderDamageGroup) || HasComp<CMUSurgeryArmedStepComponent>(ent))
            return;

        if (!HasRepairableOrgan(ent))
            return;

        args.Handled = true;

        if (!_repairable.UseFuel(used, args.User, ent.Comp.OrganRepairFuel, true))
            return;

        var self = args.User == ent.Owner;
        var delay = self
            ? ent.Comp.OrganSelfRepairTime
            : ent.Comp.OrganRepairTime * _skills.GetSkillDelayMultiplier(args.User, ConstructionSkill);

        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new CMUSynthOrganRepairDoAfterEvent(), ent, ent, used)
        {
            BreakOnMove = true,
            BreakOnDropItem = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(
            Loc.GetString("cmu-synth-organ-repair-start-self", ("target", ent.Owner), ("tool", used)),
            Loc.GetString("cmu-synth-organ-repair-start-others", ("user", args.User), ("target", ent.Owner), ("tool", used)),
            ent,
            args.User);
    }

    private void OnOrganRepairDoAfter(Entity<CMUSynthCirculationComponent> ent, ref CMUSynthOrganRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used)
            return;

        args.Handled = true;

        if (!_repairable.UseFuel(used, args.User, ent.Comp.OrganRepairFuel))
            return;

        foreach (var organ in _body.GetBodyOrganEntityComps<OrganHealthComponent>(ent.Owner))
        {
            if (IsRepairable(organ.Comp1))
                _organHealth.HealOrgan((organ.Owner, organ.Comp1), ent, ent.Comp.OrganRepairAmount);
        }

        _popup.PopupEntity(
            Loc.GetString("cmu-synth-organ-repair-finish-self", ("target", ent.Owner), ("tool", used)),
            Loc.GetString("cmu-synth-organ-repair-finish-others", ("user", args.User), ("target", ent.Owner), ("tool", used)),
            ent,
            args.User);

        // keep going until it's all fixed, same feel as welding the plating back on
        if (HasRepairableOrgan(ent))
            args.Repeat = _repairable.UseFuel(used, args.User, ent.Comp.OrganRepairFuel, true);
    }

    private void OnBloodPackBeforeInteract(Entity<BloodPackComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        var targetReagents = _bloodstream.GetReferenceReagentPrototypes((target, bloodstream));
        var synthPack = ent.Comp.TransferableReagents.Contains(SynthBloodReagent);
        if (!HasComp<SynthComponent>(target) && !synthPack)
            return;

        // the IV code just dumps whatever the pack whitelists into the bloodstream, so human
        // blood into a synth (or synth blood into a human) would sit there as dead volume forever
        if (ent.Comp.TransferableReagents.Any(r => targetReagents.Any(t => t.Id == r)))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cmu-synth-blood-pack-incompatible", ("pack", ent.Owner), ("target", target)),
            target,
            args.User,
            PopupType.SmallCaution);
    }

    private bool HasRepairableOrgan(EntityUid synth)
    {
        foreach (var organ in _body.GetBodyOrganEntityComps<OrganHealthComponent>(synth))
        {
            if (IsRepairable(organ.Comp1))
                return true;
        }

        return false;
    }

    // a destroyed organ is a replacement job, not a weld
    private static bool IsRepairable(OrganHealthComponent health)
    {
        return health.Current > FixedPoint2.Zero && health.Current < health.Max;
    }
}
