using Content.Shared._RMC14.Vents;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Physics;

namespace Content.Shared.CMU14.Vents;

// interaction range ignores directional cades, so xenos dove into vents straight through a barricade line
public sealed partial class CMUVentBarricadeSystem : EntitySystem
{
    [Dependency] private SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VentEntranceComponent, GettingInteractedWithAttemptEvent>(OnGettingInteractedWith);
    }

    private void OnGettingInteractedWith(Entity<VentEntranceComponent> ent, ref GettingInteractedWithAttemptEvent args)
    {
        if (args.Cancelled || !HasComp<VentCrawlerComponent>(args.Uid))
            return;

        if (!_interaction.InRangeUnobstructed(args.Uid, ent.Owner, collisionMask: CollisionGroup.BarricadeImpassable))
            args.Cancelled = true;
    }
}
