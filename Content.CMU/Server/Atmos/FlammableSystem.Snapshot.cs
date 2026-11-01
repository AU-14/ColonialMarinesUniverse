namespace Content.Server.Atmos.EntitySystems;

public sealed partial class FlammableSystem
{
    // makes the next Update rebuild the flammable snapshot. tests that spawn something and call
    // Update by hand need it, otherwise the new entity is only in there if the pooled server's
    // snapshot happens to be old enough
    public void RefreshUpdateSnapshot()
    {
        _nextSnapshot = TimeSpan.Zero;
    }
}
