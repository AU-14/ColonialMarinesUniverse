using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Automation;

[RegisterComponent]
public sealed partial class CMUInserterVisualsComponent : Component
{
    public readonly List<SpriteComponent.Layer> Layers = new();
    public readonly List<SpriteComponent.Layer> Cables = new();
}
