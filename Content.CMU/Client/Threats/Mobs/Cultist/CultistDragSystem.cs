using Content.Shared._RMC14.Xenonids;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Systems;
using CultistComponent = Content.Shared._CMU14.Threats.Mobs.Cultist.CultistComponent;

namespace Content.Client._CMU14.Threats.Mobs.Cultist;

public sealed partial class CultistDragSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CultistComponent, CanStartDragEvent>(OnCultistCanStartDrag);
    }

    private void OnCultistCanStartDrag(Entity<CultistComponent> cultist, ref CanStartDragEvent args)
    {
        EntityUid target = args.Target;
        if (HasComp<CultistComponent>(target) || HasComp<XenoComponent>(target))
            return;

        if (_mobState.IsDead(target))
            args.Cancelled = true;
    }
}
