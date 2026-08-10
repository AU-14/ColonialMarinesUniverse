#nullable enable

using Content.Shared.CMU.Round;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.CMU.Round;

/// <summary>
/// Applies director-resolved vendor presentation without exposing legacy vendor prototypes to clients.
/// </summary>
public sealed partial class RoundVendorVisualizerSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundSetupEndpointComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<RoundSetupEndpointComponent> endpoint, ref AppearanceChangeEvent args)
    {
        if (!TryComp<SpriteComponent>(endpoint, out var sprite) ||
            !_appearance.TryGetData<string>(endpoint, RoundVendorVisuals.BaseRsi, out var spriteRsi, args.Component) ||
            !_resourceCache.TryGetResource(
                SpriteSpecifierSerializer.TextureRoot / spriteRsi,
                out RSIResource? resource))
        {
            return;
        }

        _sprites.SetBaseRsi((endpoint, sprite), resource.RSI);
    }
}
