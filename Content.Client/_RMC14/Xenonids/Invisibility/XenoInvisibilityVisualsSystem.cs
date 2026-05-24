using Content.Shared._RMC14.Xenonids.Invisibility;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Xenonids.Invisibility;

public sealed partial class XenoInvisibilityVisualsSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> InvisibilityShader = "RMCInvisible";

    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();
    private EntityQuery<XenoActiveInvisibleComponent> _activeInvisibleQuery;

    public override void Initialize()
    {
        _activeInvisibleQuery = GetEntityQuery<XenoActiveInvisibleComponent>();

        SubscribeLocalEvent<XenoTurnInvisibleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<XenoTurnInvisibleComponent> ent, ref ComponentShutdown args)
    {
        if (!_shaders.Remove(ent, out var shader) ||
            TerminatingOrDeleted(ent))
            return;

        if (TryComp(ent, out SpriteComponent? sprite) &&
            ReferenceEquals(sprite.PostShader, shader))
        {
            sprite.PostShader = null;
        }
    }

    public override void Update(float frameTime)
    {
        var invisible = EntityQueryEnumerator<XenoTurnInvisibleComponent, SpriteComponent>();
        while (invisible.MoveNext(out var uid, out var comp, out var sprite))
        {
            var opacity = _activeInvisibleQuery.HasComp(uid) ? comp.Opacity : 1;

            if (opacity < 1)
            {
                if (!_shaders.TryGetValue(uid, out var shader))
                    _shaders[uid] = shader = _prototypes.Index(InvisibilityShader).InstanceUnique();

                sprite.PostShader = shader;
                shader.SetParameter("visibility", opacity);
            }
            else if (_shaders.Remove(uid, out var shader) &&
                     ReferenceEquals(sprite.PostShader, shader))
            {
                sprite.PostShader = null;
            }
        }
    }
}
