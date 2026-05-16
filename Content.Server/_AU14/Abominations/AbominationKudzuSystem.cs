using Content.Shared._AU14.Abominations;
using Content.Shared.Coordinates;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Abominations;

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
