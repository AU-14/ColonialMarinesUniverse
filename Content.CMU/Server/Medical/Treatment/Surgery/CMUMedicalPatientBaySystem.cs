using System.Numerics;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared.Body.Components;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUMedicalPatientBaySystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly Vector2[] EjectOffsets =
    [
        Vector2.Zero,
        new(0f, 1f),
        new(1f, 0f),
        new(-1f, 0f),
        new(0f, -1f),
        new(1f, 1f),
        new(-1f, 1f),
        new(1f, -1f),
        new(-1f, -1f),
    ];

    public ContainerSlot EnsureBodyContainer(EntityUid pod, string containerId)
    {
        return _containers.EnsureContainer<ContainerSlot>(pod, containerId);
    }

    public bool TryFindNearestPod<TPod>(
        EntityUid console,
        float linkRange,
        out EntityUid pod,
        out TPod podComp)
        where TPod : Component
    {
        pod = default;
        podComp = default!;
        var consoleCoords = Transform(console).Coordinates;
        var bestDistance = float.MaxValue;

        foreach (var candidate in _lookup.GetEntitiesInRange<TPod>(consoleCoords, linkRange))
        {
            if (!consoleCoords.TryDistance(EntityManager, Transform(candidate).Coordinates, out var distance))
                continue;
            if (distance >= bestDistance)
                continue;

            pod = candidate;
            podComp = Comp<TPod>(candidate);
            bestDistance = distance;
        }

        return pod.IsValid();
    }

    public bool TryGetPatient(ContainerSlot bodyContainer, out EntityUid patient)
    {
        patient = default;
        if (bodyContainer.ContainedEntity is not { } contained)
            return false;

        patient = contained;
        return true;
    }

    public bool ContainsPatient(ContainerSlot bodyContainer, EntityUid patient)
    {
        return bodyContainer.ContainedEntity == patient;
    }

    public bool CanInsertPatient(ContainerSlot bodyContainer, EntityUid patient)
    {
        return bodyContainer.ContainedEntity is null && HasComp<BodyComponent>(patient);
    }

    public void StartInsertDoAfter(EntityUid pod, EntityUid user, EntityUid target, TimeSpan entryDelay)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, entryDelay, new CMUMedicalPodInsertDoAfterEvent(), pod, target, pod)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            CancelDuplicate = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    public bool TryInsertPatient(EntityUid pod, ContainerSlot bodyContainer, EntityUid patient)
    {
        if (!CanInsertPatient(bodyContainer, patient))
            return false;

        if (!_containers.Insert(patient, bodyContainer))
            return false;

        UpdatePodAppearance(pod, bodyContainer);
        return true;
    }

    public bool TryEjectPatient(EntityUid pod, ContainerSlot bodyContainer, EntityUid patient)
    {
        if (!ContainsPatient(bodyContainer, patient))
            return false;

        _containers.Remove(patient, bodyContainer);
        MoveEjectedPatientToPod(pod, patient);
        UpdatePodAppearance(pod, bodyContainer);
        return true;
    }

    public void UpdatePodAppearance(EntityUid pod, ContainerSlot bodyContainer)
    {
        _appearance.SetData(pod, CMUMedicalPodVisuals.Occupied, bodyContainer.ContainedEntity is not null);
    }

    private void MoveEjectedPatientToPod(EntityUid pod, EntityUid patient)
    {
        if (TerminatingOrDeleted(patient))
            return;

        var podCoords = Transform(pod).Coordinates;
        _transform.SetCoordinates(patient, GetPodEjectCoordinates(podCoords));
    }

    private EntityCoordinates GetPodEjectCoordinates(EntityCoordinates podCoords)
    {
        foreach (var offset in EjectOffsets)
        {
            var candidate = podCoords.Offset(offset);
            if (CanEjectTo(candidate))
                return candidate;
        }

        return podCoords;
    }

    private bool CanEjectTo(EntityCoordinates coordinates)
    {
        return _turf.TryGetTileRef(coordinates, out var tile) &&
               !tile.Value.Tile.IsEmpty &&
               !_turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable);
    }
}
