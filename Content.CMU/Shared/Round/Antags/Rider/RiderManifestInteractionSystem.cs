using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Round.Antags.Rider;

// shared on purpose, otherwise the client predicts the button press and the server snaps it back
public sealed class RiderManifestInteractionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<RiderManifestComponent, InteractionAttemptEvent>(OnInteractionAttempt);
    }

    private void OnInteractionAttempt(Entity<RiderManifestComponent> ent, ref InteractionAttemptEvent args)
    {
        // the phantom's action bar checks with a null target, so only block reaching for actual things
        if (args.Target is { } target && target != ent.Owner)
            args.Cancelled = true;
    }
}
