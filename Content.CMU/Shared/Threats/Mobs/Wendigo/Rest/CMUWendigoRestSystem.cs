using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Actions;
using Content.Shared.Movement.Components;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Rest;

/// <summary>
/// Rest for lab-made Wendigos, which are not xenos: the xeno Rest action only has a handler on XenoComponent.
/// Natural Wendigos are xenos and keep the stock handler. Resting itself (no movement or attacks, faster regen)
/// is all driven by XenoRestingComponent, so both behave the same.
/// </summary>
public sealed partial class CMUWendigoRestSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private XenoRestSystem _rest = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUWendigoRestComponent, XenoRestActionEvent>(OnRestAction);
    }

    private void OnRestAction(Entity<CMUWendigoRestComponent> ent, ref XenoRestActionEvent args)
    {
        if (args.Handled || HasComp<XenoComponent>(ent))
            return;

        if (_rest.CMUToggleRest(ent, args.Action))
            args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Like resting xenos, trying to move gets the Wendigo back up.
        var query = EntityQueryEnumerator<CMUWendigoRestComponent, XenoRestingComponent, InputMoverComponent>();
        while (query.MoveNext(out var uid, out _, out var rest, out var mover))
        {
            if (!rest.Running || !mover.HasDirectionalMovement || HasComp<XenoComponent>(uid))
                continue;

            foreach (var (actionId, actionComp) in _rmcActions.GetActionsWithEvent<XenoRestActionEvent>(uid))
            {
                if (actionComp.AttachedEntity != uid || actionComp is not { Enabled: true })
                    continue;

                if (actionComp.Cooldown.HasValue && actionComp.Cooldown.Value.End > _timing.CurTime)
                    continue;

                _actions.PerformAction(uid, (actionId, actionComp));
                break;
            }
        }
    }
}
