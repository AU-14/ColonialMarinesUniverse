using System.Linq;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
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

    private const float CastScanInterval = 1f;
    private float _castScanAccumulator;

    private bool _medicalEnabled;
    private bool _boneEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUSplintItemComponent, AfterInteractEvent>(OnSplintInteract);
        SubscribeLocalEvent<CMUSplintItemComponent, CMUSplintApplyDoAfterEvent>(OnSplintDoAfter);
        SubscribeLocalEvent<CMUCastItemComponent, AfterInteractEvent>(OnCastInteract);
        SubscribeLocalEvent<CMUCastItemComponent, CMUCastApplyDoAfterEvent>(OnCastDoAfter);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BoneEnabled, v => _boneEnabled = v, true);
    }

    public bool IsLayerEnabled()
    {
        return _medicalEnabled && _boneEnabled;
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
        Dirty(part, splinted);

        if (ent.Comp.ApplySound is not null)
            Audio.PlayPredicted(ent.Comp.ApplySound, part, null);

        if (ent.Comp.ConsumedOnApply && Net.IsServer)
            QueueDel(ent.Owner);

        return true;
    }

    private void OnCastInteract(Entity<CMUCastItemComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (!HasComp<CMUHumanMedicalComponent>(target))
            return;
        if (!TryFindFracturedPart(target, out var part, args.User))
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
            && HasComp<FractureComponent>(stored.Value))
        {
            part = stored.Value;
        }
        else if (!TryFindFracturedPart(target, out part, args.User))
        {
            return;
        }
        ApplyCastToPart(ent, part);
    }

    public bool ApplyCastToPart(Entity<CMUCastItemComponent> ent, EntityUid part)
    {
        if (!TryComp<FractureComponent>(part, out var frac))
            return false;
        if (!ent.Comp.HealMinutesPerSeverity.TryGetValue(frac.Severity, out var minutes))
        {
            // Cast can't help this severity (Compound+ — surgery only).
            return false;
        }

        var cast = EnsureComp<CMUCastComponent>(part);
        cast.AppliedAt = Timing.CurTime;
        cast.HealCompletesAt = Timing.CurTime + TimeSpan.FromMinutes(minutes);
        if ((byte)ent.Comp.MaxSuppressed > (byte)cast.MaxSuppressed)
            cast.MaxSuppressed = ent.Comp.MaxSuppressed;
        Dirty(part, cast);

        if (ent.Comp.ApplySound is not null)
            Audio.PlayPredicted(ent.Comp.ApplySound, part, null);

        if (ent.Comp.ConsumedOnApply && Net.IsServer)
            QueueDel(ent.Owner);

        return true;
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
        var query = EntityQueryEnumerator<CMUCastComponent, FractureComponent>();
        while (query.MoveNext(out var partUid, out var cast, out var frac))
        {
            if (cast.HealCompletesAt > now)
                continue;
            Fracture.SetSeverity((partUid, frac), FractureSeverity.None, forceUpgrade: false);
            RemComp<CMUCastComponent>(partUid);
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
