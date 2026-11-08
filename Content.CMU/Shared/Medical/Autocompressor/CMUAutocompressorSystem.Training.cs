// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._RMC14.Medical.CPR;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared.CMU14.Medical.Autocompressor;

public sealed partial class CMUAutocompressorSystem
{
    [Dependency] private SharedContainerSystem _containers = default!;

    public const string TrainingContainer = "cmu-cpr-compressor";

    private void InitializeTraining()
    {
        SubscribeLocalEvent<CPRDummyComponent, EntRemovedFromContainerMessage>(OnDummyContainerRemoved);
        SubscribeLocalEvent<CPRDummyComponent, AnchorStateChangedEvent>(OnDummyAnchorChanged);
        SubscribeLocalEvent<CMUAutocompressorPatientComponent, GetVerbsEvent<InteractionVerb>>(OnTrainingVerbs);
    }

    private bool CanAttachDummy(EntityUid dummy, out LocId reason)
    {
        reason = "cmu-autocompressor-deploy-dummy";
        if (!Transform(dummy).Anchored)
            return false;
        reason = "cmu-autocompressor-dummy-occupied";
        return !_containers.TryGetContainer(dummy, TrainingContainer, out var container)
            || container.ContainedEntities.Count == 0;
    }

    private bool TryAttachDummy(Entity<CMUAutocompressorComponent> ent, EntityUid dummy)
    {
        var container = _containers.EnsureContainer<ContainerSlot>(dummy, TrainingContainer);
        if (!_containers.Insert(ent.Owner, container))
            return false;
        AttachPatient(ent, dummy);
        return true;
    }

    private bool IsAttached(Entity<CMUAutocompressorComponent> ent, EntityUid patient)
    {
        if (HasComp<CPRDummyComponent>(patient))
            return _containers.TryGetContainer(patient, TrainingContainer, out var container)
                && container.Contains(ent.Owner);
        return _inventory.TryGetSlotEntity(patient, PatientSlot, out var item) && item == ent.Owner;
    }

    private void DetachPatient(Entity<CMUAutocompressorComponent> ent, EntityUid patient)
    {
        if (HasComp<CPRDummyComponent>(patient))
        {
            if (_containers.TryGetContainer(patient, TrainingContainer, out var container) && container.Contains(ent.Owner))
                _containers.Remove(ent.Owner, container);
        }
        else if (_inventory.TryGetSlotEntity(patient, PatientSlot, out var item) && item == ent.Owner)
            _inventory.TryUnequip(patient, PatientSlot, silent: true, force: true);
    }

    private void OnDummyContainerRemoved(Entity<CPRDummyComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!_net.IsClient && args.Container.ID == TrainingContainer
            && TryComp<CMUAutocompressorComponent>(args.Entity, out var compressor) && compressor.Patient == ent.Owner)
            Stop((args.Entity, compressor), false);
    }

    private void OnDummyAnchorChanged(Entity<CPRDummyComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!_net.IsClient && !args.Anchored
            && TryComp<CMUAutocompressorPatientComponent>(ent, out var patient)
            && patient.Device is { } device && TryComp<CMUAutocompressorComponent>(device, out var compressor)
            && compressor.Patient == ent.Owner)
            Stop((device, compressor), true);
    }

    private void OnTrainingVerbs(Entity<CMUAutocompressorPatientComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<CPRDummyComponent>(ent)
            || ent.Comp.Device is not { } device)
            return;
        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-autocompressor-remove"),
            Act = () =>
            {
                if (_net.IsClient || !_blocker.CanInteract(user, ent.Owner)
                    || !_interaction.InRangeUnobstructed(user, ent.Owner)
                    || !TryComp<CMUAutocompressorComponent>(device, out var compressor) || compressor.Patient != ent.Owner)
                    return;
                Stop((device, compressor), true);
                _hands.TryPickupAnyHand(user, device);
            },
        });
    }
}
