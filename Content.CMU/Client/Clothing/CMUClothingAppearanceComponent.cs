using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Clothing;

/// <summary>
/// Inputs and owned layers from the last full clothing appearance refresh.
/// Item visual events still refresh their individual slots independently.
/// </summary>
[RegisterComponent]
public sealed partial class CMUClothingAppearanceComponent : Component
{
    public SpriteComponent? Sprite;
    public Sex? Sex;
    public string? Species;
    public Dictionary<string, DisplacementData>? Displacements;
    public Dictionary<string, DisplacementData>? MaleDisplacements;
    public Dictionary<string, DisplacementData>? FemaleDisplacements;
    public readonly Dictionary<string, SpriteComponent.Layer> Layers = new();
}
