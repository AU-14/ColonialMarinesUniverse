using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Visuals;

/// <summary>
/// Displays the prototype icons of items held in a gun rack's item-slot containers.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUGunRackVisualizerComponent : Component
{
    [DataField(required: true)]
    public List<string> Slots = new();

    [DataField(required: true)]
    public List<Vector2> Offsets = new();

    [DataField]
    public Vector2 Scale = Vector2.One;
}

public enum CMUGunRackVisualLayers : byte
{
    Gun1,
    Gun2,
}
