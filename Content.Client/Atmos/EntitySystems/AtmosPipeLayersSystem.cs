using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;

namespace Content.Client.Atmos.EntitySystems;

/// <summary>
/// The system responsible for updating the appearance of layered gas pipe
/// </summary>
public sealed partial class AtmosPipeLayersSystem : SharedAtmosPipeLayersSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IReflectionManager _reflection = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AtmosPipeLayersComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<AtmosPipeLayersComponent> ent, ref AppearanceChangeEvent ev)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (_appearance.TryGetData<string>(ent, AtmosPipeLayerVisuals.Sprite, out var spriteRsi) &&
            _resourceCache.TryGetResource(SpriteSpecifierSerializer.TextureRoot / spriteRsi, out RSIResource? resource))
        {
            _sprite.SetBaseRsi((ent, sprite), resource.RSI);
        }

        if (_appearance.TryGetData<Dictionary<string, string>>(ent, AtmosPipeLayerVisuals.SpriteLayers, out var pipeState))
        {
            foreach (var (layerKey, rsiPath) in pipeState)
            {
                TryParseKey(layerKey, out var @enum);
                var hasLayer = @enum != null
                    ? _sprite.LayerMapTryGet((ent, sprite), @enum, out var layerIndex, false)
                    : _sprite.LayerMapTryGet((ent, sprite), layerKey, out layerIndex, false);
                if (!hasLayer)
                    continue;

                // Some map-specific pipe wrappers replace the parent RSI while
                // retaining its appearance component. Keep the wrapper's
                // sprite when the currently selected state does not exist in
                // the replacement RSI instead of logging a client error.
                var path = new ResPath(rsiPath);
                var state = _sprite.LayerGetRsiState((ent, sprite), layerIndex);
                if (state.IsValid
                    && _resourceCache.TryGetResource(SpriteSpecifierSerializer.TextureRoot / path, out RSIResource? rsi)
                    && !rsi.RSI.TryGetState(state, out _))
                {
                    continue;
                }

                _sprite.LayerSetRsi((ent, sprite), layerIndex, path);
            }
        }
    }

    private bool TryParseKey(string keyString, [NotNullWhen(true)] out Enum? @enum)
    {
        return _reflection.TryParseEnumReference(keyString, out @enum);
    }
}
