using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Tools;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Dropship.DirectFire;

public sealed partial class MohawkChinGunSystem
{
    private const string AnchoringQuality = "Anchoring";
    private const string WeldingQuality = "Welding";
    private const string ScrewingQuality = "Screwing";
    [Dependency] private SharedDoAfterSystem _repairDoAfter = default!;
    [Dependency] private SharedStackSystem _repairStacks = default!;
    [Dependency] private SharedToolSystem _repairTools = default!;
    [Dependency] private RMCRepairableSystem _repairs = default!;
    [Dependency] private RMCWeldEffectSystem _weldEffects = default!;

    private void InitializeDamage()
    {
        SubscribeLocalEvent<MohawkChinGunComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<MohawkChinGunComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MohawkChinGunComponent, ExaminedEvent>(OnDamageExamined);
        SubscribeLocalEvent<MohawkChinGunComponent, InteractUsingEvent>(OnRepairInteract);
        SubscribeLocalEvent<MohawkChinGunComponent, MohawkChinGunRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<MohawkChinGunComponent, MohawkChinGunSetupDoAfterEvent>(OnSetupFinished);
    }

    private void OnDamageModify(Entity<MohawkChinGunComponent> point, ref DamageModifyEvent args)
    {
        if (args.Tool is { } tool && HasComp<MeleeWeaponComponent>(tool) &&
            TryComp(tool, out XenoComponent? xeno) && xeno.Tier < 2)
            args.Damage = new DamageSpecifier();
    }

    private void OnDamageChanged(Entity<MohawkChinGunComponent> point, ref DamageChangedEvent args)
    {
        if (_net.IsClient || point.Comp.Disabled ||
            _damage.GetTotalDamage((point, args.Damageable)) < point.Comp.MaxHealth)
            return;
        point.Comp.Disabled = true;
        point.Comp.RepairStage = MohawkChinGunRepairStage.Strip;
        if (point.Comp.Operator is { } actor)
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-disabled"), point, actor);
        ReleaseControl(point);
        point.Comp.ApprovedAutomaticTarget = null;
        point.Comp.AutomaticTarget = null;
        point.Comp.DefensiveFire = false;
        _appearance.SetData(point, MohawkChinGunVisuals.Disabled, true);
        Dirty(point);
        SyncFreeFireDisplay(point);
    }

    private void OnDamageExamined(Entity<MohawkChinGunComponent> point, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        var remaining = TryComp(point, out DamageableComponent? damage)
            ? Math.Max(0, point.Comp.MaxHealth - _damage.GetTotalDamage((point, damage)).Float())
            : point.Comp.MaxHealth;
        args.PushMarkup(Loc.GetString("cmu-mohawk-gunnery-health",
            ("health", (int) Math.Ceiling(remaining)), ("max", (int) point.Comp.MaxHealth)));
        if (point.Comp.Disabled)
            args.PushMarkup(RepairInstruction(point.Comp.RepairStage));
        else if (!point.Comp.Calibrated || !point.Comp.IffConfigured)
            args.PushMarkup(Loc.GetString(point.Comp.Calibrated ? "cmu-mohawk-setup-iff" : "cmu-mohawk-setup-screw"));
    }

    public static TimeSpan RepairDelay(MohawkChinGunRepairStage stage)
    {
        return TimeSpan.FromSeconds(stage switch
        {
            MohawkChinGunRepairStage.Parts => 120,
            MohawkChinGunRepairStage.Weld => 30,
            MohawkChinGunRepairStage.Strip or MohawkChinGunRepairStage.Cable or MohawkChinGunRepairStage.Calibrate => 20,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
        });
    }

    private string RepairInstruction(MohawkChinGunRepairStage stage)
    {
        return Loc.GetString(stage switch
        {
            MohawkChinGunRepairStage.Strip => "cmu-mohawk-repair-strip",
            MohawkChinGunRepairStage.Parts => "cmu-mohawk-repair-parts",
            MohawkChinGunRepairStage.Cable => "cmu-mohawk-repair-cable",
            MohawkChinGunRepairStage.Weld => "cmu-mohawk-repair-weld",
            _ => "cmu-mohawk-repair-calibrate",
        });
    }

    private bool HasRepairMaterial(EntityUid used, MohawkChinGunRepairStage stage)
    {
        return stage switch
        {
            MohawkChinGunRepairStage.Strip => _repairTools.HasQuality(used, AnchoringQuality),
            MohawkChinGunRepairStage.Parts => HasComp<MohawkChinGunRepairPartsComponent>(used),
            MohawkChinGunRepairStage.Cable => TryComp(used, out StackComponent? stack) &&
                stack.StackTypeId == "RMCCable" && _repairStacks.GetCount(used) >= 10,
            MohawkChinGunRepairStage.Weld => HasComp<BlowtorchComponent>(used) &&
                _repairTools.HasQuality(used, WeldingQuality),
            MohawkChinGunRepairStage.Calibrate => _repairTools.HasQuality(used, ScrewingQuality),
            _ => false,
        };
    }

    private bool CanRepair(Entity<MohawkChinGunComponent> point, EntityUid actor, EntityUid used,
        MohawkChinGunRepairStage stage, bool starting = false)
    {
        if (!IsGrounded(point))
        {
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-repair-grounded"), point, actor);
            return false;
        }
        if (!_skills.HasSkill(actor, "RMCSkillEngineer", 1))
        {
            _popup.PopupEntity(Loc.GetString("rmc-repairable-not-trained", ("target", point)), point, actor);
            return false;
        }
        if (!HasRepairMaterial(used, stage))
        {
            _popup.PopupEntity(RepairInstruction(stage), point, actor);
            return false;
        }
        return stage != MohawkChinGunRepairStage.Weld || !starting ||
            _repairs.UseFuel(used, actor, FixedPoint2.New(5), attempt: true);
    }

    private void OnRepairInteract(Entity<MohawkChinGunComponent> point, ref InteractUsingEvent args)
    {
        if (!point.Comp.Disabled && !args.Handled &&
            (!point.Comp.Calibrated && _repairTools.HasQuality(args.Used, ScrewingQuality) ||
             HasComp<MultitoolComponent>(args.Used) &&
             (!TryComp(point, out DropshipHardpointFaultComponent? fault) || !fault.Faulted)))
        {
            args.Handled = true;
            if (_net.IsClient)
                return;
            if (!IsGrounded(point))
            {
                _popup.PopupEntity(Loc.GetString("cmu-mohawk-repair-grounded"), point, args.User);
                return;
            }
            if (!point.Comp.Calibrated && HasComp<MultitoolComponent>(args.Used))
            {
                _popup.PopupEntity(Loc.GetString("cmu-mohawk-setup-screw"), point, args.User);
                return;
            }
            _repairDoAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3),
                new MohawkChinGunSetupDoAfterEvent(point.Comp.Calibrated), point, point, args.Used)
            {
                NeedHand = true, BreakOnMove = true, BreakOnDamage = true, BlockDuplicate = true,
            });
            return;
        }
        if (args.Handled || !point.Comp.Disabled)
            return;
        args.Handled = true;
        if (_net.IsClient || !CanRepair(point, args.User, args.Used, point.Comp.RepairStage, starting: true))
            return;
        var delay = RepairDelay(point.Comp.RepairStage);
        var ev = new MohawkChinGunRepairDoAfterEvent(point.Comp.RepairStage);
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, ev, point, point, args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };
        if (_repairDoAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupEntity(RepairInstruction(point.Comp.RepairStage), point, args.User);
            if (point.Comp.RepairStage == MohawkChinGunRepairStage.Weld)
                _weldEffects.SpawnWeldEffect(point, delay);
        }
    }

    private void OnSetupFinished(Entity<MohawkChinGunComponent> point, ref MohawkChinGunSetupDoAfterEvent args)
    {
        if (_net.IsClient || args.Cancelled || args.Handled || args.Used is not { } used ||
            point.Comp.Disabled || !IsGrounded(point))
            return;
        args.Handled = true;
        if (!args.ConfigureIff && !point.Comp.Calibrated && _repairTools.HasQuality(used, ScrewingQuality))
        {
            point.Comp.Calibrated = true;
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-setup-iff"), point, args.User);
        }
        else if (args.ConfigureIff && point.Comp.Calibrated && HasComp<MultitoolComponent>(used))
        {
            _targeting.ApplyDeployerFactions(point, args.User);
            var targeting = Comp<SentryTargetingComponent>(point);
            point.Comp.IffConfigured = targeting.FriendlyFactions.Count > 0;
            if (point.Comp.IffConfigured)
            {
                var assigned = new SentryFactionAssignedEvent(args.User);
                RaiseLocalEvent(point.Owner, ref assigned);
            }
            point.Comp.AutomaticTarget = null;
            point.Comp.ApprovedAutomaticTarget = null;
            if (point.Comp.Operator != null)
                ReleaseControl(point);
            _popup.PopupEntity(Loc.GetString(point.Comp.IffConfigured
                ? "cmu-mohawk-setup-ready" : "cmu-mohawk-setup-no-faction"), point, args.User);
        }
        Dirty(point);
    }

    private void OnRepairDoAfter(Entity<MohawkChinGunComponent> point, ref MohawkChinGunRepairDoAfterEvent args)
    {
        if (_net.IsClient || args.Cancelled || args.Handled || !point.Comp.Disabled ||
            args.Stage != point.Comp.RepairStage || args.Used is not { } used || Deleted(used))
            return;
        args.Handled = true;
        if (!CanRepair(point, args.User, used, args.Stage))
            return;
        switch (args.Stage)
        {
            case MohawkChinGunRepairStage.Parts:
                QueueDel(used);
                break;
            case MohawkChinGunRepairStage.Cable:
                if (!_repairStacks.TryUse(used, 10))
                {
                    _popup.PopupEntity(RepairInstruction(args.Stage), point, args.User);
                    return;
                }
                break;
            case MohawkChinGunRepairStage.Weld:
                if (!_repairs.UseFuel(used, args.User, FixedPoint2.New(5)))
                    return;
                break;
            case MohawkChinGunRepairStage.Calibrate:
                _damage.SetAllDamage(point.Owner, FixedPoint2.Zero);
                point.Comp.Disabled = false;
                point.Comp.Calibrated = true;
                point.Comp.RepairStage = MohawkChinGunRepairStage.Strip;
                _appearance.SetData(point, MohawkChinGunVisuals.Disabled, false);
                Dirty(point);
                SyncFreeFireDisplay(point);
                _popup.PopupEntity(Loc.GetString("cmu-mohawk-repair-complete"), point, args.User);
                return;
        }
        point.Comp.RepairStage++;
        Dirty(point);
        _popup.PopupEntity(RepairInstruction(point.Comp.RepairStage), point, args.User);
    }

    /// <summary>Admin restore: full integrity, cool barrels and calibrated. IFF is kept if any factions were ever programmed.</summary>
    public void AdminRestore(Entity<MohawkChinGunComponent> point)
    {
        if (_net.IsClient)
            return;
        _damage.SetAllDamage(point.Owner, FixedPoint2.Zero);
        point.Comp.Disabled = false;
        point.Comp.Calibrated = true;
        point.Comp.RepairStage = MohawkChinGunRepairStage.Strip;
        point.Comp.Heat = 0;
        point.Comp.Overheated = false;
        if (TryComp(point, out SentryTargetingComponent? targeting) && targeting.FriendlyFactions.Count > 0)
            point.Comp.IffConfigured = true;
        _appearance.SetData(point, MohawkChinGunVisuals.Disabled, false);
        Dirty(point);
        SyncFreeFireDisplay(point);
    }
}

[RegisterComponent]
public sealed partial class MohawkChinGunRepairPartsComponent : Component;

[Serializable, NetSerializable]
public enum MohawkChinGunRepairStage : byte
{
    Strip,
    Parts,
    Cable,
    Weld,
    Calibrate,
}

[Serializable, NetSerializable]
public enum MohawkChinGunVisuals : byte
{
    Disabled,
}

[Serializable, NetSerializable]
public sealed partial class MohawkChinGunRepairDoAfterEvent : DoAfterEvent
{
    [DataField]
    public MohawkChinGunRepairStage Stage;
    public MohawkChinGunRepairDoAfterEvent()
    {
    }
    public MohawkChinGunRepairDoAfterEvent(MohawkChinGunRepairStage stage)
    {
        Stage = stage;
    }
    public override DoAfterEvent Clone() => new MohawkChinGunRepairDoAfterEvent(Stage);
}

[Serializable, NetSerializable]
public sealed partial class MohawkChinGunSetupDoAfterEvent : SimpleDoAfterEvent
{
    [DataField] public bool ConfigureIff;
    public MohawkChinGunSetupDoAfterEvent() { }
    public MohawkChinGunSetupDoAfterEvent(bool configureIff) => ConfigureIff = configureIff;
}
