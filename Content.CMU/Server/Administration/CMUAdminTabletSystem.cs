using Content.Shared._RMC14.AlertLevel;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.ControlComputer;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Administration;

namespace Content.Server.CMU14.Administration;

public sealed class CMUAdminTabletSystem : EntitySystem
{
    [Dependency] private RMCAlertLevelSystem _alertLevel = default!;
    [Dependency] private SharedMarineAnnounceSystem _marineAnnounce = default!;

    public override void Initialize()
    {
        Subs.BuiEvents<CMUAdminTabletComponent>(MarineCommunicationsComputerUI.Key,
            subs =>
            {
                subs.Event<CMUAdminTabletSetAlertLevelMsg>(OnSetAlertLevel);
                subs.Event<CMUAdminTabletToggleFactionMsg>(OnToggleFaction);
            });
    }

    private void OnSetAlertLevel(Entity<CMUAdminTabletComponent> ent, ref CMUAdminTabletSetAlertLevelMsg args)
        => _alertLevel.Set(args.Level, args.Actor);

    private void OnToggleFaction(Entity<CMUAdminTabletComponent> ent, ref CMUAdminTabletToggleFactionMsg args)
    {
        if (!TryComp(ent, out MarineCommunicationsComputerComponent? comms))
            return;

        var faction = comms.Faction == "govfor" ? "opfor" : "govfor";
        _marineAnnounce.SetComputerFaction((ent, comms), faction);

        if (TryComp(ent, out TacticalMapComputerComponent? tacMap))
        {
            tacMap.Faction = faction;
            Dirty(ent, tacMap);
        }

        if (TryComp(ent, out MarineControlComputerComponent? control))
        {
            control.Faction = faction;
            Dirty(ent, control);
        }
    }
}
