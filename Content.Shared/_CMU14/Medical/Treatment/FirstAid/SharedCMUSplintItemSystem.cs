using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Treatment.FirstAid;

public abstract partial class SharedCMUSplintItemSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected SharedFractureSystem Fracture = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected CMUMedicalSchedulerSystem MedicalScheduler = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected IRobustRandom Random = default!;

    private const float CastRemovePromptSeconds = 30f;
    private const float CastRemoveDoAfterSeconds = 1f;
    private static readonly CMUMedicalWorkKey CastHealWork = new("cast-heal-complete");
    private static readonly CMUMedicalWorkKey CastRemovePromptWork = new("cast-remove-prompt");
    private static readonly CMUMedicalWorkKey PostOpMalunionWork = new("post-op-malunion-check");

    private bool _medicalEnabled;
    private bool _boneEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUSplintItemComponent, AfterInteractEvent>(OnSplintInteract);
        SubscribeLocalEvent<CMUSplintItemComponent, ExaminedEvent>(OnSplintExamined);
        SubscribeLocalEvent<CMUSplintItemComponent, CMUSplintApplyDoAfterEvent>(OnSplintDoAfter);
        SubscribeLocalEvent<CMUSplintedComponent, BodyPartDamagedEvent>(OnSplintedPartDamaged);
        SubscribeLocalEvent<CMUCastItemComponent, AfterInteractEvent>(OnCastInteract);
        SubscribeLocalEvent<CMUCastItemComponent, CMUCastApplyDoAfterEvent>(OnCastDoAfter);
        SubscribeLocalEvent<CMUCastComponent, BoneFracturedEvent>(OnCastPartFractured);
        SubscribeLocalEvent<CMUCastComponent, CMUMedicalWorkDueEvent>(OnCastWorkDue);
        SubscribeLocalEvent<CMUPostOpBoneSetComponent, CMUMedicalWorkDueEvent>(OnPostOpWorkDue);
        SubscribeLocalEvent<CMUHumanMedicalComponent, CMUCastVerbRemoveDoAfterEvent>(OnCastVerbRemoveDoAfter);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneEnabled, v => _boneEnabled = v, true);
    }

    private void OnSplintExamined(Entity<CMUSplintItemComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !ent.Comp.ConsumedOnApply)
            return;

        args.PushMarkup(Loc.GetString(
            "cmu-splint-item-uses-remaining",
            ("uses", Math.Max(0, ent.Comp.Uses))));
    }

    public bool IsLayerEnabled()
    {
        return _medicalEnabled && _boneEnabled;
    }

    private void OnSplintedPartDamaged(Entity<CMUSplintedComponent> ent, ref BodyPartDamagedEvent args)
    {
        if (Net.IsClient || !IsLayerEnabled())
            return;
        if (!ent.Comp.BreakOnDamage)
            return;
        if (args.Delta.GetTotal() <= ent.Comp.BreakDamageThreshold)
            return;

        RemCompDeferred<CMUSplintedComponent>(ent);
    }

    private void OnSplintInteract(Entity<CMUSplintItemComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (!HasComp<CMUHumanMedicalComponent>(target))
            return;
        // Resolve the part NOW while the medic's aim selection is still fresh —
        // the DoAfter is ApplyDelay long, and aim freshness
        // (`cmu.medical.aim_mode.freshness_seconds`) is short, so resolving at
        // DoAfter completion would usually miss the aim window.
        if (!TryFindFracturedPart(target, out var part, args.User))
            return;

        var ev = new CMUSplintApplyDoAfterEvent { PreSelectedPart = GetNetEntity(part) };
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.ApplyDelay,
            ev, ent.Owner, target: target, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };
        DoAfter.TryStartDoAfter(doAfter);
        args.Handled = true;
    }

    private void OnSplintDoAfter(Entity<CMUSplintItemComponent> ent, ref CMUSplintApplyDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;
        if (!IsLayerEnabled())
            return;

        // Use the part resolved at DoAfter start (aim was fresh). Re-resolve
        // only if the pre-selection is gone.
        EntityUid part;
        if (args.PreSelectedPart is { } netPart && TryGetEntity(netPart, out var stored)
            && HasComp<FractureComponent>(stored.Value))
        {
            part = stored.Value;
        }
        else if (!TryFindFracturedPart(target, out part, args.User))
        {
            return;
        }
        ApplySplintToPart(ent, part);
    }

    /// <summary>
    ///     Idempotent — applying twice on the same part refreshes
    ///     <see cref="CMUSplintedComponent.MaxSuppressed"/> to the highest of the
    ///     two values.
    /// </summary>
    public bool ApplySplintToPart(Entity<CMUSplintItemComponent> ent, EntityUid part)
    {
        if (!HasComp<BodyPartComponent>(part))
            return false;

        var splinted = EnsureComp<CMUSplintedComponent>(part);
        if ((byte)ent.Comp.MaxSuppressed > (byte)splinted.MaxSuppressed)
            splinted.MaxSuppressed = ent.Comp.MaxSuppressed;
        splinted.BreakOnDamage = ent.Comp.BreakOnDamage;
        splinted.BreakDamageThreshold = ent.Comp.BreakDamageThreshold;
        Dirty(part, splinted);
        var ev = new CMUSplintChangedEvent(part, false);
        RaiseLocalEvent(ref ev);

        if (ent.Comp.ApplySound is not null)
            Audio.PlayPredicted(ent.Comp.ApplySound, part, null);

        ConsumeSplintUse(ent);

        return true;
    }

    private void OnCastInteract(Entity<CMUCastItemComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (!HasComp<CMUHumanMedicalComponent>(target))
            return;
        if (!TryFindCastTargetPart(target, out var part, args.User))
            return;

        var ev = new CMUCastApplyDoAfterEvent { PreSelectedPart = GetNetEntity(part) };
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.ApplyDelay,
            ev, ent.Owner, target: target, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };
        DoAfter.TryStartDoAfter(doAfter);
        args.Handled = true;
    }

    private void OnCastDoAfter(Entity<CMUCastItemComponent> ent, ref CMUCastApplyDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;
        if (!IsLayerEnabled())
            return;

        EntityUid part;
        if (args.PreSelectedPart is { } netPart && TryGetEntity(netPart, out var stored)
            && IsCastTarget(stored.Value))
        {
            part = stored.Value;
        }
        else if (!TryFindCastTargetPart(target, out part, args.User))
        {
            return;
        }
        ApplyCastToPart(ent, part);
    }

    public bool ApplyCastToPart(Entity<CMUCastItemComponent> ent, EntityUid part)
    {
        var hasFracture = TryComp<FractureComponent>(part, out var frac);
        var hasPostOp = HasComp<CMUPostOpBoneSetComponent>(part);
        if (!hasFracture && !hasPostOp)
            return false;
        var minutes = ent.Comp.PostOpHealMinutes;
        if (hasFracture
            && !HasComp<CMUMalunionComponent>(part)
            && !ent.Comp.HealMinutesPerSeverity.TryGetValue(frac!.Severity, out minutes))
        {
            // Cast can't help this severity (Compound+ — surgery only).
            return false;
        }

        var cast = EnsureComp<CMUCastComponent>(part);
        cast.AppliedAt = Timing.CurTime;
        cast.HealCompletesAt = Timing.CurTime + TimeSpan.FromMinutes(minutes);
        cast.ReadyToRemove = false;
        cast.NextRemovePrompt = TimeSpan.Zero;
        if ((byte)ent.Comp.MaxSuppressed > (byte)cast.MaxSuppressed)
            cast.MaxSuppressed = ent.Comp.MaxSuppressed;
        Dirty(part, cast);
        MedicalScheduler.Schedule(part, CastHealWork, cast.HealCompletesAt);
        MedicalScheduler.Cancel(part, CastRemovePromptWork);
        MedicalScheduler.Cancel(part, PostOpMalunionWork);
        var ev = new CMUCastChangedEvent(part, false);
        RaiseLocalEvent(ref ev);
        if (HasComp<CMUSplintedComponent>(part))
            RemComp<CMUSplintedComponent>(part);

        if (ent.Comp.ApplySound is not null)
            Audio.PlayPredicted(ent.Comp.ApplySound, part, null);

        ConsumeCastUse(ent);

        return true;
    }

    private void ConsumeSplintUse(Entity<CMUSplintItemComponent> ent)
    {
        if (!ent.Comp.ConsumedOnApply || !Net.IsServer)
            return;

        ent.Comp.Uses--;
        if (ent.Comp.Uses <= 0)
            QueueDel(ent.Owner);
    }

    private void ConsumeCastUse(Entity<CMUCastItemComponent> ent)
    {
        if (!ent.Comp.ConsumedOnApply || !Net.IsServer)
            return;

        ent.Comp.Uses--;
        if (ent.Comp.Uses <= 0)
            QueueDel(ent.Owner);
    }

    public void AddCastRemoveVerb(Entity<CMUHumanMedicalComponent> patient, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsLayerEnabled())
            return;
        if (!args.CanInteract || !args.CanAccess)
            return;
        if (args.User != patient.Owner)
            return;
        if (!FindRemovableCast(patient.Owner, out var part))
            return;

        var user = args.User;
        var patientUid = patient.Owner;
        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("cmu-medical-cast-verb-remove"),
            Act = () => StartCastRemoveDoAfter(user, patientUid, part),
            Priority = 1,
        };
        args.Verbs.Add(verb);
    }

    private void StartCastRemoveDoAfter(EntityUid user, EntityUid patient, EntityUid part)
    {
        var removeEv = new CMUCastVerbRemoveDoAfterEvent { PreSelectedPart = GetNetEntity(part) };
        var removeDo = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(CastRemoveDoAfterSeconds),
            removeEv, patient, target: patient)
        {
            BlockDuplicate = true,
        };
        if (DoAfter.TryStartDoAfter(removeDo))
            Popup.PopupPredicted(Loc.GetString("cmu-medical-cast-removing"), patient, user);
    }

    private void OnCastVerbRemoveDoAfter(Entity<CMUHumanMedicalComponent> patient, ref CMUCastVerbRemoveDoAfterEvent args)
    {
        if (args.Cancelled)
            return;
        if (!IsLayerEnabled())
            return;
        if (!ResolvePart(patient.Owner, args.PreSelectedPart, out var part))
            return;
        if (!TryComp<CMUCastComponent>(part, out var cast) || !cast.ReadyToRemove)
            return;

        MedicalScheduler.Cancel(part, CastHealWork);
        MedicalScheduler.Cancel(part, CastRemovePromptWork);
        RemComp<CMUCastComponent>(part);
        var ev = new CMUCastChangedEvent(part, true);
        RaiseLocalEvent(ref ev);
        Popup.PopupPredicted(Loc.GetString("cmu-medical-cast-removed"), patient.Owner, args.User);
    }

    /// <summary>
    ///     Picks which fractured part to splint/cast.
    ///     Tier 1: medic's body-zone aim-picker selection (persistent — once
    ///     the medic has clicked any zone we honour it as their operating
    ///     intent, no freshness window like the shooting path uses).
    ///     Tier 2: first fractured part that isn't already splinted.
    ///     Tier 3: first fractured part — last-resort re-splint.
    /// </summary>
    public bool TryFindFracturedPart(EntityUid body, out EntityUid part, EntityUid? user = null)
    {
        part = default;

        // Tier 1: aim-picker (gated on "has the user ever clicked" so the
        // default Chest selection doesn't auto-splint chest on every fresh
        // marine).
        if (user is { } u
            && TryComp<BodyZoneTargetingComponent>(u, out var aim)
            && aim.LastSelectedAt > TimeSpan.Zero)
        {
            var (partType, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);
            foreach (var (id, partComp) in MedicalIndex.GetBodyParts(body))
            {
                if (partComp.PartType != partType)
                    continue;
                if (symmetry is { } s && partComp.Symmetry != s)
                    continue;
                if (HasComp<FractureComponent>(id))
                {
                    part = id;
                    return true;
                }
            }
        }

        foreach (var (id, _) in MedicalIndex.GetBodyParts(body))
        {
            if (HasComp<FractureComponent>(id) && !HasComp<CMUSplintedComponent>(id))
            {
                part = id;
                return true;
            }
        }

        foreach (var (id, _) in MedicalIndex.GetBodyParts(body))
        {
            if (HasComp<FractureComponent>(id))
            {
                part = id;
                return true;
            }
        }
        return false;
    }

    public bool TryFindCastTargetPart(EntityUid body, out EntityUid part, EntityUid? user = null)
    {
        part = default;

        if (user is { } u
            && TryComp<BodyZoneTargetingComponent>(u, out var aim)
            && aim.LastSelectedAt > TimeSpan.Zero)
        {
            var (partType, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);
            foreach (var (id, partComp) in MedicalIndex.GetBodyParts(body))
            {
                if (partComp.PartType != partType)
                    continue;
                if (symmetry is { } s && partComp.Symmetry != s)
                    continue;
                if (IsCastTarget(id))
                {
                    part = id;
                    return true;
                }
            }
        }

        foreach (var (id, _) in MedicalIndex.GetBodyParts(body))
        {
            if (IsCastTarget(id) && !HasComp<CMUCastComponent>(id))
            {
                part = id;
                return true;
            }
        }

        foreach (var (id, _) in MedicalIndex.GetBodyParts(body))
        {
            if (IsCastTarget(id))
            {
                part = id;
                return true;
            }
        }

        return false;
    }

    private bool IsCastTarget(EntityUid part)
    {
        return HasComp<BodyPartComponent>(part)
               && (HasComp<FractureComponent>(part) || HasComp<CMUPostOpBoneSetComponent>(part));
    }

    private bool FindRemovableCast(EntityUid body, out EntityUid part)
    {
        part = default;
        foreach (var (id, _) in MedicalIndex.GetBodyParts(body))
        {
            if (TryComp<CMUCastComponent>(id, out var cast) && cast.ReadyToRemove)
            {
                part = id;
                return true;
            }
        }

        return false;
    }

    private bool ResolvePart(EntityUid body, NetEntity? selected, out EntityUid part)
    {
        part = default;
        if (selected is { } netPart && TryGetEntity(netPart, out var stored) && HasComp<BodyPartComponent>(stored.Value))
        {
            part = stored.Value;
            return true;
        }

        return FindRemovableCast(body, out part);
    }

    private void OnCastPartFractured(Entity<CMUCastComponent> ent, ref BoneFracturedEvent args)
    {
        if (Net.IsClient || !IsLayerEnabled())
            return;
        if (!ent.Comp.ReadyToRemove)
            Popup.PopupEntity(Loc.GetString("cmu-medical-cast-broke"), args.Body, args.Body, PopupType.MediumCaution);

        MedicalScheduler.Cancel(ent.Owner, CastHealWork);
        MedicalScheduler.Cancel(ent.Owner, CastRemovePromptWork);
        MedicalScheduler.Cancel(ent.Owner, PostOpMalunionWork);
        RemComp<CMUCastComponent>(ent.Owner);
        if (HasComp<CMUPostOpBoneSetComponent>(ent.Owner))
            RemComp<CMUPostOpBoneSetComponent>(ent.Owner);
        var ev = new CMUCastChangedEvent(ent.Owner, true);
        RaiseLocalEvent(ref ev);
    }

    /// <summary>
    ///     Registers the one authoritative malunion deadline created by fracture surgery.
    /// </summary>
    public void SchedulePostOpMalunion(EntityUid part, CMUPostOpBoneSetComponent? postOp = null)
    {
        if (Net.IsClient || !Resolve(part, ref postOp, false))
            return;

        MedicalScheduler.Schedule(part, PostOpMalunionWork, postOp.MalunionCheckAt);
    }

    private void OnCastWorkDue(Entity<CMUCastComponent> ent, ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != CastHealWork && args.Key != CastRemovePromptWork)
            return;
        if (!IsLayerEnabled())
        {
            MedicalScheduler.Schedule(ent.Owner, args.Key, Timing.CurTime + TimeSpan.FromSeconds(1));
            return;
        }

        var now = Timing.CurTime;
        if (args.Key == CastHealWork)
        {
            if (ent.Comp.ReadyToRemove)
                return;
            if (ent.Comp.HealCompletesAt > now)
            {
                MedicalScheduler.Schedule(ent.Owner, CastHealWork, ent.Comp.HealCompletesAt);
                return;
            }

            if (TryComp<FractureComponent>(ent.Owner, out var frac))
                Fracture.SetSeverity((ent.Owner, frac), FractureSeverity.None, forceUpgrade: false);
            if (HasComp<CMUMalunionComponent>(ent.Owner))
                RemComp<CMUMalunionComponent>(ent.Owner);
            if (HasComp<CMUPostOpBoneSetComponent>(ent.Owner))
                RemComp<CMUPostOpBoneSetComponent>(ent.Owner);
            MedicalScheduler.Cancel(ent.Owner, PostOpMalunionWork);

            ent.Comp.ReadyToRemove = true;
            ent.Comp.NextRemovePrompt = now;
            Dirty(ent.Owner, ent.Comp);
            var ev = new CMUCastChangedEvent(ent.Owner, false);
            RaiseLocalEvent(ref ev);
            MedicalScheduler.Schedule(ent.Owner, CastRemovePromptWork, now);
            return;
        }

        if (args.Key != CastRemovePromptWork || !ent.Comp.ReadyToRemove)
            return;
        if (ent.Comp.NextRemovePrompt > now)
        {
            MedicalScheduler.Schedule(ent.Owner, CastRemovePromptWork, ent.Comp.NextRemovePrompt);
            return;
        }

        if (TryComp<BodyPartComponent>(ent.Owner, out var part) && part.Body is { } body)
            Popup.PopupEntity(Loc.GetString("cmu-medical-cast-ready-remove"), body, body, PopupType.Medium);
        ent.Comp.NextRemovePrompt = now + TimeSpan.FromSeconds(CastRemovePromptSeconds);
        Dirty(ent.Owner, ent.Comp);
        MedicalScheduler.Schedule(ent.Owner, CastRemovePromptWork, ent.Comp.NextRemovePrompt);
    }

    private void OnPostOpWorkDue(
        Entity<CMUPostOpBoneSetComponent> ent,
        ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != PostOpMalunionWork)
            return;
        if (!IsLayerEnabled())
        {
            MedicalScheduler.Schedule(ent.Owner, PostOpMalunionWork, Timing.CurTime + TimeSpan.FromSeconds(1));
            return;
        }

        var now = Timing.CurTime;
        if (ent.Comp.MalunionCheckAt > now)
        {
            MedicalScheduler.Schedule(ent.Owner, PostOpMalunionWork, ent.Comp.MalunionCheckAt);
            return;
        }

        if (!HasComp<CMUCastComponent>(ent.Owner) && Random.Prob(ent.Comp.MalunionChance))
        {
            var fracture = EnsureComp<FractureComponent>(ent.Owner);
            Fracture.SetSeverity((ent.Owner, fracture), FractureSeverity.Simple);
            var malunion = EnsureComp<CMUMalunionComponent>(ent.Owner);
            malunion.AppearedAt = now;
            Dirty(ent.Owner, malunion);

            if (TryComp<BodyPartComponent>(ent.Owner, out var part) && part.Body is { } body)
                Popup.PopupEntity(Loc.GetString("cmu-medical-cast-malunion"), body, body, PopupType.MediumCaution);
        }

        RemComp<CMUPostOpBoneSetComponent>(ent.Owner);
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUSplintApplyDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity? PreSelectedPart;
}

[Serializable, NetSerializable]
public sealed partial class CMUCastApplyDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity? PreSelectedPart;
}

[Serializable, NetSerializable]
public sealed partial class CMUCastVerbRemoveDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity? PreSelectedPart;
}
