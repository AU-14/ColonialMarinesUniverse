using Content.Server.Ghost.Roles.Components;
using Content.Shared.Ghost.Roles.Components;

namespace Content.Server.Ghost.Roles;

public sealed partial class GhostRoleSystem
{
    public void UpdateRMCSpawnerAvailability(Entity<GhostRoleMobSpawnerComponent> spawner)
    {
        if (TryComp(spawner, out GhostRoleComponent? ghostRole))
        {
            ghostRole.Taken = spawner.Comp.CurrentTakeovers >= spawner.Comp.AvailableTakeovers;
            if (ghostRole.Taken)
                UnregisterGhostRole((spawner, ghostRole));
            else
                RegisterGhostRole((spawner, ghostRole));
        }

        UpdateAllEui();
    }
}
