using Content.Shared.Gibbing;
using Content.Shared.Mobs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using AbominationComponent = Content.Shared.CMU14.Threats.Mobs.Abomination.AbominationComponent;

namespace Content.Server.CMU14.Threats.Mobs.Abomination;

/// <summary>
///     When any abomination dies, gib them and seed a patch of flesh kudzu at
///     their feet.
/// </summary>
public sealed partial class AbominationDeathSystem : EntitySystem
{
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    public static readonly EntProtoId FleshKudzuSource = "AU14AbominationFleshKudzuSource";

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<AbominationComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        // Capture the corpse coordinates *before* gibbing — once the body is
        // gibbed the entity is deleted and ToCoordinates returns an invalid map.
        TransformComponent xform = Transform(ent.Owner);
        MapCoordinates coords = _transform.GetMapCoordinates(ent.Owner, xform);

        _gibbing.Gib(ent.Owner);

        if (coords.MapId == default(MapId))
            return;

        Spawn(FleshKudzuSource, coords);
    }
}
