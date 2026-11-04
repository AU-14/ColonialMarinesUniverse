using Content.Shared._RMC14.Xenonids.Invisibility;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Xenonids.Invisibility;

public sealed partial class XenoInvisibilityVisualsSystem : EntitySystem // CMU14 Class
{
    private static readonly ProtoId<ShaderPrototype> InvisibilityShader = "RMCInvisible";
    private const string ShaderId = "RMCInvisible";

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private EntityQuery<XenoActiveInvisibleComponent> _activeInvisibleQuery;

    public override void Initialize()
    {
        _activeInvisibleQuery = GetEntityQuery<XenoActiveInvisibleComponent>();

        SubscribeLocalEvent<XenoTurnInvisibleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<XenoTurnInvisibleComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent)
                || !TryComp(ent, out SpriteComponent? sprite))
            return;

        _sprite.RemovePostShader(sprite, ShaderId);
    }

    public override void Update(float frameTime)
    {
        var invisible = EntityQueryEnumerator<XenoTurnInvisibleComponent, SpriteComponent>();
        while (invisible.MoveNext(out var uid, out var comp, out var sprite))
        {
            var opacity = _activeInvisibleQuery.HasComp(uid) ? comp.Opacity : 1;

            if (!_sprite.TryGetPostShader(sprite, ShaderId, out var entry))
            {
                if (opacity >= 1)
                    continue;

                _sprite.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(ShaderId, _prototypes.Index(InvisibilityShader).InstanceUnique()));
                continue;
            }

            if (opacity >= 1)
            {
                _sprite.RemovePostShader(sprite, ShaderId);
                continue;
            }

            entry.Shader.SetParameter("visibility", opacity);
        }
    }
}
