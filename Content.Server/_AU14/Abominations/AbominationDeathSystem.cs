using Content.Shared._AU14.Abominations;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Abominations;

/// <summary>
/// When any abomination dies, gib them and seed a patch of flesh kudzu at
/// their feet.
/// </summary>
public sealed partial class AbominationDeathSystem : EntitySystem
{
    public static readonly EntProtoId FleshKudzuSource = "AU14AbominationFleshKudzuSource";

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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
        var xform = Transform(ent.Owner);
        var coords = _transform.GetMapCoordinates(ent.Owner, xform);

        _body.GibBody(ent.Owner);

        if (coords.MapId == default)
            return;

        Spawn(FleshKudzuSource, coords);
    }
}
