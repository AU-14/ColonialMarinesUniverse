namespace Content.Shared.Preferences.Loadouts;

public sealed partial class LoadoutPrototype
{
    /// <summary>
    /// RMC loadout point cost.
    /// </summary>
    [DataField]
    public int? Cost;

    /// <summary>
    /// Adds components from the loadout equipment prototypes instead of equipping their entities.
    /// </summary>
    [DataField]
    public bool ComponentsAdd;
}
