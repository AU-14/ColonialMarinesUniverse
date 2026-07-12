using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Medical.Core;

/// <summary>
///     Maintains a structural medical index and exposes cached snapshots to callers.
/// </summary>
public sealed partial class CMUMedicalBodyIndexSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private CMUMedicalChangeSystem _changes = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUHumanMedicalComponent, ComponentInit>(OnMedicalInit);
        SubscribeLocalEvent<CMUMedicalBodyIndexComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<CMUMedicalBodyIndexComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);
        SubscribeLocalEvent<OrganHealthComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<OrganHealthComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
    }

    /// <summary>
    ///     Resolves a body part without traversing the body's part hierarchy.
    /// </summary>
    public bool TryGetBodyPart(
        EntityUid body,
        CMUMedicalBodyPartKey key,
        out EntityUid part)
    {
        part = default;
        if (TryComp<CMUMedicalBodyIndexComponent>(body, out var index))
            return index.BodyParts.TryGetValue(key, out part);

        foreach (var candidate in _body.GetBodyChildren(body))
        {
            if (candidate.Component.PartType != key.Type || candidate.Component.Symmetry != key.Symmetry)
                continue;

            part = candidate.Id;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Resolves the body's root part without looking through its root container.
    /// </summary>
    public bool TryGetRootPart(EntityUid body, out Entity<BodyPartComponent> root)
    {
        root = default;
        if (TryComp<CMUMedicalBodyIndexComponent>(body, out var index))
        {
            if (index.RootPart is not { } indexed ||
                !TryComp<BodyPartComponent>(indexed, out var indexedComponent))
            {
                return false;
            }

            root = (indexed, indexedComponent);
            return true;
        }

        if (_body.GetRootPartOrNull(body) is not { } fallback)
            return false;

        root = (fallback.Entity, fallback.BodyPart);
        return true;
    }

    /// <summary>
    ///     Enumerates every configured child-part slot and its optional occupant in prototype order.
    /// </summary>
    public IEnumerable<CMUMedicalBodyPartSlotEntry> GetBodyPartSlots(EntityUid part)
    {
        if (TryComp<BodyPartComponent>(part, out var partComponent) &&
            partComponent.Body is { } body &&
            TryComp<CMUMedicalBodyIndexComponent>(body, out var index) &&
            index.PartChildSlots.TryGetValue(part, out var slots))
        {
            foreach (var slot in slots)
                yield return slot;

            yield break;
        }

        if (partComponent is null)
            yield break;

        foreach (var slot in ReadBodyPartSlots(part, partComponent))
            yield return slot;
    }

    /// <summary>
    ///     Resolves the current occupant of a configured child-part slot.
    /// </summary>
    public bool TryGetBodyPartInSlot(EntityUid part, string slotId, out EntityUid child)
    {
        foreach (var slot in GetBodyPartSlots(part))
        {
            if (slot.SlotId != slotId || slot.Part is not { } occupant)
                continue;

            child = occupant;
            return true;
        }

        child = default;
        return false;
    }

    /// <summary>
    ///     Resolves an organ carrying <typeparamref name="T"/> without traversing the body.
    /// </summary>
    public bool TryGetOrgan<T>(EntityUid body, out EntityUid organ)
        where T : IComponent
    {
        organ = default;
        foreach (var candidate in GetOrgans(body))
        {
            if (!HasComp<T>(candidate.Owner))
                continue;

            organ = candidate.Owner;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Enumerates organs attached to a part without scanning the body's other parts.
    /// </summary>
    public IEnumerable<Entity<OrganComponent>> GetPartOrgans(EntityUid part)
    {
        if (TryComp<BodyPartComponent>(part, out var partComponent) &&
            partComponent.Body is { } body &&
            TryComp<CMUMedicalBodyIndexComponent>(body, out var index) &&
            index.PartOrgans.TryGetValue(part, out var organs))
        {
            foreach (var organ in organs)
            {
                if (TryComp<OrganComponent>(organ, out var component))
                    yield return (organ, component);
            }

            yield break;
        }

        foreach (var (organ, component) in _body.GetPartOrgans(part, partComponent))
            yield return (organ, component);
    }

    /// <summary>
    ///     Enumerates every configured organ slot and its optional occupant in prototype order.
    /// </summary>
    public IEnumerable<CMUMedicalOrganSlotEntry> GetOrganSlots(EntityUid part)
    {
        if (TryComp<BodyPartComponent>(part, out var partComponent) &&
            partComponent.Body is { } body &&
            TryComp<CMUMedicalBodyIndexComponent>(body, out var index) &&
            index.PartOrganSlots.TryGetValue(part, out var slots))
        {
            foreach (var slot in slots)
                yield return slot;

            yield break;
        }

        if (partComponent is null)
            yield break;

        foreach (var slot in ReadOrganSlots(part, partComponent))
            yield return slot;
    }

    /// <summary>
    ///     Resolves the current occupant of a configured organ slot.
    /// </summary>
    public bool TryGetOrganInSlot(EntityUid part, string slotId, out EntityUid organ)
    {
        foreach (var slot in GetOrganSlots(part))
        {
            if (slot.SlotId != slotId || slot.Organ is not { } occupant)
                continue;

            organ = occupant;
            return true;
        }

        organ = default;
        return false;
    }

    /// <summary>
    ///     Resolves an organ's parent part in constant time on indexed bodies.
    /// </summary>
    public bool TryGetOrganPart(EntityUid organ, out EntityUid part)
    {
        part = default;
        if (TryComp<OrganComponent>(organ, out var organComponent) &&
            organComponent.Body is { } owningBody &&
            TryComp<CMUMedicalBodyIndexComponent>(owningBody, out var index))
        {
            return index.OrganParts.TryGetValue(organ, out part);
        }

        if (_body.GetParentPartOrNull(organ) is not { } fallbackPart)
            return false;

        part = fallbackPart;
        return true;
    }

    /// <summary>
    ///     Resolves both the owning body and parent part of an attached organ.
    /// </summary>
    public bool TryGetOrganOwner(EntityUid organ, out EntityUid body, out EntityUid part)
    {
        body = default;
        part = default;
        if (!TryComp<OrganComponent>(organ, out var organComponent) ||
            organComponent.Body is not { } owningBody ||
            !TryGetOrganPart(organ, out part))
        {
            return false;
        }

        body = owningBody;
        return true;
    }

    /// <summary>
    ///     Enumerates organs with a requested component in deterministic body order.
    /// </summary>
    public IEnumerable<Entity<T, OrganComponent>> GetOrgans<T>(EntityUid body)
        where T : IComponent
    {
        foreach (var organ in GetOrgans(body))
        {
            if (TryComp<T>(organ, out var component))
                yield return (organ.Owner, component, organ.Comp);
        }
    }

    /// <summary>
    ///     Enumerates body parts in the same deterministic order as <see cref="SharedBodySystem.GetBodyChildren"/>.
    ///     The server reads the structural index; clients fall back to the networked body hierarchy.
    /// </summary>
    public IEnumerable<Entity<BodyPartComponent>> GetBodyParts(EntityUid body)
    {
        if (TryComp<CMUMedicalBodyIndexComponent>(body, out var index))
        {
            foreach (var part in index.BodyPartOrder)
            {
                if (TryComp<BodyPartComponent>(part, out var component))
                    yield return (part, component);
            }

            yield break;
        }

        foreach (var part in _body.GetBodyChildren(body))
            yield return part;
    }

    /// <summary>
    ///     Enumerates organs in body traversal order using the server index or the client body hierarchy.
    /// </summary>
    public IEnumerable<Entity<OrganComponent>> GetOrgans(EntityUid body)
    {
        if (TryComp<CMUMedicalBodyIndexComponent>(body, out var index))
        {
            foreach (var organ in index.OrganOrder)
            {
                if (TryComp<OrganComponent>(organ, out var component))
                    yield return (organ, component);
            }

            yield break;
        }

        foreach (var organ in _body.GetBodyOrgans(body))
            yield return organ;
    }

    /// <summary>
    ///     Returns the cached snapshot for the current structural revision.
    ///     Repeated calls at the same revision return the same instance.
    /// </summary>
    public bool TryGetSnapshot(
        EntityUid body,
        [NotNullWhen(true)] out CMUMedicalSnapshot? snapshot)
    {
        snapshot = null;
        if (!TryComp<CMUMedicalBodyIndexComponent>(body, out var index) ||
            !TryComp<CMUMedicalAggregateComponent>(body, out var aggregate))
        {
            return false;
        }

        snapshot = aggregate.Snapshot ??= BuildSnapshot(index, aggregate);
        return true;
    }

    private void OnMedicalInit(Entity<CMUHumanMedicalComponent> ent, ref ComponentInit args)
    {
        if (_net.IsClient)
            return;

        var index = EnsureComp<CMUMedicalBodyIndexComponent>(ent.Owner);
        var aggregate = EnsureComp<CMUMedicalAggregateComponent>(ent.Owner);
        Invalidate(ent.Owner, aggregate, RebuildIndex(ent.Owner, index));
    }

    private void OnBodyPartAdded(Entity<CMUMedicalBodyIndexComponent> ent, ref BodyPartAddedEvent args)
    {
        if (!TryComp<CMUMedicalAggregateComponent>(ent, out var aggregate))
            return;

        Invalidate(ent.Owner, aggregate, RebuildIndex(ent.Owner, ent.Comp));
    }

    private void OnBodyPartRemoved(Entity<CMUMedicalBodyIndexComponent> ent, ref BodyPartRemovedEvent args)
    {
        if (!TryComp<CMUMedicalAggregateComponent>(ent, out var aggregate))
            return;

        Invalidate(ent.Owner, aggregate, RebuildIndex(ent.Owner, ent.Comp));
    }

    private void OnOrganAdded(Entity<OrganHealthComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (!TryGetMedicalState(args.Body, out var index, out var aggregate))
            return;

        Invalidate(args.Body, aggregate, ReindexPartOrgans(index, args.Part));
    }

    private void OnOrganRemoved(Entity<OrganHealthComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!TryGetMedicalState(args.OldBody, out var index, out var aggregate))
            return;

        Invalidate(args.OldBody, aggregate, ReindexPartOrgans(index, args.OldPart));
    }

    private CMUMedicalSnapshot BuildSnapshot(
        CMUMedicalBodyIndexComponent index,
        CMUMedicalAggregateComponent aggregate)
    {
        return new CMUMedicalSnapshot(
            aggregate.Revision,
            index.BodyParts,
            index.Organs,
            index.BodyPartOrder,
            index.OrganOrder);
    }

    private bool RebuildIndex(EntityUid body, CMUMedicalBodyIndexComponent index)
    {
        var rootPart = (EntityUid?) null;
        var bodyParts = new Dictionary<CMUMedicalBodyPartKey, EntityUid>();
        var organs = new HashSet<EntityUid>();
        var bodyPartOrder = new List<EntityUid>();
        var organOrder = new List<EntityUid>();
        var partChildSlots = new Dictionary<EntityUid, List<CMUMedicalBodyPartSlotEntry>>();
        var partOrgans = new Dictionary<EntityUid, List<EntityUid>>();
        var partOrganSlots = new Dictionary<EntityUid, List<CMUMedicalOrganSlotEntry>>();
        var organParts = new Dictionary<EntityUid, EntityUid>();

        foreach (var (part, component) in _body.GetBodyChildren(body))
        {
            rootPart ??= part;
            bodyPartOrder.Add(part);
            var key = new CMUMedicalBodyPartKey(component.PartType, component.Symmetry);
            bodyParts[key] = part;
            partChildSlots[part] = ReadBodyPartSlots(part, component);

            var attachedOrgans = new List<EntityUid>();
            var organSlots = ReadOrganSlots(part, component);
            foreach (var slot in organSlots)
            {
                if (slot.Organ is not { } organ)
                    continue;

                attachedOrgans.Add(organ);
                organOrder.Add(organ);
                organs.Add(organ);
                organParts[organ] = part;
            }

            partOrgans[part] = attachedOrgans;
            partOrganSlots[part] = organSlots;
        }

        if (index.RootPart == rootPart &&
            index.BodyPartOrder.SequenceEqual(bodyPartOrder) &&
            index.OrganOrder.SequenceEqual(organOrder) &&
            index.BodyParts.Count == bodyParts.Count &&
            index.BodyParts.All(entry => bodyParts.TryGetValue(entry.Key, out var value) && value == entry.Value) &&
            index.Organs.SetEquals(organs) &&
            index.OrganParts.Count == organParts.Count &&
            index.OrganParts.All(entry => organParts.TryGetValue(entry.Key, out var value) && value == entry.Value) &&
            BodyPartSlotMapsEqual(index.PartChildSlots, partChildSlots) &&
            PartOrganMapsEqual(index.PartOrgans, partOrgans) &&
            OrganSlotMapsEqual(index.PartOrganSlots, partOrganSlots))
        {
            return false;
        }

        index.RootPart = rootPart;
        Replace(index.BodyParts, bodyParts);
        Replace(index.Organs, organs);
        Replace(index.BodyPartOrder, bodyPartOrder);
        Replace(index.OrganOrder, organOrder);
        Replace(index.PartChildSlots, partChildSlots);
        Replace(index.PartOrgans, partOrgans);
        Replace(index.PartOrganSlots, partOrganSlots);
        Replace(index.OrganParts, organParts);
        return true;
    }

    private List<CMUMedicalBodyPartSlotEntry> ReadBodyPartSlots(EntityUid part, BodyPartComponent component)
    {
        var slots = new List<CMUMedicalBodyPartSlotEntry>(component.Children.Count);
        foreach (var (slotId, slot) in component.Children)
        {
            EntityUid? occupant = null;
            var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);
            if (_containers.TryGetContainer(part, containerId, out var container))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (!HasComp<BodyPartComponent>(contained))
                        continue;

                    occupant = contained;
                    break;
                }
            }

            slots.Add(new CMUMedicalBodyPartSlotEntry(slotId, slot.Type, occupant));
        }

        return slots;
    }

    private bool ReindexPartOrgans(CMUMedicalBodyIndexComponent index, EntityUid part)
    {
        if (!index.PartOrgans.TryGetValue(part, out var indexedOrgans))
            return false;

        if (!TryComp<BodyPartComponent>(part, out var partComponent) ||
            !index.PartOrganSlots.TryGetValue(part, out var indexedSlots))
        {
            return false;
        }

        var currentSlots = ReadOrganSlots(part, partComponent);
        var currentOrgans = currentSlots
            .Where(slot => slot.Organ is not null)
            .Select(slot => slot.Organ!.Value)
            .ToList();
        if (indexedOrgans.SequenceEqual(currentOrgans) && indexedSlots.SequenceEqual(currentSlots))
            return false;

        foreach (var organ in indexedOrgans)
        {
            index.Organs.Remove(organ);
            if (index.OrganParts.TryGetValue(organ, out var indexedPart) && indexedPart == part)
                index.OrganParts.Remove(organ);
        }

        indexedOrgans.Clear();
        indexedSlots.Clear();
        indexedSlots.AddRange(currentSlots);
        foreach (var organ in currentOrgans)
        {
            indexedOrgans.Add(organ);
            index.Organs.Add(organ);
            index.OrganParts[organ] = part;
        }

        index.OrganOrder.Clear();
        foreach (var bodyPart in index.BodyPartOrder)
        {
            if (index.PartOrgans.TryGetValue(bodyPart, out var partEntries))
                index.OrganOrder.AddRange(partEntries);
        }

        return true;
    }

    private List<CMUMedicalOrganSlotEntry> ReadOrganSlots(EntityUid part, BodyPartComponent component)
    {
        var slots = new List<CMUMedicalOrganSlotEntry>(component.Organs.Count);
        foreach (var slotId in component.Organs.Keys)
        {
            EntityUid? occupant = null;
            var containerId = SharedBodySystem.GetOrganContainerId(slotId);
            if (_containers.TryGetContainer(part, containerId, out var container))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (!HasComp<OrganComponent>(contained))
                        continue;

                    occupant = contained;
                    break;
                }
            }

            slots.Add(new CMUMedicalOrganSlotEntry(slotId, occupant));
        }

        return slots;
    }

    private static bool PartOrganMapsEqual(
        IReadOnlyDictionary<EntityUid, List<EntityUid>> left,
        IReadOnlyDictionary<EntityUid, List<EntityUid>> right)
    {
        return left.Count == right.Count &&
            left.All(entry => right.TryGetValue(entry.Key, out var value) && entry.Value.SequenceEqual(value));
    }

    private static bool BodyPartSlotMapsEqual(
        IReadOnlyDictionary<EntityUid, List<CMUMedicalBodyPartSlotEntry>> left,
        IReadOnlyDictionary<EntityUid, List<CMUMedicalBodyPartSlotEntry>> right)
    {
        return left.Count == right.Count &&
            left.All(entry => right.TryGetValue(entry.Key, out var value) && entry.Value.SequenceEqual(value));
    }

    private static bool OrganSlotMapsEqual(
        IReadOnlyDictionary<EntityUid, List<CMUMedicalOrganSlotEntry>> left,
        IReadOnlyDictionary<EntityUid, List<CMUMedicalOrganSlotEntry>> right)
    {
        return left.Count == right.Count &&
            left.All(entry => right.TryGetValue(entry.Key, out var value) && entry.Value.SequenceEqual(value));
    }

    private static void Replace<TKey, TValue>(Dictionary<TKey, TValue> destination, Dictionary<TKey, TValue> source)
        where TKey : notnull
    {
        destination.Clear();
        foreach (var entry in source)
            destination.Add(entry.Key, entry.Value);
    }

    private static void Replace<T>(HashSet<T> destination, HashSet<T> source)
    {
        destination.Clear();
        destination.UnionWith(source);
    }

    private static void Replace<T>(List<T> destination, List<T> source)
    {
        destination.Clear();
        destination.AddRange(source);
    }

    private bool TryGetMedicalState(
        EntityUid body,
        [NotNullWhen(true)] out CMUMedicalBodyIndexComponent? index,
        [NotNullWhen(true)] out CMUMedicalAggregateComponent? aggregate)
    {
        index = null;
        aggregate = null;
        if (_net.IsClient || !HasComp<CMUHumanMedicalComponent>(body))
            return false;

        index = EnsureComp<CMUMedicalBodyIndexComponent>(body);
        aggregate = EnsureComp<CMUMedicalAggregateComponent>(body);
        return true;
    }

    private void Invalidate(
        EntityUid body,
        CMUMedicalAggregateComponent aggregate,
        bool changed)
    {
        if (!changed)
            return;

        aggregate.Revision = unchecked(aggregate.Revision + 1);
        aggregate.Snapshot = null;
        _changes.MarkChanged(body, CMUMedicalChangeFlags.Anatomy);
    }
}
