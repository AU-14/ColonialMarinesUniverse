namespace Content.Client.CMU14.Dropship.TacticalLand;

/// <summary>Client-owned lamps disappear with their approach projection.</summary>
[RegisterComponent]
public sealed partial class MohawkApproachVisualsComponent : Component
{
    public Box2 Bounds;
    public readonly List<EntityUid> Lights = new();
}
