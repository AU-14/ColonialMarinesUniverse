using Content.Shared.Chemistry.Components;
using Content.Shared.DragDrop;

namespace Content.Shared.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotDragDropSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ReagentTankComponent, CanDragEvent>(OnCanDragTank);
        SubscribeLocalEvent<CMUConstructionRobotComponent, CanDropTargetEvent>(OnCanDrop);
    }

    private void OnCanDragTank(Entity<ReagentTankComponent> ent, ref CanDragEvent args)
    {
        if (ent.Comp.TankType == ReagentTankType.Fuel)
            args.Handled = true;
    }

    private void OnCanDrop(Entity<CMUConstructionRobotComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled || !TryComp<ReagentTankComponent>(args.Dragged, out var tank) ||
            tank.TankType != ReagentTankType.Fuel)
            return;

        args.Handled = true;
        args.CanDrop = true;
    }
}
