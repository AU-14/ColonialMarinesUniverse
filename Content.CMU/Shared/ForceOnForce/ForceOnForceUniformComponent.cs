using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ForceOnForce;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ForceOnForceUniformComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntProtoId> Uniforms = new();
}

/// <summary>Uniforms recognized for a platoon, independently of which side it rolled.</summary>
[Prototype]
public sealed partial class ForceOnForceUniformPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public HashSet<EntProtoId> Uniforms = new();
}
