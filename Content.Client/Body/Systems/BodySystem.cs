using Content.Shared.Body.Systems;

namespace Content.Client.Body.Systems;

public sealed class BodySystem : SharedBodySystem
{
    public override void Initialize()
    {
        // Compatibility facade only; the modern shared BodySystem owns subscriptions.
    }
}
