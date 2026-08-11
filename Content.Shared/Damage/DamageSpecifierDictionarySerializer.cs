using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.Damage;

/// <summary>
/// Reads damage specified as explicit types or damage groups.
/// </summary>
public sealed class DamageSpecifierDictionarySerializer :
    ITypeReader<Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>, MappingDataNode>
{
    private readonly DictionarySerializer<ProtoId<DamageGroupPrototype>, FixedPoint2> _damageGroupSerializer = new();
    private readonly DictionarySerializer<ProtoId<DamageTypePrototype>, FixedPoint2> _damageTypeSerializer = new();

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        var values = new Dictionary<ValidationNode, ValidationNode>();

        if (node.TryGet<MappingDataNode>("types", out var typesNode))
        {
            values.Add(
                new ValidatedValueNode(new ValueDataNode("types")),
                _damageTypeSerializer.Validate(serializationManager, typesNode, dependencies, context));
        }

        if (node.TryGet<MappingDataNode>("groups", out var groupsNode))
        {
            values.Add(
                new ValidatedValueNode(new ValueDataNode("groups")),
                _damageGroupSerializer.Validate(serializationManager, groupsNode, dependencies, context));
        }

        return new ValidatedMappingNode(values);
    }

    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>>? instanceProvider = null)
    {
        var damage = instanceProvider?.Invoke() ?? new();

        if (node.TryGet<MappingDataNode>("types", out var typesNode))
        {
            var types = serializationManager.Read<Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>>(
                typesNode,
                hookCtx,
                context,
                notNullableOverride: true);

            foreach (var (type, amount) in types)
            {
                damage[type] = amount;
            }
        }

        if (!node.TryGet<MappingDataNode>("groups", out var groupsNode))
            return damage;

        var groups = serializationManager.Read<Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2>>(
            groupsNode,
            hookCtx,
            context,
            notNullableOverride: true);
        var prototypes = dependencies.Resolve<IPrototypeManager>();

        foreach (var (groupId, amount) in groups)
        {
            if (!prototypes.TryIndex(groupId, out var group))
            {
                dependencies.Resolve<ILogManager>()
                    .RootSawmill.Error($"Unknown damage group given to DamageSpecifier: {groupId}");
                continue;
            }

            var remainingTypes = group.DamageTypes.Count;
            var remainingDamage = amount;
            foreach (var damageType in group.DamageTypes)
            {
                var distributedDamage = remainingDamage / FixedPoint2.New(remainingTypes);
                damage[damageType] = damage.GetValueOrDefault(damageType) + distributedDamage;
                remainingDamage -= distributedDamage;
                remainingTypes--;
            }
        }

        return damage;
    }
}
