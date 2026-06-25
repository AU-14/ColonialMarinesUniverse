using Content.Shared.Coordinates;
using Robust.Shared.Prototypes;
using AbominationComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationComponent;
using AbominationPlantKudzuActionEvent
    = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationPlantKudzuActionEvent;

namespace Content.Server._CMU14.Threats.Mobs.Abomination;

public sealed class AbominationKudzuSystem : EntitySystem
{
    public static readonly EntProtoId KudzuSource = "AU14AbominationFleshKudzuSource";

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationComponent, AbominationPlantKudzuActionEvent>(OnPlantKudzu);
    }

    private void OnPlantKudzu(Entity<AbominationComponent> ent, ref AbominationPlantKudzuActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Spawn(KudzuSource, ent.Owner.ToCoordinates());
    }
}
