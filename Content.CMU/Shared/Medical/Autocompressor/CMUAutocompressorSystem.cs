// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.CPR;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Database;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Autocompressor;

public sealed partial class CMUAutocompressorSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedBoneSystem _bones = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private CPRSystem _cpr = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<EntityUid> _stopped = new();

    public const string PatientSlot = "outerClothing";
    private static readonly EntProtoId<SkillDefinitionComponent> MedicalSkill = "RMCSkillMedical";

    public override void Initialize()
    {
        base.Initialize();
        InitializeTraining();
        SubscribeLocalEvent<CMUAutocompressorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUAutocompressorComponent, CMUAutocompressorFitDoAfterEvent>(OnFitDoAfter);
        SubscribeLocalEvent<CMUAutocompressorComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<CMUAutocompressorComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<CMUAutocompressorComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<CMUAutocompressorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUAutocompressorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUAutocompressorComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<CMUAutocompressorComponent, PowerCellChangedEvent>(OnCellChanged);
        SubscribeLocalEvent<CMUAutocompressorComponent, BatteryStateChangedEvent>(OnBatteryStateChanged);
        SubscribeLocalEvent<CMUAutocompressorComponent, GetVerbsEvent<InteractionVerb>>(OnVerbs);
        SubscribeLocalEvent<CMUAutocompressorPatientComponent, MobStateChangedEvent>(OnPatientStateChanged,
            after: new[] { typeof(RMCUnrevivableSystem) });
        SubscribeLocalEvent<CMUAutocompressorPatientComponent, ReceiveCPRAttemptEvent>(OnManualCPR);
        SubscribeLocalEvent<CMUAutocompressorPatientComponent, ComponentShutdown>(OnPatientShutdown);
        SubscribeLocalEvent<InternalBleedingChangedEvent>(OnInternalBleedingChanged);
    }

    private void OnAfterInteract(Entity<CMUAutocompressorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;
        args.Handled = true;
        if (!CanFit(ent, args.User, target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), args.User, args.User);
            return;
        }
        var delay = ent.Comp.AttachDelay * _skills.GetSkillDelayMultiplier(args.User, MedicalSkill);
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new CMUAutocompressorFitDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            TargetEffect = "RMCEffectHealBusy",
        };
        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-autocompressor-fitting", ("target", target)), args.User, args.User);
    }

    private void OnFitDoAfter(Entity<CMUAutocompressorComponent> ent, ref CMUAutocompressorFitDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;
        args.Handled = true;
        if (!_net.IsClient)
            TryFit(ent, args.User, target);
    }

    private bool CanWear(Entity<CMUAutocompressorComponent> ent, EntityUid user, EntityUid target, out LocId reason)
    {
        reason = "cmu-autocompressor-cannot-fit";
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(user) || TerminatingOrDeleted(target)
            || ent.Comp.Running || !_blocker.CanInteract(user, ent)
            || !_interaction.InRangeUnobstructed(user, target)
            || !_hands.TryGetActiveItem(user, out var held) || held != ent.Owner)
            return false;
        if (!HasComp<CPRDummyComponent>(target) && !_skills.HasSkill(user, MedicalSkill, ent.Comp.MedicalSkill))
        {
            reason = "cmu-autocompressor-no-skill";
            return false;
        }
        if (HasComp<CPRDummyComponent>(target))
            return CanAttachDummy(target, out reason);
        if (!_cpr.CMUCanWearMechanicalCPR(target))
        {
            reason = "cmu-autocompressor-invalid-patient";
            return false;
        }
        if (!_inventory.TryGetSlotContainer(target, PatientSlot, out var slot, out _) || slot.ContainedEntity != null)
        {
            reason = "cmu-autocompressor-remove-armor";
            return false;
        }
        if (!HasOperatingCharge(ent))
        {
            reason = "cmu-autocompressor-no-power";
            return false;
        }
        return true;
    }

    private bool CanFit(Entity<CMUAutocompressorComponent> ent, EntityUid user, EntityUid target, out LocId reason)
    {
        if (!CanWear(ent, user, target, out reason))
            return false;
        if (user != target && _cpr.CMUCanReceiveMechanicalCPR(target))
            return true;
        reason = "cmu-autocompressor-invalid-patient";
        return false;
    }

    public bool TryFit(Entity<CMUAutocompressorComponent> ent, EntityUid user, EntityUid target)
    {
        if (_net.IsClient)
            return false;
        if (!CanFit(ent, user, target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), ent, user);
            return false;
        }
        if (HasComp<CPRDummyComponent>(target))
        {
            if (!TryAttachDummy(ent, target))
                return false;
        }
        else if (!_inventory.TryEquip(user, target, ent, PatientSlot, silent: true))
            return false;
        _adminLog.Add(LogType.Healed, LogImpact.Low,
            $"{ToPrettyString(user):player} fitted {ToPrettyString(ent.Owner):entity} on {ToPrettyString(target):entity}");
        return true;
    }

    private void OnEquipAttempt(Entity<CMUAutocompressorComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Slot != PatientSlot)
            return;
        if (CanWear(ent, args.User, args.EquipTarget, out var reason))
            return;
        args.Reason = reason;
        args.Cancel();
    }

    private void OnEquipped(Entity<CMUAutocompressorComponent> ent, ref GotEquippedEvent args)
    {
        if (_net.IsClient || args.Slot != PatientSlot)
            return;
        AttachPatient(ent, args.EquipTarget);
    }

    private void AttachPatient(Entity<CMUAutocompressorComponent> ent, EntityUid target)
    {
        ent.Comp.Patient = target;
        ent.Comp.Training = HasComp<CPRDummyComponent>(target);
        Dirty(ent);
        var patient = EnsureComp<CMUAutocompressorPatientComponent>(target);
        patient.Device = ent;
        patient.Running = false;
        Dirty(target, patient);
        if (!TryStartTreatment(ent))
            UpdateAppearance(ent);
    }

    private bool TryStartTreatment(Entity<CMUAutocompressorComponent> ent)
    {
        if (_net.IsClient || ent.Comp.Running || ent.Comp.Patient is not { } patient
            || !_cpr.CMUCanReceiveMechanicalCPR(patient) || !HasOperatingCharge(ent)
            || !IsAttached(ent, patient))
            return false;
        ent.Comp.Running = true;
        ent.Comp.NextCompression = _timing.CurTime + ent.Comp.CompressionInterval;
        ent.Comp.CompressionStartedAt = _timing.CurTime;
        ent.Comp.FractureStage = FractureSeverity.None;
        ent.Comp.LungDamageApplied = false;
        EnsureComp<CMUAutocompressorRunningComponent>(ent);
        if (TryComp<CMUAutocompressorPatientComponent>(patient, out var attached))
        {
            attached.Running = true;
            Dirty(patient, attached);
        }
        Dirty(ent);
        UpdateAppearance(ent);
        _popup.PopupEntity(Loc.GetString("cmu-autocompressor-started", ("device", ent.Owner), ("target", patient)), patient, PopupType.Small);
        return true;
    }

    private void OnUnequipped(Entity<CMUAutocompressorComponent> ent, ref GotUnequippedEvent args)
    {
        if (!_net.IsClient && args.Slot == PatientSlot)
            Stop(ent, false);
    }

    private void OnMapInit(Entity<CMUAutocompressorComponent> ent, ref MapInitEvent args)
    {
        if (!_net.IsClient)
            UpdateAppearance(ent);
    }

    private void OnShutdown(Entity<CMUAutocompressorComponent> ent, ref ComponentShutdown args)
    {
        if (!_net.IsClient)
        {
            ClearPatient(ent);
            _stopped.Add(ent.Owner);
        }
    }

    private void OnPatientShutdown(Entity<CMUAutocompressorPatientComponent> ent, ref ComponentShutdown args)
    {
        if (!_net.IsClient && ent.Comp.Device is { } device && TryComp<CMUAutocompressorComponent>(device, out var compressor)
            && compressor.Patient == ent.Owner)
            Stop((device, compressor), false);
    }

    private void OnPatientStateChanged(Entity<CMUAutocompressorPatientComponent> ent, ref MobStateChangedEvent args)
    {
        if (_net.IsClient || ent.Comp.Device is not { } device || !TryComp<CMUAutocompressorComponent>(device, out var compressor)
            || compressor.Patient != ent.Owner)
            return;
        if (_cpr.CMUCanReceiveMechanicalCPR(ent))
            TryStartTreatment((device, compressor));
        else if (compressor.Running)
            Stop((device, compressor), true);
    }

    private void OnManualCPR(Entity<CMUAutocompressorPatientComponent> ent, ref ReceiveCPRAttemptEvent args)
    {
        if (ent.Comp.Device is not { } device || !TryComp<CMUAutocompressorComponent>(device, out var compressor)
            || !compressor.Running || compressor.Patient != ent.Owner)
            return;
        args.Cancelled = true;
        if (!_net.IsClient && args.Start)
            _popup.PopupEntity(Loc.GetString("cmu-autocompressor-already-running"), ent, args.Performer);
    }

    private bool HasOperatingCharge(Entity<CMUAutocompressorComponent> ent)
        => float.IsFinite(ent.Comp.ChargePerCompression) && ent.Comp.ChargePerCompression > 0
            && float.IsFinite(ent.Comp.MinimumCharge) && ent.Comp.CompressionInterval > TimeSpan.Zero
            && _powerCell.HasCharge(ent.Owner, Math.Max(ent.Comp.ChargePerCompression, ent.Comp.MinimumCharge));

    private void OnChargeChanged(Entity<CMUAutocompressorComponent> ent, ref ChargeChangedEvent args)
        => OnPowerChanged(ent);

    private void OnCellChanged(Entity<CMUAutocompressorComponent> ent, ref PowerCellChangedEvent args)
        => OnPowerChanged(ent);

    private void OnBatteryStateChanged(Entity<CMUAutocompressorComponent> ent, ref BatteryStateChangedEvent args)
        => OnPowerChanged(ent);

    private void OnPowerChanged(Entity<CMUAutocompressorComponent> ent)
    {
        if (_net.IsClient)
            return;
        if (ent.Comp.Running && !HasOperatingCharge(ent))
            Stop(ent, true, "cmu-autocompressor-battery-low");
        else
            TryStartTreatment(ent);
        UpdateAppearance(ent);
    }

    private void ClearPatient(Entity<CMUAutocompressorComponent> ent)
    {
        if (ent.Comp.Patient is { } patient && TryComp<CMUAutocompressorPatientComponent>(patient, out var attached)
            && attached.Device == ent.Owner)
        {
            attached.Device = null;
            attached.Running = false;
            Dirty(patient, attached);
        }
        ent.Comp.Patient = null;
        ent.Comp.Training = false;
    }

    public void Stop(Entity<CMUAutocompressorComponent> ent, bool detach, LocId? message = null)
    {
        if (_net.IsClient || !ent.Comp.Running && ent.Comp.Patient == null)
            return;
        var patient = ent.Comp.Patient;
        var wasRunning = ent.Comp.Running;
        ent.Comp.Running = false;
        ClearPatient(ent);
        _stopped.Add(ent.Owner);
        Dirty(ent);
        UpdateAppearance(ent);
        if (patient is not { } target || TerminatingOrDeleted(target))
            return;
        if (detach)
            DetachPatient(ent, target);
        if (wasRunning)
            _popup.PopupEntity(Loc.GetString(message ?? "cmu-autocompressor-stopped", ("device", ent.Owner), ("target", target)), target, PopupType.Small);
    }

    private void UpdateAppearance(Entity<CMUAutocompressorComponent> ent)
    {
        _clothing.SetEquippedPrefix(ent.Owner, ent.Comp.Running ? "running" : null);
        _appearance.SetData(ent.Owner, CMUAutocompressorVisuals.Running, ent.Comp.Running);
        var level = CMUAutocompressorBattery.Empty;
        if (_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
        {
            var ratio = _battery.GetChargeLevel(battery.Value.AsNullable());
            level = ratio > 0.66f ? CMUAutocompressorBattery.High
                : ratio > 0.33f ? CMUAutocompressorBattery.Medium
                : ratio > 0 ? CMUAutocompressorBattery.Low : CMUAutocompressorBattery.Empty;
        }
        _appearance.SetData(ent.Owner, CMUAutocompressorVisuals.Battery, level);
    }

    private void OnVerbs(Entity<CMUAutocompressorComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Patient == null)
            return;
        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-autocompressor-remove"),
            Act = () =>
            {
                if (_blocker.CanInteract(user, ent) && _interaction.InRangeUnobstructed(user, ent.Owner))
                    Stop(ent, true);
            },
        });
    }

    private FractureSeverity GetFractureStage(CMUAutocompressorComponent compressor, TimeSpan now)
    {
        if (compressor.Training)
            return FractureSeverity.None;
        var elapsed = now - compressor.CompressionStartedAt;
        if (elapsed > compressor.ShatteredFractureDelay)
            return FractureSeverity.Shattered;
        if (elapsed > compressor.CompoundFractureDelay)
            return FractureSeverity.Compound;
        return elapsed > compressor.FractureDelay ? FractureSeverity.Simple : FractureSeverity.None;
    }

    private void FractureRibs(EntityUid patient, FractureSeverity severity)
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled) || !_cfg.GetCVar(CMUMedicalCCVars.BoneEnabled))
            return;

        foreach (var (part, anatomy) in _medicalIndex.GetBodyParts(patient))
        {
            if (anatomy.PartType != BodyPartType.Torso)
                continue;
            if (TryComp<FractureComponent>(part, out var fracture) && fracture.Severity >= severity)
                continue;
            if (_bones.SeedFracture(part, severity))
                _popup.PopupEntity(Loc.GetString(severity switch
                {
                    FractureSeverity.Compound => "cmu-autocompressor-ribs-compound",
                    FractureSeverity.Shattered => "cmu-autocompressor-ribs-shattered",
                    _ => "cmu-autocompressor-ribs-fractured",
                }, ("target", patient)), patient, PopupType.MediumCaution);
        }
    }

    private void OnInternalBleedingChanged(ref InternalBleedingChangedEvent args)
    {
        if (_net.IsClient || args.Removed
            || !TryComp<CMUAutocompressorPatientComponent>(args.Body, out var patient)
            || patient.Device is not { } device
            || !TryComp<CMUAutocompressorComponent>(device, out var compressor))
            return;
        TryDamageLung((device, compressor), args.Body, args.Part);
    }

    private void TryDamageLung(Entity<CMUAutocompressorComponent> ent, EntityUid patient, EntityUid part)
    {
        if (!ent.Comp.Running || ent.Comp.Patient != patient || ent.Comp.LungDamageApplied
            || ent.Comp.FractureStage != FractureSeverity.Shattered || ent.Comp.LungDamage <= FixedPoint2.Zero
            || !_cfg.GetCVar(CMUMedicalCCVars.Enabled) || !_cfg.GetCVar(CMUMedicalCCVars.BoneEnabled)
            || !_cfg.GetCVar(CMUMedicalCCVars.OrganEnabled)
            || !TryComp<BodyPartComponent>(part, out var anatomy) || anatomy.Body != patient
            || anatomy.PartType != BodyPartType.Torso
            || !TryComp<FractureComponent>(part, out var fracture) || fracture.Severity != FractureSeverity.Shattered
            || !TryComp<InternalBleedingComponent>(part, out var bleeding) || bleeding.BloodlossPerSecond <= 0)
            return;

        foreach (var (organ, _) in _medicalIndex.GetPartOrgans(part))
        {
            if (!HasComp<LungsComponent>(organ) || !TryComp<OrganHealthComponent>(organ, out var health)
                || health.Current <= FixedPoint2.Zero)
                continue;
            ent.Comp.LungDamageApplied = true;
            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = ent.Comp.LungDamage } };
            var ev = new OrganDamagedEvent(patient, organ, damage, OrganDamageSource.RibFracture);
            RaiseLocalEvent(organ, ref ev, broadcast: true);
            return;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUAutocompressorRunningComponent, CMUAutocompressorComponent>();
        while (query.MoveNext(out var uid, out _, out var compressor))
        {
            if (!compressor.Running || MetaData(uid).EntityPaused)
                continue;
            var fractureStage = compressor.FractureStage == FractureSeverity.Shattered
                ? FractureSeverity.Shattered : GetFractureStage(compressor, now);
            var fractureDue = fractureStage > compressor.FractureStage;
            if (now < compressor.NextCompression && !fractureDue)
                continue;
            if (compressor.Patient is not { } patient || TerminatingOrDeleted(patient)
                || !IsAttached((uid, compressor), patient)
                || !_cpr.CMUCanReceiveMechanicalCPR(patient))
            {
                Stop((uid, compressor), true);
                continue;
            }
            if (!HasOperatingCharge((uid, compressor)))
            {
                Stop((uid, compressor), true, "cmu-autocompressor-battery-low");
                continue;
            }
            if (fractureDue)
            {
                compressor.FractureStage = fractureStage;
                FractureRibs(patient, fractureStage);
                if (!compressor.Running || TerminatingOrDeleted(uid) || TerminatingOrDeleted(patient))
                    continue;
                if (fractureStage == FractureSeverity.Shattered && !compressor.LungDamageApplied)
                {
                    foreach (var (part, anatomy) in _medicalIndex.GetBodyParts(patient))
                    {
                        if (anatomy.PartType == BodyPartType.Torso)
                            TryDamageLung((uid, compressor), patient, part);
                    }
                    if (!compressor.Running || TerminatingOrDeleted(uid) || TerminatingOrDeleted(patient))
                        continue;
                }
            }
            if (now < compressor.NextCompression)
                continue;
            compressor.NextCompression = now + compressor.CompressionInterval;
            _cpr.CMUTryMechanicalCPR(patient, uid, compressor.ChargePerCompression);
        }
        foreach (var uid in _stopped)
        {
            if (!TerminatingOrDeleted(uid) && (!TryComp<CMUAutocompressorComponent>(uid, out var compressor) || !compressor.Running))
                RemComp<CMUAutocompressorRunningComponent>(uid);
        }
        _stopped.Clear();
    }
}
