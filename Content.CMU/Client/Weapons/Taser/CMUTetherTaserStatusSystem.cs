using Content.Client.Items;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared.CMU14.Weapons.Taser;
using Content.Shared.Power.EntitySystems;
using Robust.Client.Graphics;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Weapons.Taser;

/// <summary>
/// Shows a wired taser's battery as a charge bar in the hand HUD, with the state of its wire. Replaces the shot boxes on
/// the projectile disabler gun, and sits under the pistol's own counter for an underbarrel taser. Also draws the wires.
/// </summary>
public sealed partial class CMUTetherTaserStatusSystem : EntitySystem
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUTetherTaserComponent, GunSystem.AmmoCounterControlEvent>(OnAmmoCounterControl,
            after: [typeof(GunSystem)]);
        SubscribeLocalEvent<AttachableHolderComponent, ItemStatusCollectMessage>(OnHolderStatus);

        _overlays.AddOverlay(new CMUTaserWireOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<CMUTaserWireOverlay>();
    }

    private void OnAmmoCounterControl(Entity<CMUTetherTaserComponent> ent, ref GunSystem.AmmoCounterControlEvent args)
    {
        args.Control = new CMUTaserChargeControl(ent.Owner, EntityManager, _battery, _timing);
    }

    private void OnHolderStatus(Entity<AttachableHolderComponent> ent, ref ItemStatusCollectMessage args)
    {
        foreach (var slot in ent.Comp.Slots.Keys)
        {
            if (!_container.TryGetContainer(ent, slot, out var container))
                continue;

            foreach (var attached in container.ContainedEntities)
            {
                if (HasComp<CMUTetherTaserComponent>(attached))
                    args.Controls.Add(new CMUTaserChargeControl(attached, EntityManager, _battery, _timing));
            }
        }
    }
}
