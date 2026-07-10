using System.Collections.Frozen;
using System.Collections.Immutable;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared.Body.Part;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

/// <summary>
///     Immutable surgery data compiled from entity and CMU metadata
///     prototypes. Runtime flow code uses this instead of rescanning
///     prototype singleton components.
/// </summary>
public sealed class CMUSurgeryDefinition
{
    public EntProtoId<CMSurgeryComponent> Id { get; }
    public EntityPrototype Prototype { get; }
    public int Priority { get; }
    public EntProtoId<CMSurgeryComponent>? Requirement { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public int MinSkill { get; }
    public bool AllowSelfSurgery { get; }
    public FrozenSet<BodyPartType> ValidParts { get; }
    public FrozenSet<BodyPartType> SelfSurgeryValidParts { get; }
    public ImmutableArray<CMUSurgeryStepDefinition> Steps { get; }
    public FrozenDictionary<EntProtoId<CMSurgeryStepComponent>, CMUSurgeryStepDefinition> StepsById { get; }
    internal CMUSurgeryStepMetadataPrototype? Metadata { get; }

    internal CMUSurgeryDefinition(
        EntProtoId<CMSurgeryComponent> id,
        EntityPrototype prototype,
        int priority,
        EntProtoId<CMSurgeryComponent>? requirement,
        string displayName,
        string category,
        int minSkill,
        bool allowSelfSurgery,
        FrozenSet<BodyPartType> validParts,
        FrozenSet<BodyPartType> selfSurgeryValidParts,
        ImmutableArray<CMUSurgeryStepDefinition> steps,
        FrozenDictionary<EntProtoId<CMSurgeryStepComponent>, CMUSurgeryStepDefinition> stepsById,
        CMUSurgeryStepMetadataPrototype? metadata)
    {
        Id = id;
        Prototype = prototype;
        Priority = priority;
        Requirement = requirement;
        DisplayName = displayName;
        Category = category;
        MinSkill = minSkill;
        AllowSelfSurgery = allowSelfSurgery;
        ValidParts = validParts;
        SelfSurgeryValidParts = selfSurgeryValidParts;
        Steps = steps;
        StepsById = stepsById;
        Metadata = metadata;
    }

    /// <summary>
    ///     Resolves a step by its compiled execution index without consulting prototypes.
    /// </summary>
    public bool TryGetStepAt(int index, out CMUSurgeryStepDefinition step)
    {
        if ((uint) index >= (uint) Steps.Length)
        {
            step = default!;
            return false;
        }

        step = Steps[index];
        return true;
    }

    /// <summary>
    ///     Resolves a step by its stable prototype identifier.
    /// </summary>
    public bool TryGetStep(
        EntProtoId<CMSurgeryStepComponent> stepId,
        out CMUSurgeryStepDefinition step)
    {
        return StepsById.TryGetValue(stepId, out step!);
    }
}

public sealed record CMUSurgeryStepDefinition(
    EntProtoId<CMSurgeryStepComponent> Id,
    int Index,
    string Label,
    string? ToolCategory,
    CMUSurgeryOrganCondition? OrganCondition,
    string? ReinsertOrganSlot);

public readonly record struct CMUSurgeryOrganCondition(string OrganSlot, OrganDamageStage MinStage);

internal sealed class CMUSurgeryRegistry
{
    public static readonly CMUSurgeryRegistry Empty = new(
        FrozenDictionary<EntProtoId<CMSurgeryComponent>, CMUSurgeryDefinition>.Empty,
        ImmutableArray<CMUSurgeryDefinition>.Empty,
        FrozenDictionary<BodyPartType, ImmutableArray<CMUSurgeryDefinition>>.Empty);

    private readonly FrozenDictionary<EntProtoId<CMSurgeryComponent>, CMUSurgeryDefinition> _definitions;
    private readonly FrozenDictionary<BodyPartType, ImmutableArray<CMUSurgeryDefinition>> _eligibleByPart;

    public ImmutableArray<CMUSurgeryDefinition> MetadataDefinitions { get; }

    public CMUSurgeryRegistry(
        FrozenDictionary<EntProtoId<CMSurgeryComponent>, CMUSurgeryDefinition> definitions,
        ImmutableArray<CMUSurgeryDefinition> metadataDefinitions,
        FrozenDictionary<BodyPartType, ImmutableArray<CMUSurgeryDefinition>> eligibleByPart)
    {
        _definitions = definitions;
        MetadataDefinitions = metadataDefinitions;
        _eligibleByPart = eligibleByPart;
    }

    public bool TryGetDefinition(string surgeryId, out CMUSurgeryDefinition definition)
    {
        return _definitions.TryGetValue(new EntProtoId<CMSurgeryComponent>(surgeryId), out definition!);
    }

    public bool ContainsStep(string stepId)
    {
        var typedStep = new EntProtoId<CMSurgeryStepComponent>(stepId);
        foreach (var definition in _definitions.Values)
        {
            if (definition.StepsById.ContainsKey(typedStep))
                return true;
        }

        return false;
    }

    public ImmutableArray<CMUSurgeryDefinition> GetEligibleDefinitions(BodyPartType partType)
    {
        return _eligibleByPart.GetValueOrDefault(partType, ImmutableArray<CMUSurgeryDefinition>.Empty);
    }
}
