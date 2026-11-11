using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Item.Stain;

/// <summary>
/// Tracks client-only helper layers owned by the item stain visualizer.
/// </summary>
[RegisterComponent]
public sealed partial class CMUItemStainVisualsComponent : Component
{
    public readonly List<string> LayerKeys = new();
    public readonly List<SpriteComponent.Layer> SourceBuffer = new();
    public readonly HashSet<SpriteComponent.Layer> OwnedLayers = new();
    public readonly List<int> RemovalIndices = new();
    public readonly List<StainLayer> Layers = new();
    public Color? Color;

    public sealed record StainLayer(SpriteComponent.Layer Source, SpriteComponent.Layer Overlay,
        SpriteComponent.Layer Mask, string OverlayKey, string MaskKey);
}
