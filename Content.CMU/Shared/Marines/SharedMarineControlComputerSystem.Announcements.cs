using Content.Shared._RMC14.Marines.Announce;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Marines.ControlComputer;

public abstract partial class SharedMarineControlComputerSystem
{
    [Dependency] private CMUSharedZLevelsSystem _shipZLevels = default!;

    public Filter GetShipAnnouncementFilter(Entity<MarineControlComputerComponent> computer)
    {
        var map = _warship.TryGetWarshipMap(computer, out var warshipMap)
            ? warshipMap
            : _transform.GetMapId(computer.Owner);
        var faction = SharedMarineAnnounceSystem.ResolveAnnouncementFaction(computer.Comp.Faction);
        return Filter.Empty().AddWhereAttachedEntity(uid =>
            _shipZLevels.IsSameZNetwork(_transform.GetMapId(uid), map) &&
            IsShipAnnouncementRecipient(uid, faction));
    }
}
