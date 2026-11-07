using Content.Client.Body;
using Content.Shared.CMU14.Threats.Mobs.Threat;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Threats.Mobs.Threat;

public sealed class CMUThreatEyesVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUThreatComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUThreatComponent, VisualBodySpriteRefreshEvent>(OnVisualRefresh);
    }

    private void OnStartup(Entity<CMUThreatComponent> ent, ref ComponentStartup args)
    {
        UnshadeEyes(ent);
    }

    private void OnVisualRefresh(Entity<CMUThreatComponent> ent, ref VisualBodySpriteRefreshEvent args)
    {
        UnshadeEyes(ent);
    }

    private void UnshadeEyes(EntityUid uid)
    {
        if (!TryComp(uid, out SpriteComponent? sprite) ||
            !_sprites.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.Eyes, out var layer, true))
            return;

        if (((SpriteComponent.Layer) sprite[layer]).ShaderPrototype == SpriteSystem.UnshadedId)
            return;

        sprite.LayerSetShader(layer, "unshaded");
    }
}
