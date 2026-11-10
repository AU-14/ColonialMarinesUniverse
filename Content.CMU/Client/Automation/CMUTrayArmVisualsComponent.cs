using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Automation;

/// <summary>CMU14 (ECS-04): the client's layers for a tray arm's rig (see <see cref="CMUTrayArmVisualsSystem"/>).</summary>
[RegisterComponent]
public sealed partial class CMUTrayArmVisualsComponent : Component
{
    public readonly List<SpriteComponent.Layer> Layers = new();
    public readonly List<SpriteComponent.Layer> Cables = new();

    /// <summary>The telescoping rod between the elbow and the pushed-out forearm.</summary>
    public SpriteComponent.Layer? Rod;

    /// <summary>The icon of what the claw carries.</summary>
    public SpriteComponent.Layer? Item;

    /// <summary>The prototype whose icon <see cref="Item"/> currently shows.</summary>
    public EntProtoId? ItemShown;
}
