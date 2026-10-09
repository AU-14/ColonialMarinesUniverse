using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.ThreeD;

namespace Content.Server.CMU14.ThreeD;

/// <summary>Vehicle cabins inherit opt-in availability without enabling unrelated maps.</summary>
public sealed class CMU3DVehicleInteriorSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metadata = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleInteriorComponent, VehicleInteriorLoadedEvent>(OnLoaded);
        SubscribeLocalEvent<CMU3DVehicleInteriorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMU3DVehicleInteriorComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CMU3DVehicleInteriorComponent, MapUidChangedEvent>(OnMapChanged);
        SubscribeLocalEvent<CMU3DVehicleInteriorComponent, MetaFlagRemoveAttemptEvent>(OnFlagRemove);
    }

    private void OnLoaded(Entity<VehicleInteriorComponent> ent, ref VehicleInteriorLoadedEvent args)
    {
        EnsureComp<CMU3DVehicleCabinComponent>(args.MapUid);
        var interior = EnsureComp<CMU3DVehicleInteriorComponent>(ent);
        interior.Map = args.MapUid;
        Refresh(interior, Transform(ent).MapUid);
    }

    private void OnStartup(Entity<CMU3DVehicleInteriorComponent> ent, ref ComponentStartup args) =>
        _metadata.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);

    private void OnRemove(Entity<CMU3DVehicleInteriorComponent> ent, ref ComponentRemove args)
    {
        _metadata.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);
        Refresh(ent.Comp, null);
    }

    private void OnMapChanged(Entity<CMU3DVehicleInteriorComponent> ent, ref MapUidChangedEvent args) =>
        Refresh(ent.Comp, args.NewMap);

    private void OnFlagRemove(Entity<CMU3DVehicleInteriorComponent> ent, ref MetaFlagRemoveAttemptEvent args)
    {
        // Other users of transform events may stop tracking this vehicle independently.
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.ToRemove &= ~MetaDataFlags.ExtraTransformEvents;
    }

    private void Refresh(CMU3DVehicleInteriorComponent interior, EntityUid? sourceMap)
    {
        if (!interior.Map.IsValid() || TerminatingOrDeleted(interior.Map))
            return;
        if (HasComp<CMU3DMapComponent>(sourceMap))
            EnsureComp<CMU3DMapComponent>(interior.Map);
        else
            RemComp<CMU3DMapComponent>(interior.Map);
    }
}
