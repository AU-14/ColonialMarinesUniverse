using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI.ProfileEditorControls;

public sealed partial class ProfilePreviewSpriteView
{
    /// <summary>Use the public arrival outfit instead of inventing a loadout from a default profile.</summary>
    public void LoadArrivalPreview(HumanoidCharacterProfile profile, IReadOnlyDictionary<string, EntProtoId> outfit)
    {
        LoadPreview(profile, showClothes: false);
        var inventory = EntMan.System<InventorySystem>();
        foreach (var (slot, prototype) in outfit)
        {
            var item = EntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
            if (!inventory.TryEquip(PreviewDummy, item, slot, silent: true, force: true))
                EntMan.DeleteEntity(item);
        }
    }
}
