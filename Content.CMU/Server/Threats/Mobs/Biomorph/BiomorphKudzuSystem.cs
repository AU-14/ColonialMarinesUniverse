using Content.Shared.CMU14.Threats.Mobs.Biomorph;
using Content.Shared.Coordinates;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Threats.Mobs.Biomorph;

public sealed class BiomorphKudzuSystem : EntitySystem
{
    public static readonly EntProtoId KudzuSource = "AU14BiomorphFleshKudzuSource";

    public override void Initialize()
    {
        SubscribeLocalEvent<BiomorphComponent, BiomorphPlantKudzuActionEvent>(OnPlantKudzu);
    }

    private void OnPlantKudzu(Entity<BiomorphComponent> ent, ref BiomorphPlantKudzuActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Spawn(KudzuSource, ent.Owner.ToCoordinates());
    }
}
