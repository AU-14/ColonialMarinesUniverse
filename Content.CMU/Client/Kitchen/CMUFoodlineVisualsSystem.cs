using Content.Client.CMU14.Vendors;
using Content.Shared.CMU14.Kitchen;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Kitchen;

/// <summary>
///     Refreshes an open NutriCo menu when its map's foodline opens or closes.
/// </summary>
public sealed partial class CMUFoodlineVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFoodlineVendorComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<CMUFoodlineVendorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp(ent, out UserInterfaceComponent? ui))
            return;

        foreach (var bui in ui.ClientOpenInterfaces.Values)
        {
            if (bui is CMUNutriCoFoodBui food)
                food.Refresh();
            else if (bui is CMUAutoDrinkBui drink)
                drink.Refresh();
        }
    }
}
