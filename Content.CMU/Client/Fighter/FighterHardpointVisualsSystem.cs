using Content.Shared.CMU14.Fighter;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterHardpointVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FighterWeaponsComponent, AfterAutoHandleStateEvent>(OnWeaponsState);
    }

    private void OnWeaponsState(Entity<FighterWeaponsComponent> weapons, ref AfterAutoHandleStateEvent ev)
    {
        foreach (var slot in weapons.Comp.Loadout)
        {
            if (slot.Slot < 0 || slot.Slot >= weapons.Comp.Hardpoints.Count ||
                !TryComp(weapons.Comp.Hardpoints[slot.Slot], out SpriteComponent? sprite)) continue;
            var state = slot.Kind switch
            {
                FighterWeaponKind.Gau => slot.Rounds > 0 ? "30mm_cannon_loaded1" : "30mm_cannon_loaded0",
                FighterWeaponKind.Rockets => slot.Rounds >= 6 ? "minirocket_pod_loaded" : slot.Rounds > 0
                    ? "minirocket_pod_loaded_" + slot.Rounds : "minirocket_pod_installed",
                FighterWeaponKind.Missile => "single",
                _ => "rocket_pod_installed",
            };
            _sprites.LayerSetSprite((weapons.Comp.Hardpoints[slot.Slot], sprite), 0,
                new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Objects/dropship_attachments.rsi"), state));
        }
    }
}
