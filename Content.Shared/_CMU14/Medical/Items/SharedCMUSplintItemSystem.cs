using System.Linq;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
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

namespace Content.Shared._CMU14.Medical.Items;

public abstract class SharedCMUSplintItemSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected readonly SharedFractureSystem Fracture = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;

    private const float CastScanInterval = 1f;
    private const float CastRemovePromptSeconds = 30f;
    private const float CastRemoveDoAfterSeconds = 1f;
    private float _castScanAccumulator;

    private bool _medicalEnabled;
    private bool _boneEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUSplintItemComponent, AfterInteractEvent>(OnSplintInteract);
        SubscribeLocalEvent<CMUSplintItemComponent, CMUSplintApplyDoAfterEvent>(OnSplintDoAfter);
        SubscribeLocalEvent<CMUSplintedComponent, BodyPartDamagedEvent>(OnSplintedPartDamaged);
        SubscribeLocalEvent<CMUCastItemComponent, AfterInteractEvent>(OnCastInteract);
        SubscribeLocalEvent<CMUCastItemComponent, CMUCastApplyDoAfterEvent>(OnCastDoAfter);
        SubscribeLocalEvent<CMUCastComponent, BoneFracturedEvent>(OnCastPartFractured);
        SubscribeLocalEvent<CMUHumanMedicalComponent, CMUCastVerbRemoveDoAfterEvent>(OnCastVerbRemoveDoAfter);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneEnabled, v => _boneEnabled = v, true);
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
        RaiseLocalEvent(new CMUSplintChangedEvent(part, false));

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
        RaiseLocalEvent(new CMUCastChangedEvent(part, false));
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

        RemComp<CMUCastComponent>(part);
        RaiseLocalEvent(new CMUCastChangedEvent(part, true));
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
            foreach (var (id, partComp) in Body.GetBodyChildren(body))
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

        foreach (var (id, _) in Body.GetBodyChildren(body))
        {
            if (HasComp<FractureComponent>(id) && !HasComp<CMUSplintedComponent>(id))
            {
                part = id;
                return true;
            }
        }

        foreach (var (id, _) in Body.GetBodyChildren(body))
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
            foreach (var (id, partComp) in Body.GetBodyChildren(body))
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

        foreach (var (id, _) in Body.GetBodyChildren(body))
        {
            if (IsCastTarget(id) && !HasComp<CMUCastComponent>(id))
            {
                part = id;
                return true;
            }
        }

        foreach (var (id, _) in Body.GetBodyChildren(body))
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
        foreach (var (id, _) in Body.GetBodyChildren(body))
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

        RemComp<CMUCastComponent>(ent.Owner);
        if (HasComp<CMUPostOpBoneSetComponent>(ent.Owner))
            RemComp<CMUPostOpBoneSetComponent>(ent.Owner);
        RaiseLocalEvent(new CMUCastChangedEvent(ent.Owner, true));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

        if (!IsLayerEnabled())
            return;

        _castScanAccumulator += frameTime;
        if (_castScanAccumulator < CastScanInterval)
            return;
        _castScanAccumulator = 0f;

        var now = Timing.CurTime;
        var castQuery = EntityQueryEnumerator<CMUCastComponent, BodyPartComponent>();
        while (castQuery.MoveNext(out var partUid, out var cast, out var part))
        {
            if (cast.ReadyToRemove)
            {
                if (cast.NextRemovePrompt <= now && part.Body is { } body)
                {
                    Popup.PopupEntity(Loc.GetString("cmu-medical-cast-ready-remove"), body, body, PopupType.Medium);
                    cast.NextRemovePrompt = now + TimeSpan.FromSeconds(CastRemovePromptSeconds);
                    Dirty(partUid, cast);
                }
                continue;
            }

            if (cast.HealCompletesAt > now)
                continue;

            if (TryComp<FractureComponent>(partUid, out var frac))
                Fracture.SetSeverity((partUid, frac), FractureSeverity.None, forceUpgrade: false);
            if (HasComp<CMUMalunionComponent>(partUid))
                RemComp<CMUMalunionComponent>(partUid);
            if (HasComp<CMUPostOpBoneSetComponent>(partUid))
                RemComp<CMUPostOpBoneSetComponent>(partUid);

            cast.ReadyToRemove = true;
            cast.NextRemovePrompt = now;
            Dirty(partUid, cast);
            RaiseLocalEvent(new CMUCastChangedEvent(partUid, false));
        }

        var postOpQuery = EntityQueryEnumerator<CMUPostOpBoneSetComponent, BodyPartComponent>();
        while (postOpQuery.MoveNext(out var partUid, out var postOp, out var part))
        {
            if (HasComp<CMUCastComponent>(partUid) || postOp.MalunionCheckAt > now)
                continue;

            if (Random.Prob(postOp.MalunionChance))
            {
                var frac = EnsureComp<FractureComponent>(partUid);
                Fracture.SetSeverity((partUid, frac), FractureSeverity.Simple);
                var malunion = EnsureComp<CMUMalunionComponent>(partUid);
                malunion.AppearedAt = now;
                Dirty(partUid, malunion);

                if (part.Body is { } body)
                    Popup.PopupEntity(Loc.GetString("cmu-medical-cast-malunion"), body, body, PopupType.MediumCaution);
            }

            RemComp<CMUPostOpBoneSetComponent>(partUid);
        }
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
