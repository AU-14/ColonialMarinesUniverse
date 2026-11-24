using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids.Egg;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Actions;

public sealed partial class RMCActionsSystem
{
    private void OnQueenOvipositorChanged(Entity<RMCActionOrderComponent> queen, ref XenoOvipositorChangedEvent args)
    {
        switch (queen.Comp.Id.Id)
        {
            case "CMXenoQueenImmature":
            case "CMUQueenImmatureOvipositorOrder":
                SetOrderId(queen, args.Attached ? "CMUQueenImmatureOvipositorOrder" : "CMXenoQueenImmature");
                break;
            case "CMXenoQueen":
            case "CMUQueenMatureOvipositorOrder":
                SetOrderId(queen, args.Attached ? "CMUQueenMatureOvipositorOrder" : "CMXenoQueen");
                break;
        }
    }

    public override void SetOrderId(EntityUid uid, EntProtoId id)
    {
        if (!TryComp<RMCActionOrderComponent>(uid, out var order) || order.Id == id)
            return;

        base.SetOrderId(uid, id);
        if (_player.TryGetSessionByEntity(uid, out var player))
            LoadOrder((uid, order), player);
    }
}
