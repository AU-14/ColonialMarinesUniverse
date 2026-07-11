using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Injuries.Pain.Events;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Body.Part;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Medical.Core;

/// <summary>
///     Fans many fine-grained medical domain events into one revisioned body change stream.
/// </summary>
public sealed partial class CMUMedicalChangeSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    private HashSet<EntityUid> _pending = new();
    private HashSet<EntityUid> _flushing = new();

    public override void Initialize()
    {
        base.Initialize();

        if (_net.IsClient)
            return;

        SubscribeLocalEvent<BodyPartDamagedEvent>(OnBodyPartDamaged);
        SubscribeLocalEvent<BodyPartHealedEvent>(OnBodyPartHealed);
        SubscribeLocalEvent<BodyPartWoundAppliedEvent>(OnWoundApplied);
        SubscribeLocalEvent<BodyPartWoundsChangedEvent>(OnWoundsChanged);
        SubscribeLocalEvent<WoundTreatedEvent>(OnWoundTreated);
        SubscribeLocalEvent<BoneFracturedEvent>(OnBoneFractured);
        SubscribeLocalEvent<FractureSeverityChangedEvent>(OnFractureChanged);
        SubscribeLocalEvent<OrganDamagedEvent>(OnOrganDamaged);
        SubscribeLocalEvent<OrganStageChangedEvent>(OnOrganChanged);
        SubscribeLocalEvent<PainTierChangedEvent>(OnPainChanged);
        SubscribeLocalEvent<InternalBleedingChangedEvent>(OnInternalBleedingChanged);
        SubscribeLocalEvent<CMUShrapnelChangedEvent>(OnShrapnelChanged);
        SubscribeLocalEvent<CMUEscharChangedEvent>(OnEscharChanged);
        SubscribeLocalEvent<CMUSplintChangedEvent>(OnSplintChanged);
        SubscribeLocalEvent<CMUCastChangedEvent>(OnCastChanged);
        SubscribeLocalEvent<CMUTourniquetComponent, ComponentStartup>(OnTourniquetChanged);
        SubscribeLocalEvent<CMUTourniquetComponent, ComponentRemove>(OnTourniquetChanged);
        SubscribeLocalEvent<CMUNecroticComponent, ComponentStartup>(OnNecrosisChanged);
        SubscribeLocalEvent<CMUNecroticComponent, ComponentRemove>(OnNecrosisChanged);
        SubscribeLocalEvent<CMUSurgicalTraitChangedEvent>(OnSurgicalTraitChanged);
        SubscribeLocalEvent<CMUHumanMedicalComponent, CMSurgeryCompleteEvent>(OnSurgeryComplete);
    }

    /// <summary>
    ///     Invalidates the body's cached snapshot immediately and queues a coalesced change notification.
    /// </summary>
    public bool MarkChanged(EntityUid body, CMUMedicalChangeFlags changes)
    {
        return QueueChanged(body, changes, invalidate: true);
    }

    /// <summary>
    ///     Returns the current authoritative medical revision, or zero before an aggregate exists.
    /// </summary>
    public uint GetRevision(EntityUid body)
    {
        return TryComp<CMUMedicalAggregateComponent>(body, out var aggregate)
            ? aggregate.MedicalRevision
            : 0;
    }

    private bool QueueChanged(EntityUid body, CMUMedicalChangeFlags changes, bool invalidate)
    {
        if (_net.IsClient || changes == CMUMedicalChangeFlags.None || !HasComp<CMUHumanMedicalComponent>(body))
            return false;

        var aggregate = EnsureComp<CMUMedicalAggregateComponent>(body);
        if (aggregate.PendingChanges == CMUMedicalChangeFlags.None)
        {
            _pending.Add(body);
        }

        if (invalidate && !aggregate.PendingRevisionAdvancedByChange)
        {
            aggregate.MedicalRevision = unchecked(aggregate.MedicalRevision + 1);
            aggregate.PendingRevisionAdvancedByChange = true;
        }

        aggregate.PendingChanges |= changes;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        (_pending, _flushing) = (_flushing, _pending);
        foreach (var body in _flushing)
        {
            if (!TryComp<CMUMedicalAggregateComponent>(body, out var aggregate))
                continue;

            var changes = aggregate.PendingChanges;
            aggregate.PendingChanges = CMUMedicalChangeFlags.None;
            aggregate.PendingRevisionAdvancedByChange = false;
            if (changes == CMUMedicalChangeFlags.None)
                continue;

            var ev = new CMUMedicalChangedEvent(body, aggregate.MedicalRevision, changes);
            RaiseLocalEvent(body, ref ev);
            RaiseLocalEvent(ref ev);
        }

        _flushing.Clear();
    }

    private void OnBodyPartDamaged(ref BodyPartDamagedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Anatomy);

    private void OnBodyPartHealed(ref BodyPartHealedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Anatomy);

    private void OnWoundApplied(ref BodyPartWoundAppliedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Wounds | CMUMedicalChangeFlags.Visuals);

    private void OnWoundsChanged(ref BodyPartWoundsChangedEvent args)
        => MarkPartChanged(args.Part, CMUMedicalChangeFlags.Wounds | CMUMedicalChangeFlags.Visuals);

    private void OnWoundTreated(ref WoundTreatedEvent args)
        => MarkChanged(
            args.Body,
            CMUMedicalChangeFlags.Wounds | CMUMedicalChangeFlags.Treatment | CMUMedicalChangeFlags.Visuals);

    private void OnBoneFractured(ref BoneFracturedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Fractures);

    private void OnFractureChanged(ref FractureSeverityChangedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Fractures);

    private void OnOrganDamaged(ref OrganDamagedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Organs);

    private void OnOrganChanged(ref OrganStageChangedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Organs);

    private void OnPainChanged(ref PainTierChangedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Pain);

    private void OnInternalBleedingChanged(ref InternalBleedingChangedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Wounds);

    private void OnShrapnelChanged(ref CMUShrapnelChangedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Wounds | CMUMedicalChangeFlags.Surgery);

    private void OnEscharChanged(ref CMUEscharChangedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Wounds | CMUMedicalChangeFlags.Surgery);

    private void OnSplintChanged(ref CMUSplintChangedEvent args)
        => MarkPartChanged(args.Part, CMUMedicalChangeFlags.Treatment | CMUMedicalChangeFlags.Visuals);

    private void OnCastChanged(ref CMUCastChangedEvent args)
        => MarkPartChanged(args.Part, CMUMedicalChangeFlags.Treatment | CMUMedicalChangeFlags.Visuals);

    private void OnTourniquetChanged<TEvent>(Entity<CMUTourniquetComponent> ent, ref TEvent args)
        => MarkPartChanged(ent.Owner, CMUMedicalChangeFlags.Treatment);

    private void OnNecrosisChanged<TEvent>(Entity<CMUNecroticComponent> ent, ref TEvent args)
        => MarkPartChanged(ent.Owner, CMUMedicalChangeFlags.Wounds | CMUMedicalChangeFlags.Surgery);

    private void OnSurgicalTraitChanged(ref CMUSurgicalTraitChangedEvent args)
        => MarkChanged(args.Body, CMUMedicalChangeFlags.Surgery);

    private void OnSurgeryComplete(Entity<CMUHumanMedicalComponent> ent, ref CMSurgeryCompleteEvent args)
        => MarkChanged(ent.Owner, CMUMedicalChangeFlags.Surgery);

    private void MarkPartChanged(EntityUid part, CMUMedicalChangeFlags changes)
    {
        if (TryComp<BodyPartComponent>(part, out var bodyPart) && bodyPart.Body is { } body)
            MarkChanged(body, changes);
    }
}
