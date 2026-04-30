using System;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Wounds;

public abstract class SharedCMUTourniquetSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly SkillsSystem Skills = default!;
    [Dependency] protected readonly SharedCMUWoundsSystem Wounds = default!;
    [Dependency] protected readonly SharedHandsSystem Hands = default!;
    [Dependency] protected readonly RMCUnrevivableSystem Unrevivable = default!;
    private const float TourniquetScanInterval = 0.5f;
    private float _tourniquetScanAccumulator;

    private bool _medicalEnabled;
    private bool _woundsEnabled;
    private float _necrosisMinutes;
    private float _severanceMinutes;

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
        Cfg.OnValueChanged(CMUMedicalCCVars.TourniquetSeveranceMinutes, v => _severanceMinutes = v, true);
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

        if (!TryFindTourniquetTargetPart(args.User, target, out var part, out var alreadyOn))
        {
            return;
        }

        if (alreadyOn)
        {
            Popup.PopupPredicted(Loc.GetString("cmu-medical-tourniquet-already-on"), target, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var applyEv = new CMUTourniquetApplyDoAfterEvent { PreSelectedPart = GetNetEntity(part) };
        var applyDo = new DoAfterArgs(EntityManager, args.User, ent.Comp.ApplyDelay,
            applyEv, ent.Owner, target: target, used: ent.Owner)
        {
            BlockDuplicate = true,
        };
        var applyStarted = DoAfter.TryStartDoAfter(applyDo);
        if (applyStarted)
            Popup.PopupPredicted(Loc.GetString("cmu-medical-tourniquet-applying"), target, args.User);
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

        if (!ResolvePart(target, args.PreSelectedPart, out var part))
        {
            return;
        }

        var freshApply = !HasComp<CMUTourniquetComponent>(part);
        var ok = ApplyTourniquetToPart(ent, part);
        if (ok && freshApply)
            Popup.PopupPredicted(Loc.GetString("cmu-medical-tourniquet-applied"), target, args.User);
    }

    private void OnPatientGetAltVerbs(Entity<CMUHumanMedicalComponent> patient, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsLayerEnabled())
            return;
        if (!args.CanInteract || !args.CanAccess)
            return;
        if (!FindTourniquettedLimb(args.User, patient.Owner, out var part))
            return;

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

    private void StartVerbRemoveDoAfter(EntityUid user, EntityUid patient, EntityUid part)
    {
        var removeEv = new CMUTourniquetVerbRemoveDoAfterEvent { PreSelectedPart = GetNetEntity(part) };
        var removeDo = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(1.0),
            removeEv, patient, target: patient)
        {
            BlockDuplicate = true,
        };
        var started = DoAfter.TryStartDoAfter(removeDo);
        if (started)
            Popup.PopupPredicted(Loc.GetString("cmu-medical-tourniquet-removing"), patient, user);
    }

    private void OnVerbRemoveDoAfter(Entity<CMUHumanMedicalComponent> patient, ref CMUTourniquetVerbRemoveDoAfterEvent args)
    {
        if (args.Cancelled)
            return;
        if (!IsLayerEnabled())
            return;
        if (!ResolvePart(patient.Owner, args.PreSelectedPart, out var part))
            return;
        if (!TryComp<CMUTourniquetComponent>(part, out var tq))
            return;

        var refundProto = tq.RefundOnRemove;
        RemComp<CMUTourniquetComponent>(part);
        if (HasComp<CMUNecroticComponent>(part))
            RemComp<CMUNecroticComponent>(part);

        Popup.PopupPredicted(Loc.GetString("cmu-medical-tourniquet-removed"), patient.Owner, args.User);

        if (Net.IsServer && refundProto is { } proto)
        {
            var coords = Transform(args.User).Coordinates;
            var newItem = Spawn(proto, coords);
            Hands.TryPickupAnyHand(args.User, newItem);
        }
    }

    /// <summary>
    ///     Idempotent — re-applying refreshes the necrosis countdown to "now".
    /// </summary>
    public bool ApplyTourniquetToPart(Entity<CMUTourniquetItemComponent> ent, EntityUid part)
    {
        if (!HasComp<BodyPartComponent>(part))
            return false;

        var now = Timing.CurTime;
        var necrosisOffset = TimeSpan.FromMinutes(_necrosisMinutes);
        var severanceOffset = TimeSpan.FromMinutes(_severanceMinutes);

        var tq = EnsureComp<CMUTourniquetComponent>(part);
        tq.AppliedAt = now;
        tq.NecrosisAt = now + necrosisOffset;
        tq.SeveranceAt = tq.NecrosisAt + severanceOffset;
        tq.RefundOnRemove = ent.Comp.RefundOnRemove;
        Dirty(part, tq);

        Wounds.StopSurfaceBleedingOnPart(part);

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
            foreach (var (id, partComp) in Body.GetBodyChildren(patient))
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

        foreach (var (id, partComp) in Body.GetBodyChildren(patient))
        {
            if (!IsTourniquetable(partComp.PartType))
                continue;
            if (HasComp<CMUTourniquetComponent>(id))
            {
                part = id;
                alreadyOn = true;
                return true;
            }
        }

        foreach (var (id, partComp) in Body.GetBodyChildren(patient))
        {
            if (!IsTourniquetable(partComp.PartType))
                continue;
            if (HasComp<BodyPartWoundComponent>(id) || HasComp<InternalBleedingComponent>(id))
            {
                part = id;
                alreadyOn = false;
                return true;
            }
        }

        return false;
    }

    private bool FindTourniquettedLimb(EntityUid user, EntityUid patient, out EntityUid part)
    {
        part = default;
        if (TryComp<BodyZoneTargetingComponent>(user, out var aim) && aim.LastSelectedAt > TimeSpan.Zero)
        {
            var (partType, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);
            foreach (var (id, partComp) in Body.GetBodyChildren(patient))
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

        foreach (var (id, partComp) in Body.GetBodyChildren(patient))
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

    private static bool IsTourniquetable(BodyPartType type)
        => type is BodyPartType.Arm or BodyPartType.Leg;

    private bool ResolvePart(EntityUid patient, NetEntity? netPart, out EntityUid part)
    {
        part = default;
        if (netPart is { } ne && TryGetEntity(ne, out var stored) && HasComp<BodyPartComponent>(stored.Value))
        {
            part = stored.Value;
            return true;
        }
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

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
            if (part.Body is not { } body || Unrevivable.IsUnrevivable(body))
                continue;

            if (tq.NextUpdate > now)
                continue;
            tq.NextUpdate = now + TimeSpan.FromSeconds(1);
            if (now >= tq.NecrosisAt && !HasComp<CMUNecroticComponent>(partUid))
            {
                var nec = AddComp<CMUNecroticComponent>(partUid);
                nec.AppliedAt = now;
                Dirty(partUid, nec);
            }

            if (now >= tq.SeveranceAt && Net.IsServer)
            {
                // Strip the tourniquet first so its presence doesn't keep
                // skipping the bleed tick on a part that's about to drop.
                RemComp<CMUTourniquetComponent>(partUid);
                if (HasComp<CMUNecroticComponent>(partUid))
                    RemComp<CMUNecroticComponent>(partUid);

                var ev = new BodyPartSeveredEvent(body, partUid, part.PartType);
                RaiseLocalEvent(partUid, ref ev);
            }
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUTourniquetApplyDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity? PreSelectedPart;
}

[Serializable, NetSerializable]
public sealed partial class CMUTourniquetVerbRemoveDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity? PreSelectedPart;
}
