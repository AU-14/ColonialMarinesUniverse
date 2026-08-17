using System.Numerics;
using Content.Shared._RMC14.Sprite;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Light;

public sealed partial class RMCLightOffsetSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;

    private static readonly Vector2 OffsetLightWallFace = new(0f, -0.495f);
    private readonly HashSet<EntityUid> ToUpdate = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCLightOffsetComponent, ComponentStartup>(OnLightStartup);
        SubscribeLocalEvent<RMCLightOffsetComponent, MapInitEvent>(OnLightUpdate);
        SubscribeLocalEvent<RMCLightOffsetComponent, EntParentChangedMessage>(OnLightUpdate);
    }

    private void OnLightStartup(Entity<RMCLightOffsetComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            OffsetLight(ent);
    }

    private void OnLightUpdate<T>(Entity<RMCLightOffsetComponent> ent, ref T args)
    {
        if (!TryComp(ent, out MetaDataComponent? metaData) ||
            metaData.EntityLifeStage < EntityLifeStage.MapInitialized)
        {
            return;
        }

        ToUpdate.Add(ent);

        if (_net.IsClient)
            return;

        if (TerminatingOrDeleted(ent))
            return;

        OffsetLight(ent);
    }

    private void OffsetLight(Entity<RMCLightOffsetComponent> ent)
    {
        var sprite = EnsureComp<SpriteSetRenderOrderComponent>(ent);
        ApplyPointLightOffset(ent);

        Dirty(ent, sprite);
    }

    private void ApplyPointLightOffset(EntityUid uid)
    {
        if (!_pointLight.TryGetLight(uid, out var light))
            return;

        if (light.Offset == OffsetLightWallFace)
            return;

        light.Offset = OffsetLightWallFace;
        Dirty(uid, light);
    }
}
