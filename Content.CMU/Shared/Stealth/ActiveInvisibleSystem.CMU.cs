using Content.Shared.CMU14.Stealth;

namespace Content.Shared._RMC14.Stealth;

public sealed partial class ActiveInvisibleSystem
{
    private void InitializeCMUInvisibilityLifetime()
    {
        // Lifecycle hooks have one owner; cloak-specific behavior listens on its own components.
        SubscribeLocalEvent<EntityActiveInvisibleComponent, ComponentStartup>(OnCMUInvisibilityStartup);
        SubscribeLocalEvent<EntityActiveInvisibleComponent, ComponentShutdown>(OnCMUInvisibilityShutdown);
    }

    private void OnCMUInvisibilityStartup(Entity<EntityActiveInvisibleComponent> ent, ref ComponentStartup args)
    {
        var ev = new CMUInvisibilityStartedEvent();
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    private void OnCMUInvisibilityShutdown(Entity<EntityActiveInvisibleComponent> ent, ref ComponentShutdown args)
    {
        var ev = new CMUInvisibilityStoppedEvent();
        RaiseLocalEvent(ent.Owner, ref ev);
    }
}
