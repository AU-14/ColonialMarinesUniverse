using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.NPC.Prototypes;

/// <summary>
/// Contains data about this faction's relations with other factions.
/// </summary>
[Prototype]
public sealed partial class NpcFactionPrototype : IPrototype, IInheritingPrototype // CMU14 Class
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<NpcFactionPrototype>))]
    public string[]? Parents { get; private set; } // CMU14: inheritance (e.g. THREAT -> RMCXeno)

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; } // CMU14

    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public string? Name { get; private set; }

    [DataField]
    public List<ProtoId<NpcFactionPrototype>> Friendly = new();

    [DataField]
    public List<ProtoId<NpcFactionPrototype>> Hostile = new();

    [DataField]
    public bool AllianceTarget; // CMU14: standing settable on alliance consoles

    [DataField]
    public bool SentryProtected; // CMU14: never a valid sentry/turret target (e.g. Provost Office)

    [DataField]
    public bool Hidden; // CMU14: never shown on ID examination (e.g. CLF insurgents must stay covert)
}

/// <summary>
/// Cached data for the faction prototype. Is modified at runtime, whereas the prototype is not.
/// </summary>
[Serializable, NetSerializable]
public record struct FactionData
{
    [ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>> Friendly;

    [ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>> Hostile;
}
