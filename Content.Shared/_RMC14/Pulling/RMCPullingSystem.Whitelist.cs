namespace Content.Shared._RMC14.Pulling;

public sealed partial class RMCPullingSystem
{
    private void InitializeWhitelist()
    {
        SubscribeLocalEvent<PullWhitelistComponent, SetPullWhitelistEvent>(OnSetPullWhitelist);
    }

    private void OnSetPullWhitelist(Entity<PullWhitelistComponent> ent, ref SetPullWhitelistEvent args)
    {
        ent.Comp.Whitelist = args.Whitelist;
        Dirty(ent);
    }
}