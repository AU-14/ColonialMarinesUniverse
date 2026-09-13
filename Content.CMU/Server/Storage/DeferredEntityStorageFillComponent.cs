namespace Content.Server.Storage.Components;

/// <summary>Lets a bounded shipment builder populate a crate after its map initialization.</summary>
[RegisterComponent]
public sealed partial class DeferredEntityStorageFillComponent : Component
{
    public readonly List<string> Prototypes = new();
}
