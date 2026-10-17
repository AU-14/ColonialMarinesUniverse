using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged;

public partial class MagazineAmmoProviderComponent
{
    /// <summary>
    /// Whether an automatically ejected magazine should enter a free hand before falling to the ground.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool EjectToHand;
}
