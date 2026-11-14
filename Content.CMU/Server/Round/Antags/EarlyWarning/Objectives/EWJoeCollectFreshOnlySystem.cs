using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Objectives.Components;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// Put beside an <see cref="EWJoeCollectConditionComponent"/> whose whitelist matches things the Joe already
/// carries (its own ID card, the cells in its own tools). When the objective is assigned, everything matching
/// in the Joe's container tree is marked spent, so only what it takes afterwards counts and the directive
/// cannot be finished by its own kit.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeCollectFreshOnlyComponent : Component;

public sealed class EWJoeCollectFreshOnlySystem : EntitySystem
{
    [Dependency] private EWJoeCollectConditionSystem _collect = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeCollectFreshOnlyComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
    }

    private void OnAfterAssign(Entity<EWJoeCollectFreshOnlyComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (args.Mind.OwnedEntity is not { } owner
            || !TryComp(owner, out EWJoeComponent? joe)
            || !TryComp(ent, out EWJoeCollectConditionComponent? collect))
        {
            return;
        }

        joe.SpentCollectibles.UnionWith(_collect.HeldMatching(owner, collect.Whitelist));
    }
}
