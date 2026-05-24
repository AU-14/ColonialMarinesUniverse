using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._CMU14.ZLevels.Ghost;
using Content.Shared.Actions;

namespace Content.Server._CMU14.ZLevels.Ghost;

public sealed partial class CMUZLevelGhostMoverSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevel = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUZLevelGhostMoverComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUZLevelGhostMoverComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CMUZLevelGhostMoverComponent, CMUZLevelActionUp>(OnZLevelUp);
        SubscribeLocalEvent<CMUZLevelGhostMoverComponent, CMUZLevelActionDown>(OnZLevelDown);

        SubscribeLocalEvent<CMUZLevelViewerComponent, MapInitEvent>(OnViewerMapInit);
        SubscribeLocalEvent<CMUZLevelViewerComponent, ComponentRemove>(OnViewerRemove);
    }

    private void OnMapInit(Entity<CMUZLevelGhostMoverComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ZLevelUpActionEntity, ent.Comp.UpActionProto);
        _actions.AddAction(ent, ref ent.Comp.ZLevelDownActionEntity, ent.Comp.DownActionProto);
    }

    private void OnRemove(Entity<CMUZLevelGhostMoverComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelUpActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelDownActionEntity);
    }

    private void OnViewerMapInit(Entity<CMUZLevelViewerComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ZLevelActionEntity, ent.Comp.ActionProto);
    }

    private void OnViewerRemove(Entity<CMUZLevelViewerComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelActionEntity);
    }

    private void OnZLevelDown(Entity<CMUZLevelGhostMoverComponent> ent, ref CMUZLevelActionDown args)
    {
        if (args.Handled)
            return;

        args.Handled = _zLevel.TryMoveDown(ent);
    }

    private void OnZLevelUp(Entity<CMUZLevelGhostMoverComponent> ent, ref CMUZLevelActionUp args)
    {
        if (args.Handled)
            return;

        args.Handled = _zLevel.TryMoveUp(ent);
    }
}
