using System;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

public abstract partial class SharedCMUTourniquetSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] private INetConfigurationManager _netCfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected SkillsSystem Skills = default!;
    [Dependency] protected SharedCMUWoundsSystem Wounds = default!;
    [Dependency] protected SharedHandsSystem Hands = default!;
    [Dependency] protected RMCUnrevivableSystem Unrevivable = default!;
    [Dependency] protected SharedCMUSplintItemSystem Splints = default!;
    [Dependency] protected SharedCMUOpenStumpSystem Stumps = default!;
    private const float TourniquetScanInterval = 0.5f;
    private float _tourniquetScanAccumulator;

    private bool _medicalEnabled;
    private bool _woundsEnabled;
    private float _necrosisMinutes;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUTourniquetItemComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUTourniquetItemComponent, CMUTourniquetApplyDoAfterEvent>(OnApplyDoAfter);
        SubscribeLocalEvent<CMUHumanMedicalComponent, GetVerbsEvent<AlternativeVerb>>(OnPatientGetAltVerbs);
        SubscribeLocalEvent<CMUHumanMedicalComponent, CMUTourniquetVerbRemoveDoAfterEvent>(OnVerbRemoveDoAfter);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.WoundsEnabled, v => _woundsEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.TourniquetNecrosisMinutes, v => _necrosisMinutes = v, true);
    }

    public bool IsLayerEnabled()
    {
        return _medicalEnabled && _woundsEnabled;
    }

    private void OnAfterInteract(Entity<CMUTourniquetItemComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
        {
            return;
        }
        if (!HasComp<CMUHumanMedicalComponent>(target))
        {
            return;
        }
        if (HasComp<SynthComponent>(target))
        {
            return;
        }

        CMUStump? stump = null;
        EntityUid part;
        var alreadyOn = false;
        // CMU14: with targeted healing on, the tourniquet only goes on the selected limb, like bandages and kits.
        if (IsTargetedHealingEnabled(args.User))
        {
            if (!TryFindSelectedTourniquetSite(args.User, target, out part, out stump, out alreadyOn))
            {
                Popup.PopupClient(Loc.GetString("cmu-medical-tourniquet-no-selected-limb"), target, args.User, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }
        }
        // CMU14: a limb that needs a tourniquet takes priority over where the user is aiming.
        else if (TryFindNeededTourniquetSite(args.User, target, out var neededPart, out var neededStump))
        {
            part = neededPart;
            stump = neededStump;
        }
        else if (TryFindAimedStump(args.User, target, out var stumpParent, out var aimedStump))
        {
            part = stumpParent;
            stump = aimedStump;
            alreadyOn = aimedStump.Clamped;
        }
        else if (!TryFindTourniquetTargetPart(args.User, target, out part, out alreadyOn))
        {
            if (!Stumps.TryFindUnclampedStump(target, out var openParent, out var openStump))
                return;

            part = openParent;
            stump = openStump;
        }

        if (alreadyOn)
        {
            Popup.PopupBroadcast(Loc.GetString("cmu-medical-tourniquet-already-on"), target, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var applyEv = new CMUTourniquetApplyDoAfterEvent
        {
            PreSelectedPart = GetNetEntity(part),
            StumpType = stump?.Type,
            StumpSymmetry = stump?.Symmetry ?? default,
        };
        var applyDo = new DoAfterArgs(EntityManager, args.User, ent.Comp.ApplyDelay,
            applyEv, ent.Owner, target: target, used: ent.Owner)
        {
            BlockDuplicate = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        };
        var applyStarted = DoAfter.TryStartDoAfter(applyDo);
        if (applyStarted)
            Popup.PopupBroadcast(Loc.GetString("cmu-medical-tourniquet-applying"), target, args.User);
        args.Handled = true;
    }

    private void OnApplyDoAfter(Entity<CMUTourniquetItemComponent> ent, ref CMUTourniquetApplyDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;
        if (!IsLayerEnabled())
        {
            return;
        }
        if (HasComp<SynthComponent>(target))
        {
            return;
        }

        if (!ResolvePart(target, args.PreSelectedPart, out var part))
        {
            return;
        }

        if (args.StumpType is { } stumpType)
        {
            if (!Stumps.TryFindStump(target, stumpType, args.StumpSymmetry, out var stumpParent, out var stump) ||
                stump.Clamped)
            {
                return;
            }

            Stumps.SetClamped(stumpParent, stump, true, ent.Comp.RefundOnRemove);
            if (ent.Comp.ApplySound is not null)
                Audio.PlayPredicted(ent.Comp.ApplySound, stumpParent, null);
            if (ent.Comp.ConsumedOnApply && Net.IsServer)
                QueueDel(ent.Owner);
            Popup.PopupBroadcast(Loc.GetString("cmu-medical-tourniquet-applied-stump"), target, args.User);
            return;
        }

        var freshApply = !HasComp<CMUTourniquetComponent>(part);
        var ok = ApplyTourniquetToPart(ent, part);
        if (ok && freshApply)
            Popup.PopupBroadcast(Loc.GetString("cmu-medical-tourniquet-applied"), target, args.User);
    }

    private void OnPatientGetAltVerbs(Entity<CMUHumanMedicalComponent> patient, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (IsLayerEnabled()
            && args.CanInteract
            && args.CanAccess
            && FindClampedStump(args.User, patient.Owner, out var clampedParent, out var clampedStump))
        {
            var stumpUser = args.User;
            var stumpPatient = patient.Owner;
            var stumpType = clampedStump.Type;
            var stumpSymmetry = clampedStump.Symmetry;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-medical-tourniquet-verb-remove-stump"),
                Act = () => StartVerbRemoveDoAfter(stumpUser, stumpPatient, clampedParent, stumpType, stumpSymmetry),
                Priority = 1,
            });
        }
        else if (IsLayerEnabled()
            && args.CanInteract
            && args.CanAccess
            && FindTourniquettedLimb(args.User, patient.Owner, out var part))
        {
            var user = args.User;
            var patientUid = patient.Owner;
            var verb = new AlternativeVerb
            {
                Text = Loc.GetString("cmu-medical-tourniquet-verb-remove"),
                Act = () => StartVerbRemoveDoAfter(user, patientUid, part),
                Priority = 1,
            };
            args.Verbs.Add(verb);
        }

        Splints.AddSupportRemoveVerbs(patient, ref args);
    }

    private void StartVerbRemoveDoAfter(EntityUid user, EntityUid patient, EntityUid part, BodyPartType? stumpType = null, BodyPartSymmetry stumpSymmetry = default)
    {
        var removeEv = new CMUTourniquetVerbRemoveDoAfterEvent
        {
            PreSelectedPart = GetNetEntity(part),
            StumpType = stumpType,
            StumpSymmetry = stumpSymmetry,
        };
        var removeDo = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(1.0),
            removeEv, patient, target: patient)
        {
            BlockDuplicate = true,
        };
        var started = DoAfter.TryStartDoAfter(removeDo);
        if (started)
            Popup.PopupBroadcast(Loc.GetString("cmu-medical-tourniquet-removing"), patient, user);
    }

    private void OnVerbRemoveDoAfter(Entity<CMUHumanMedicalComponent> patient, ref CMUTourniquetVerbRemoveDoAfterEvent args)
    {
        if (args.Cancelled)
            return;
        if (!IsLayerEnabled())
            return;
        if (!ResolvePart(patient.Owner, args.PreSelectedPart, out var part))
            return;

        if (args.StumpType is { } stumpType)
        {
            if (!Stumps.TryFindStump(patient.Owner, stumpType, args.StumpSymmetry, out var stumpParent, out var stump) ||
                !stump.Clamped)
            {
                return;
            }

            var stumpRefund = stump.ClampRefund;
            Stumps.SetClamped(stumpParent, stump, false, null);
            Popup.PopupBroadcast(Loc.GetString("cmu-medical-tourniquet-removed"), patient.Owner, args.User);
            if (Net.IsServer && stumpRefund is { } stumpProto)
            {
                var refunded = Spawn(stumpProto, Transform(args.User).Coordinates);
                Hands.TryPickupAnyHand(args.User, refunded);
            }
            return;
        }

        if (!TryComp<CMUTourniquetComponent>(part, out var tq))
            return;

        var refundProto = tq.RefundOnRemove;
        RemComp<CMUTourniquetComponent>(part);
        if (HasComp<CMUNecroticComponent>(part))
            RemComp<CMUNecroticComponent>(part);

        Popup.PopupBroadcast(Loc.GetString("cmu-medical-tourniquet-removed"), patient.Owner, args.User);

        if (Net.IsServer && refundProto is { } proto)
        {
            var coords = Transform(args.User).Coordinates;
            var newItem = Spawn(proto, coords);
            Hands.TryPickupAnyHand(args.User, newItem);
        }
    }

    /// <summary>
    ///     Applies reversible blood-flow occlusion. Reapplication cannot reset the ischemia clock.
    /// </summary>
    public bool ApplyTourniquetToPart(Entity<CMUTourniquetItemComponent> ent, EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var bodyPart) || bodyPart.Body is null ||
            !IsTourniquetable(bodyPart.PartType) || HasComp<CMUTourniquetComponent>(part))
            return false;
        if (HasComp<CMURoboticLimbComponent>(part))
            return false;
        if (HasComp<SynthComponent>(part))
            return false;
        if (Wounds.TryGetBodyOwner(part) is { } body && HasComp<SynthComponent>(body))
            return false;

        var now = Timing.CurTime;
        var necrosisOffset = TimeSpan.FromMinutes(_necrosisMinutes);

        var tq = EnsureComp<CMUTourniquetComponent>(part);
        tq.AppliedAt = now;
        tq.NecrosisAt = now + necrosisOffset;
        tq.RefundOnRemove = ent.Comp.RefundOnRemove;
        Dirty(part, tq);

        if (ent.Comp.ApplySound is not null)
            Audio.PlayPredicted(ent.Comp.ApplySound, part, null);

        if (ent.Comp.ConsumedOnApply && Net.IsServer)
            QueueDel(ent.Owner);

        return true;
    }


    /// <summary>
    ///     Tier 1: medic's body-zone aim picker. Tier 2: first arm/leg
    ///     already wearing a tourniquet (so a follow-up click removes
    ///     without re-aiming). Tier 3: first wounded arm/leg.
    /// </summary>
    public bool TryFindTourniquetTargetPart(EntityUid user, EntityUid patient, out EntityUid part, out bool alreadyOn)
    {
        part = default;
        alreadyOn = false;

        if (TryComp<BodyZoneTargetingComponent>(user, out var aim) && aim.LastSelectedAt > TimeSpan.Zero)
        {
            var (partType, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);
            EntityUid? firstMatch = null;
            foreach (var (id, partComp) in MedicalIndex.GetBodyParts(patient))
            {
                if (partComp.PartType != partType)
                    continue;
                if (symmetry is { } s && partComp.Symmetry != s)
                {
                    continue;
                }
                if (!IsTourniquetable(partComp.PartType))
                {
                    continue;
                }
                if (HasComp<CMURoboticLimbComponent>(id))
                {
                    continue;
                }

                if (HasComp<CMUTourniquetComponent>(id))
                {
                    part = id;
                    alreadyOn = true;
                    return true;
                }
                firstMatch ??= id;
            }
            if (firstMatch is { } found)
            {
                part = found;
                alreadyOn = false;
                return true;
            }
        }

        foreach (var (id, partComp) in MedicalIndex.GetBodyParts(patient))
        {
            if (!IsTourniquetable(partComp.PartType))
                continue;
            if (HasComp<CMURoboticLimbComponent>(id))
                continue;
            if (HasComp<CMUTourniquetComponent>(id))
            {
                part = id;
                alreadyOn = true;
                return true;
            }
        }

        foreach (var (id, partComp) in MedicalIndex.GetBodyParts(patient))
        {
            if (!IsTourniquetable(partComp.PartType))
                continue;
            if (HasComp<CMURoboticLimbComponent>(id))
                continue;
            if (HasComp<BodyPartWoundComponent>(id)) // CMU14: internal bleeding isn't visible, so it can't pick the limb
            {
                part = id;
                alreadyOn = false;
                return true;
            }
        }

        return false;
    }

    /// <summary>CMU14: whether the user wants treatments kept to the body part they've selected.</summary>
    private bool IsTargetedHealingEnabled(EntityUid user)
    {
        if (Net.IsClient)
            return Cfg.GetCVar(CMUMedicalCCVars.TargetedHealingEnabled);

        return TryComp<ActorComponent>(user, out var actor) &&
               _netCfg.GetClientCVar(actor.PlayerSession.Channel, CMUMedicalCCVars.TargetedHealingEnabled);
    }

    /// <summary>
    /// CMU14: the tourniquet site on the body part the user has selected: the stump of a missing limb there, or the arm
    /// or leg the part belongs to.
    /// </summary>
    private bool TryFindSelectedTourniquetSite(
        EntityUid user,
        EntityUid patient,
        out EntityUid part,
        out CMUStump? stump,
        out bool alreadyOn)
    {
        part = default;
        stump = null;
        alreadyOn = false;
        if (!TryComp<BodyZoneTargetingComponent>(user, out var aim))
            return false;

        var (partType, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);
        if (Stumps.TryFindStump(patient, partType, symmetry, out var stumpParent, out var selectedStump))
        {
            part = stumpParent;
            stump = selectedStump;
            alreadyOn = selectedStump.Clamped;
            return true;
        }

        foreach (var (id, partComp) in MedicalIndex.GetBodyParts(patient))
        {
            if (partComp.PartType != partType || partComp.Symmetry != symmetry)
                continue;

            if (!TryGetTourniquetLimb(id, out var limb))
                return false;

            part = limb;
            alreadyOn = HasComp<CMUTourniquetComponent>(limb);
            return true;
        }

        return false;
    }

    /// <summary>
    /// CMU14: the limb that needs a tourniquet most: an open stump, then an arterial bleed, then a severe bleed. Only
    /// visible bleeding counts. Between two of the same kind, the one the user is aiming at wins.
    /// </summary>
    private bool TryFindNeededTourniquetSite(EntityUid user, EntityUid patient, out EntityUid part, out CMUStump? stump)
    {
        part = default;
        stump = null;

        (BodyPartType Type, BodyPartSymmetry Symmetry)? aimedAt = null;
        if (TryComp<BodyZoneTargetingComponent>(user, out var aim) && aim.LastSelectedAt > TimeSpan.Zero)
            aimedAt = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);

        bool IsAimed(BodyPartType type, BodyPartSymmetry symmetry)
            => aimedAt is { } aimed && aimed.Type == type && aimed.Symmetry == symmetry;

        var bestScore = -1;
        foreach (var (id, partComp) in MedicalIndex.GetBodyParts(patient))
        {
            if (Wounds.IsBloodFlowOccluded(id))
                continue;

            if (TryComp<CMUOpenStumpComponent>(id, out var stumps))
            {
                foreach (var open in stumps.Stumps)
                {
                    if (open.Clamped)
                        continue;

                    var score = NeedScore(StumpNeed, IsAimed(open.Type, open.Symmetry));
                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    part = id;
                    stump = open;
                }
            }

            if (!TryComp<BodyPartWoundComponent>(id, out var wounds) ||
                wounds.ExternalBleeding < ExternalBleedTier.Severe ||
                !TryGetTourniquetLimb(id, out var limb))
            {
                continue;
            }

            var limbComp = Comp<BodyPartComponent>(limb);
            var aimedLimb = IsAimed(partComp.PartType, partComp.Symmetry) || IsAimed(limbComp.PartType, limbComp.Symmetry);
            var need = wounds.ExternalBleeding == ExternalBleedTier.Arterial ? ArterialNeed : SevereNeed;
            var woundScore = NeedScore(need, aimedLimb);
            if (woundScore <= bestScore)
                continue;

            bestScore = woundScore;
            part = limb;
            stump = null;
        }

        return bestScore >= 0;
    }

    private const int StumpNeed = 3;
    private const int ArterialNeed = 2;
    private const int SevereNeed = 1;

    private static int NeedScore(int need, bool aimed)
        => need * 10 + (aimed ? 1 : 0);

    /// <summary>
    /// CMU14: the arm or leg a tourniquet goes on to stop bleeding from this part: the part itself, or the limb a
    /// hand or foot hangs off.
    /// </summary>
    private bool TryGetTourniquetLimb(EntityUid part, out EntityUid limb)
    {
        limb = default;
        var current = part;
        while (TryComp<BodyPartComponent>(current, out var site))
        {
            if (IsTourniquetable(site.PartType))
            {
                if (HasComp<CMURoboticLimbComponent>(current))
                    return false;

                limb = current;
                return true;
            }

            if (!TryComp<ChildOrganComponent>(current, out var child) || child.Parent is not { } parent)
                return false;

            current = parent;
        }

        return false;
    }

    private bool FindTourniquettedLimb(EntityUid user, EntityUid patient, out EntityUid part)
    {
        part = default;
        if (TryComp<BodyZoneTargetingComponent>(user, out var aim) && aim.LastSelectedAt > TimeSpan.Zero)
        {
            var (partType, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);
            foreach (var (id, partComp) in MedicalIndex.GetBodyParts(patient))
            {
                if (partComp.PartType != partType)
                    continue;
                if (partComp.Symmetry != symmetry)
                    continue;
                if (!HasComp<CMUTourniquetComponent>(id))
                    continue;
                part = id;
                return true;
            }
        }

        foreach (var (id, partComp) in MedicalIndex.GetBodyParts(patient))
        {
            if (!IsTourniquetable(partComp.PartType))
                continue;
            if (HasComp<CMUTourniquetComponent>(id))
            {
                part = id;
                return true;
            }
        }

        return false;
    }

    /// <summary>The stump of the missing limb the user is aiming at, if there is one.</summary>
    private bool TryFindAimedStump(EntityUid user, EntityUid patient, out EntityUid parentPart, out CMUStump stump)
    {
        parentPart = default;
        stump = default!;
        if (!TryComp<BodyZoneTargetingComponent>(user, out var aim) || aim.LastSelectedAt <= TimeSpan.Zero)
            return false;

        var (partType, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);
        return symmetry is { } side && Stumps.TryFindStump(patient, partType, side, out parentPart, out stump);
    }

    /// <summary>A stump with a tourniquet on it: the aimed one, or any when the user isn't aiming.</summary>
    private bool FindClampedStump(EntityUid user, EntityUid patient, out EntityUid parentPart, out CMUStump stump)
    {
        if (TryComp<BodyZoneTargetingComponent>(user, out var aim) && aim.LastSelectedAt > TimeSpan.Zero)
            return TryFindAimedStump(user, patient, out parentPart, out stump) && stump.Clamped;

        foreach (var (partUid, _) in MedicalIndex.GetBodyParts(patient))
        {
            if (!TryComp<CMUOpenStumpComponent>(partUid, out var comp))
                continue;

            foreach (var candidate in comp.Stumps)
            {
                if (!candidate.Clamped)
                    continue;

                parentPart = partUid;
                stump = candidate;
                return true;
            }
        }

        parentPart = default;
        stump = default!;
        return false;
    }

    private static bool IsTourniquetable(BodyPartType type)
        => type is BodyPartType.Arm or BodyPartType.Leg;

    private bool ResolvePart(EntityUid patient, NetEntity? netPart, out EntityUid part)
    {
        part = default;
        if (netPart is { } ne && TryGetEntity(ne, out var stored) &&
            TryComp<BodyPartComponent>(stored.Value, out var bodyPart) && bodyPart.Body == patient)
        {
            part = stored.Value;
            return true;
        }
        return false;
    }

    protected void UpdateServer(float frameTime)
    {
        if (!IsLayerEnabled())
            return;

        _tourniquetScanAccumulator += frameTime;
        if (_tourniquetScanAccumulator < TourniquetScanInterval)
            return;
        _tourniquetScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<CMUTourniquetComponent, BodyPartComponent>();
        while (query.MoveNext(out var partUid, out var tq, out var part))
        {
            if (part.Body is not { } body ||
                Unrevivable.IsUnrevivable(body) ||
                HasComp<SynthComponent>(body) ||
                HasComp<CMURoboticLimbComponent>(partUid))
            {
                continue;
            }

            if (tq.NextUpdate > now)
                continue;
            tq.NextUpdate = now + TimeSpan.FromSeconds(1);
            if (now >= tq.NecrosisAt && !HasComp<CMUNecroticComponent>(partUid))
            {
                var nec = AddComp<CMUNecroticComponent>(partUid);
                nec.AppliedAt = now;
                Dirty(partUid, nec);
            }
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUTourniquetApplyDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity? PreSelectedPart;

    /// <summary>Set when the tourniquet goes on the stump of this missing limb instead of a limb.</summary>
    [DataField]
    public BodyPartType? StumpType;

    [DataField]
    public BodyPartSymmetry StumpSymmetry;
}

[Serializable, NetSerializable]
public sealed partial class CMUTourniquetVerbRemoveDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity? PreSelectedPart;

    [DataField]
    public BodyPartType? StumpType;

    [DataField]
    public BodyPartSymmetry StumpSymmetry;
}
