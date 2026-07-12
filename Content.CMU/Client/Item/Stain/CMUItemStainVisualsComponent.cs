namespace Content.Client._CMU14.Item.Stain;

/// <summary>
/// Tracks client-only helper layers owned by the item stain visualizer.
/// </summary>
[RegisterComponent]
public sealed partial class CMUItemStainVisualsComponent : Component
{
    public readonly List<string> LayerKeys = new();
}
