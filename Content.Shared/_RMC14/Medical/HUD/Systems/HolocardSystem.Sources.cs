using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Medical.HUD.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Rejuvenate;

namespace Content.Shared._RMC14.Medical.HUD.Systems;

public sealed partial class HolocardSystem
{
    private void InitializeSourceOwnership()
    {
        SubscribeLocalEvent<HolocardStateComponent, ComponentStartup>(OnHolocardStartup);
        SubscribeLocalEvent<HolocardStateComponent, RejuvenateEvent>(OnHolocardRejuvenate);
        SubscribeLocalEvent<HolocardStateComponent, MobStateChangedEvent>(OnHolocardMobStateChanged);
    }

    private void OnHolocardStartup(Entity<HolocardStateComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;
        // Legacy map/prototype labels have no provenance; preserve them as explicit
        // annotations. Saved new sources must not promote an automatic projection.
        if (ent.Comp.ManualStatus == HolocardStatus.None && ent.Comp.AutomaticStatus == HolocardStatus.None &&
            !ent.Comp.BrainRemovalAssessment)
            ent.Comp.ManualStatus = ent.Comp.HolocardStatus;
        RefreshEffectiveStatus(ent);
    }

    /// <summary>Replaces only the diagnostic owner's current assessment.</summary>
    public void SetAutomaticStatus(Entity<HolocardStateComponent?> ent, HolocardStatus status)
    {
        if (_net.IsClient || !Enum.IsDefined(status) || !Resolve(ent.Owner, ref ent.Comp, false) ||
            TerminatingOrDeleted(ent.Owner) || EntityManager.IsQueuedForDeletion(ent.Owner))
            return;
        ent.Comp.AutomaticStatus = status;
        RefreshEffectiveStatus((ent.Owner, ent.Comp));
    }

    /// <summary>Sets or resolves the existing brain-extraction assessment; this is presentation only.</summary>
    public void SetBrainRemovalAssessment(Entity<HolocardStateComponent?> ent, bool active)
    {
        if (_net.IsClient || !Resolve(ent.Owner, ref ent.Comp, false) || TerminatingOrDeleted(ent.Owner) ||
            EntityManager.IsQueuedForDeletion(ent.Owner))
            return;
        ent.Comp.BrainRemovalAssessment = active;
        RefreshEffectiveStatus((ent.Owner, ent.Comp));
    }

    private void OnHolocardMobStateChanged(Entity<HolocardStateComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead && TryComp<MobStateComponent>(ent, out var current) &&
            current.CurrentState == args.NewMobState)
            SetBrainRemovalAssessment(ent.Owner, false);
    }

    private void OnHolocardRejuvenate(Entity<HolocardStateComponent> ent, ref RejuvenateEvent args)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent.Owner))
            return;
        ent.Comp.AutomaticStatus = HolocardStatus.None;
        ent.Comp.BrainRemovalAssessment = false;
        // A deliberate annotation belongs to the user, not the healed tissue.
        RefreshEffectiveStatus(ent);
    }

    private void RefreshEffectiveStatus(Entity<HolocardStateComponent> ent)
    {
        var status = Priority(ent.Comp.AutomaticStatus) > Priority(ent.Comp.ManualStatus)
            ? ent.Comp.AutomaticStatus
            : ent.Comp.ManualStatus;
        if (ent.Comp.BrainRemovalAssessment)
            status = HolocardStatus.Permadead;
        if (ent.Comp.HolocardStatus == status)
            return;
        ent.Comp.HolocardStatus = status;
        Dirty(ent);
        // Every writer, including automatic downgrades and brain extraction, uses
        // the same effective-state notification as a manual change.
        if (_container.TryGetOuterContainer(ent, Transform(ent), out var container))
        {
            var ev = new HolocardContainerStatusUpdateEvent(status);
            RaiseLocalEvent(container.Owner, ref ev);
        }
    }

    private static int Priority(HolocardStatus status) => status switch
    {
        HolocardStatus.None => 0,
        HolocardStatus.Stable => 1,
        HolocardStatus.Urgent => 2,
        HolocardStatus.Trauma => 3,
        HolocardStatus.OrganFailure => 4,
        HolocardStatus.Emergency => 5,
        HolocardStatus.Xeno => 6,
        HolocardStatus.Permadead => 7,
        _ => 0,
    };
}
