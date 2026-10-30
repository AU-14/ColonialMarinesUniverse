using Content.Shared.CMU14.Round;

namespace Content.Shared._RMC14.Dropship.Weapon;

public abstract partial class SharedDropshipWeaponSystem
{
    public string? GetConsoleFaction(EntityUid console)
    {
        if (TryComp(console, out WhitelistedShuttleComponent? whitelist) &&
            !string.IsNullOrWhiteSpace(whitelist.Faction))
        {
            return whitelist.Faction;
        }

        return _dropship.TryGetGridFaction(console, out var faction) ? faction : null;
    }

    public void SetTerminalTarget(Entity<DropshipTerminalWeaponsComponent> terminal, EntityUid? target)
    {
        SetTarget(terminal, target);
        RefreshWeaponsUI(terminal);
    }
}
