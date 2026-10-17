using Content.Client._RMC14.Dropship.Utility;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.TacticalMap;

namespace Content.Client._RMC14.Dropship.Weapon;

public sealed class DropshipWeaponSystem : SharedDropshipWeaponSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DropshipTerminalWeaponsComponent, AfterAutoHandleStateEvent>(OnWeaponsState);
        SubscribeLocalEvent<RMCEquipmentDeployerComponent, RMCEquipmentDeployerStateUpdatedEvent>(OnEquipmentDeployerState);
    }

    private void OnWeaponsState(Entity<DropshipTerminalWeaponsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshWeaponsUI(ent);
    }

    private void OnEquipmentDeployerState(Entity<RMCEquipmentDeployerComponent> ent,
        ref RMCEquipmentDeployerStateUpdatedEvent args)
    {
        // The equipment deployer is a contained entity, not part of the terminal's
        // own component state. Refresh open consoles when its authoritative state
        // arrives so deploy/retract controls and status text do not stay stale.
        var grid = Transform(ent).GridUid;
        var query = EntityQueryEnumerator<DropshipTerminalWeaponsComponent>();
        while (query.MoveNext(out var uid, out var terminal))
        {
            if (Transform(uid).GridUid != grid)
                continue;

            RefreshWeaponsUI((uid, terminal));
        }
    }

    protected override void RefreshWeaponsUI(Entity<DropshipTerminalWeaponsComponent> terminal)
    {
        try
        {
            base.RefreshWeaponsUI(terminal);
            if (!TryComp(terminal, out UserInterfaceComponent? ui))
                return;

            foreach (var open in ui.ClientOpenInterfaces.Values)
            {
                if (open is DropshipWeaponsBui bui)
                    bui.Refresh();
            }
        }
        catch (Exception e)
        {
            Log.Error($"Error refreshing {nameof(DropshipWeaponsBui)}:\n{e}");
        }
    }
}
