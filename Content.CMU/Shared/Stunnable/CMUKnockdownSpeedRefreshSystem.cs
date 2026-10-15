using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;

namespace Content.Shared.CMU14.Stunnable;

// paralysis recovery stands you up while KnockedDown still runs, baking its crawl slow in, then the
// shutdown's Stand() no-ops and never refreshes again. refresh once it's actually gone
public sealed partial class CMUKnockdownSpeedRefreshSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<KnockedDownComponent, ComponentRemove>(OnKnockedDownRemove);
    }

    private void OnKnockedDownRemove(Entity<KnockedDownComponent> ent, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        _movementSpeed.RefreshFrictionModifiers(ent.Owner);
    }
}
