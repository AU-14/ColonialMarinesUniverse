using Content.Shared.CMU14.Threats.Mobs.Biomorph;
using Content.Shared.Gibbing;
using Content.Shared.Mobs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Threats.Mobs.Biomorph;

/// <summary>
///     When any abomination dies, gib them and seed a patch of flesh kudzu at
///     their feet.
/// </summary>
public sealed partial class BiomorphDeathSystem : EntitySystem
{
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    public static readonly EntProtoId FleshKudzuSource = "AU14BiomorphFleshKudzuSource";

    public override void Initialize()
    {
        SubscribeLocalEvent<BiomorphComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<BiomorphComponent> ent, ref MobStateChangedEvent args)
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
