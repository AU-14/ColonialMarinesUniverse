using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Movement.Systems;

public sealed partial class FloorOcclusionSystem : SharedFloorOcclusionSystem
{
    private static readonly ProtoId<ShaderPrototype> HorizontalCut = "HorizontalCut";
    private const string ShaderId = "HorizontalCut"; // CMU14: keyed id required by the v288 multi post-shader API

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!; // CMU14

    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<FloorOcclusionComponent, ComponentStartup>(OnOcclusionStartup);
        SubscribeLocalEvent<FloorOcclusionComponent, ComponentShutdown>(OnOcclusionShutdown);
        SubscribeLocalEvent<FloorOcclusionComponent, AfterAutoHandleStateEvent>(OnOcclusionAuto);
    }

    private void OnOcclusionAuto(Entity<FloorOcclusionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SetShader(ent.Owner, ent.Comp.Enabled);
    }

    private void OnOcclusionStartup(Entity<FloorOcclusionComponent> ent, ref ComponentStartup args)
    {
        SetShader(ent.Owner, ent.Comp.Enabled);
    }

    private void OnOcclusionShutdown(Entity<FloorOcclusionComponent> ent, ref ComponentShutdown args)
    {
        SetShader(ent.Owner, false);
    }

    protected override void SetEnabled(Entity<FloorOcclusionComponent> entity)
    {
        SetShader(entity.Owner, entity.Comp.Enabled);
    }

    private void SetShader(Entity<SpriteComponent?> sprite, bool enabled)
    {
        if (!_spriteQuery.Resolve(sprite.Owner, ref sprite.Comp, false))
            return;

        // CMU14: obsolete PostShader property clears every keyed entry on the sprite (thermal cloaks, holograms);
        // the guard below only made sense for the single-slot API it was protecting
        // var shader = _proto.Index(HorizontalCut).Instance();
        // if (sprite.Comp.PostShader is not null && sprite.Comp.PostShader != shader)
        //     return;

        if (enabled)
        {
            _sprite.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(ShaderId, _proto.Index(HorizontalCut).InstanceUnique())); // CMU14
        }
        else
        {
            _sprite.RemovePostShader(sprite, ShaderId); // CMU14
        }
    }
}
