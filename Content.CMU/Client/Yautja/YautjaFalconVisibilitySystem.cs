using Content.Shared.CMU14.Yautja;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client.CMU14.Yautja;

public sealed partial class YautjaFalconVisibilitySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public override void Update(float frameTime)
    {
        var local = _player.LocalEntity;
        // CMU14: the Falcon is the reference's hunter-HUD hologram, which its owner sees while carrying the bracers
        // that run it - falcon.dm:107-112 stores owned_bracers, registers COMSIG_ITEM_DROPPED on them and calls
        // add_to_all_mob_huds(), and dropping the bracers ends the hologram. Tying visibility to the species alone
        // (the previous HasComp<YautjaComponent> check) left a human who is allowed to pilot the drone unable to
        // see it. A Yautja still always counts, as does the drone's controller, who the server only accepts with
        // worn or held bracers.
        var visible = local is { } viewer &&
                      (HasComp<YautjaComponent>(viewer) ||
                       HasComp<YautjaFalconControllerComponent>(viewer) ||
                       HasComp<GhostComponent>(viewer) ||
                       CarriesBracer(viewer));

        var query = EntityQueryEnumerator<YautjaFalconDroneDeployedComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), YautjaFalconVisualLayers.Base, out var layer, true))
                continue;

            _sprite.LayerSetVisible((uid, sprite), layer, visible);
        }
    }

    /// <summary>
    /// CMU14: whether this viewer carries the bracers the Falcon runs on, worn in the gloves slot the control
    /// check requires or held in hand. This is the reference's rule (falcon.dm:55-70 demands worn yautja bracers to
    /// control, and the hologram follows the bracers rather than a species), which is what lets an ordinary human
    /// see the drone they are allowed to pilot.
    /// </summary>
    private bool CarriesBracer(EntityUid viewer)
    {
        var slots = _inventory.GetSlotEnumerator(viewer, SlotFlags.GLOVES);
        while (slots.NextItem(out var item))
        {
            if (HasComp<YautjaGearContainerComponent>(item))
                return true;
        }

        // A bracer merely held in hand is covered by the controller check above: the server only accepts a
        // controller who has the bracers worn or held (YautjaItemSystem.TryGetFalconBracer).
        return false;
    }
}
