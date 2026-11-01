using Content.Shared.Preferences;

namespace Content.Server.Station.Systems;

public sealed partial class StationSpawningSystem
{
    private void EquipGlobalLoadout(EntityUid entity, HumanoidCharacterProfile? profile)
    {
        if (profile == null)
            return;

        var session = _actors.GetSession(entity);
        var loadout = profile.GetLoadoutOrDefault(HumanoidCharacterProfile.GlobalLoadoutId,
            session, profile.Species, EntityManager, _prototypeManager).Clone();
        if (session != null)
            loadout.EnsureValid(profile, session, IoCManager.Instance!);
        EquipRoleLoadout(entity, loadout, _prototypeManager.Index(loadout.Role));
    }
}
