using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Ladder;

namespace Content.Shared.CMU14.ZLevels.Core.EntitySystems;

// clicking a ladder with the pulling hand hits the pull's virtual item, which eats every interaction,
// so you could never start a climb while dragging someone. route that click to the ladder instead,
// the climb itself already brings whatever you're pulling along.
public sealed partial class CMULadderPullClimbSystem : EntitySystem
{
    [Dependency] private SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PullableComponent, ShouldHandleVirtualItemInteractEvent>(OnPulledShouldHandle);
    }

    private void OnPulledShouldHandle(Entity<PullableComponent> ent, ref ShouldHandleVirtualItemInteractEvent args)
    {
        var interact = args.Event;
        if (!interact.CanReach ||
            interact.Target is not { } ladder ||
            ent.Comp.Puller != interact.User)
        {
            return;
        }

        if (!HasComp<CMUZLevelLadderComponent>(ladder) && !HasComp<LadderComponent>(ladder))
            return;

        // activate straight away and leave Handle false, re-raising the click on the pulled mob
        // would let any of its own BeforeRangedInteract handlers react to the ladder
        _interaction.InteractionActivate(interact.User, ladder);
    }
}
