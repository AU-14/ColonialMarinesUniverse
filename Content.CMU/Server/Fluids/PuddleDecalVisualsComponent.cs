using System.Numerics;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Decals;
using Robust.Shared.Prototypes;

namespace Content.Server.Fluids.Components;

[RegisterComponent, Access(typeof(PuddleSystem))]
public sealed partial class PuddleDecalVisualsComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<DecalPrototype>> Decals = new();

    [DataField]
    public Vector2 Offset = new(-0.5f, -0.5f);

    [DataField]
    public int ZIndex;

    [DataField]
    public bool Cleanable = true;

    [DataField]
    public bool RandomRotation = true;

    [ViewVariables]
    public uint? DecalId;

    [ViewVariables]
    public EntityUid? GridUid;
}
