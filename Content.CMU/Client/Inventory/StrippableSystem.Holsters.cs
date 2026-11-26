using Content.Shared._RMC14.Inventory;

namespace Content.Client.Strip;

public sealed partial class StrippableSystem
{
    private void InitializeHolsterVisibility()
    {
        // Attaching or removing shoulder webbing changes a worn uniform without re-equipping it.
        SubscribeLocalEvent<CMHolsterComponent, ComponentStartup>(OnHolsterChanged);
        SubscribeLocalEvent<CMHolsterComponent, ComponentShutdown>(OnHolsterChanged);
    }

    private void OnHolsterChanged(EntityUid item, CMHolsterComponent holster, EntityEventArgs args)
    {
        if (TryComp(item, out TransformComponent? transform))
            UpdateUi(transform.ParentUid);
    }
}
