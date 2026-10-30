using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Kitchen;

/// <summary>
///     The DEFAC kitchen's shutters. While any of them on a map are open the foodline is open for business, and the
///     NutriCo machines on that map stop dispensing so people eat what the mess technicians cook.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUKitchenShutterComponent : Component;

/// <summary>
///     A NutriCo food or drink machine that stops dispensing while its map's foodline is open.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUFoodlineVendorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool FoodlineOpen;

    [DataField]
    public LocId Message = "cmu-foodline-open";
}
