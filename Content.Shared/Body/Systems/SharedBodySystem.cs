using System.Numerics;
using System.Linq;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Gibbing;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Body.Systems;

/// <summary>
/// Compatibility facade for fork systems that still consume the legacy body-part API.
/// Nubody's flat organ container and organ relationships remain authoritative.
/// </summary>
public sealed partial class SharedBodySystem : EntitySystem
{
    public const string PartSlotContainerIdPrefix = "body_part_slot_";
    public const string BodyRootContainerId = "body_root_part";
    public const string OrganSlotContainerIdPrefix = "body_organ_slot_";

    private const float GibletLaunchImpulse = 8f;
    private const float GibletLaunchImpulseVariance = 3f;

    private static readonly ProtoId<DamageTypePrototype> BloodlossDamageType = "Bloodloss";

    [Dependency] private OrganRelationSystem _relations = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartComponent, OrganGotInsertedEvent>(OnBodyPartInserted);
        SubscribeLocalEvent<BodyPartComponent, OrganGotRemovedEvent>(OnBodyPartRemoved);
        SubscribeLocalEvent<OrganComponent, OrganGotInsertedEvent>(OnOrganInserted);
        SubscribeLocalEvent<OrganComponent, OrganGotRemovedEvent>(OnOrganRemoved);
        SubscribeLocalEvent<OrganComponent, OrganRelatedEvent>(OnOrganRelated);
        SubscribeLocalEvent<OrganComponent, OrganOrphanedEvent>(OnOrganOrphaned);
    }

    private void OnBodyPartInserted(Entity<BodyPartComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (!TryComp<OrganComponent>(ent, out var organ) || GetBodyPartEventSlot(organ) is not { } slot)
            return;

        ent.Comp.Body = args.Target;
        Dirty(ent);

        var ev = new BodyPartAddedEvent(slot, ent);
        RaiseLocalEvent(args.Target, ref ev);

        AddLeg(ent, args.Target);
    }

    private void OnBodyPartRemoved(Entity<BodyPartComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (!TryComp<OrganComponent>(ent, out var organ) || GetBodyPartEventSlot(organ) is not { } slot)
            return;

        var ev = new BodyPartRemovedEvent(slot, ent);
        RaiseLocalEvent(args.Target, ref ev);

        RemoveLeg(ent, args.Target);
        ApplyVitalPartRemovalDamage(args.Target, ent);

        ent.Comp.Body = null;
        Dirty(ent);
    }

    private void OnOrganInserted(Entity<OrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (HasComp<BodyPartComponent>(ent) || GetParentPartOrNull(ent) is not { } parent)
            return;

        RaiseOrganAdded(ent, parent, args.Target);
    }

    private void OnOrganRemoved(Entity<OrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (HasComp<BodyPartComponent>(ent) || GetParentPartOrNull(ent) is not { } parent)
            return;

        RaiseOrganRemoved(ent, parent, args.Target);
    }

    private void OnOrganRelated(Entity<OrganComponent> ent, ref OrganRelatedEvent args)
    {
        if (HasComp<BodyPartComponent>(ent))
            return;

        RaiseOrganAdded(ent, args.Parent, ent.Comp.Body);
    }

    private void OnOrganOrphaned(Entity<OrganComponent> ent, ref OrganOrphanedEvent args)
    {
        if (HasComp<BodyPartComponent>(ent))
            return;

        RaiseOrganRemoved(ent, args.OldParent, ent.Comp.Body);
    }

    private void RaiseOrganAdded(Entity<OrganComponent> ent, EntityUid parent, EntityUid? body)
    {
        var added = new OrganAddedEvent(parent);
        RaiseLocalEvent(ent, ref added);

        if (body is not { } bodyUid)
            return;

        var addedToBody = new OrganAddedToBodyEvent(bodyUid, parent);
        RaiseLocalEvent(ent, ref addedToBody);
    }

    private void RaiseOrganRemoved(Entity<OrganComponent> ent, EntityUid parent, EntityUid? body)
    {
        var removed = new OrganRemovedEvent(parent);
        RaiseLocalEvent(ent, ref removed);

        if (body is not { } bodyUid)
            return;

        var removedFromBody = new OrganRemovedFromBodyEvent(bodyUid, parent);
        RaiseLocalEvent(ent, ref removedFromBody);
    }

    private void AddLeg(Entity<BodyPartComponent> leg, Entity<BodyComponent?> body)
    {
        if (leg.Comp.PartType != BodyPartType.Leg || !Resolve(body, ref body.Comp, false))
            return;

        if (body.Comp.LegEntities.Add(leg))
        {
            Dirty(body);
            UpdateMovementSpeed(body, body.Comp);
        }
    }

    private void RemoveLeg(Entity<BodyPartComponent> leg, Entity<BodyComponent?> body)
    {
        if (leg.Comp.PartType != BodyPartType.Leg ||
            TerminatingOrDeleted(body.Owner) ||
            !Resolve(body, ref body.Comp, false))
            return;

        if (!body.Comp.LegEntities.Remove(leg))
            return;

        Dirty(body);
        UpdateMovementSpeed(body, body.Comp);

        if (body.Comp.LegEntities.Count == 0)
            _standing.Down(body);
    }

    private void ApplyVitalPartRemovalDamage(Entity<BodyComponent?> body, Entity<BodyPartComponent> part)
    {
        if (_timing.ApplyingState ||
            TerminatingOrDeleted(body.Owner) ||
            !part.Comp.IsVital ||
            !Resolve(body, ref body.Comp, false))
            return;

        if (GetBodyChildrenOfType(body, part.Comp.PartType, body.Comp).Any())
            return;

        var damage = new DamageSpecifier(_prototypes.Index(BloodlossDamageType), 300);
        _damageable.TryChangeDamage(body, damage);
    }

    public void UpdateMovementSpeed(
        EntityUid bodyId,
        BodyComponent? body = null,
        MovementSpeedModifierComponent? movement = null)
    {
        if (!Resolve(bodyId, ref body, ref movement, false) || body.RequiredLegs <= 0)
            return;

        var walkSpeed = 0f;
        var sprintSpeed = 0f;
        var acceleration = 0f;
        foreach (var leg in body.LegEntities)
        {
            if (!TryComp<MovementBodyPartComponent>(leg, out var modifier))
                continue;

            walkSpeed += modifier.WalkSpeed;
            sprintSpeed += modifier.SprintSpeed;
            acceleration += modifier.Acceleration;
        }

        walkSpeed /= body.RequiredLegs;
        sprintSpeed /= body.RequiredLegs;
        acceleration /= body.RequiredLegs;
        _movement.ChangeBaseSpeed(bodyId, walkSpeed, sprintSpeed, acceleration, movement);
    }

    public HashSet<EntityUid> GibBody(
        EntityUid bodyId,
        bool gibOrgans = false,
        BodyComponent? body = null,
        bool launchGibs = true,
        Vector2? splatDirection = null,
        float splatModifier = 1,
        Angle splatCone = default,
        SoundSpecifier? gibSoundOverride = null)
    {
        if (!Resolve(bodyId, ref body, false))
            return new();

        var bodyParts = GetBodyChildren(bodyId, body).Select(part => part.Id).ToArray();
        var giblets = _gibbing.Gib(bodyId, dropGiblets: false);
        giblets.UnionWith(bodyParts);

        foreach (var giblet in giblets.ToArray())
        {
            if (!gibOrgans && HasComp<OrganComponent>(giblet) && !HasComp<BodyPartComponent>(giblet))
            {
                giblets.Remove(giblet);
                continue;
            }

            _transform.DropNextTo(giblet, bodyId);
            if (!launchGibs || !TryComp<PhysicsComponent>(giblet, out _))
                continue;

            var direction = splatDirection?.ToAngle() ?? _random.NextAngle();
            if (splatDirection is not null && splatCone != default)
                direction = _random.NextAngle(direction - splatCone / 2, direction + splatCone / 2);

            var impulse = GibletLaunchImpulse * splatModifier + _random.NextFloat(GibletLaunchImpulseVariance);
            _physics.ApplyLinearImpulse(giblet, direction.ToVec() * impulse);
        }

        return giblets;
    }

    public static string GetPartSlotContainerId(string slotId)
    {
        return PartSlotContainerIdPrefix + slotId;
    }

    public static string GetOrganContainerId(string slotId)
    {
        return OrganSlotContainerIdPrefix + slotId;
    }

    public static string? GetCanonicalSlotId(ProtoId<OrganCategoryPrototype>? category)
    {
        return category?.Id switch
        {
            "Torso" => BodyRootContainerId,
            "Head" => "head",
            "ArmLeft" => "left_arm",
            "ArmRight" => "right_arm",
            "HandLeft" => "left_hand",
            "HandRight" => "right_hand",
            "LegLeft" => "left_leg",
            "LegRight" => "right_leg",
            "FootLeft" => "left_foot",
            "FootRight" => "right_foot",
            "Hands" => "hands",
            "Legs" => "legs",
            "Feet" => "feet",
            "Brain" => "brain",
            "Eyes" => "eyes",
            "Tongue" => "tongue",
            "Appendix" => "appendix",
            "Ears" => "ears",
            "Lungs" => "lungs",
            "Heart" => "heart",
            "Stomach" => "stomach",
            "Liver" => "liver",
            "Kidneys" => "kidneys",
            _ => null,
        };
    }

    private static string? GetBodyPartEventSlot(OrganComponent organ)
    {
        var slot = GetCanonicalSlotId(organ.Category);
        if (slot is null || slot == BodyRootContainerId)
            return slot;

        return GetPartSlotContainerId(slot);
    }
}
