using Content.Shared._CMU14.RoundSetup.LegacyBush;
using Robust.Server.GameObjects;

namespace Content.Server._CMU14.ZLevels.Compatibility;

/// <summary>
/// Resolves the data-driven ship markers embedded in the pre-rebase USS Bush maps.
/// </summary>
public sealed partial class CMULegacyBushMarkerSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VendorMarkerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<VendorMarkerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Replacement != null)
            _pending.Add(ent);
    }

    public override void Update(float frameTime)
    {
        try
        {
            foreach (var markerUid in _pending)
            {
                if (TerminatingOrDeleted(markerUid) ||
                    !TryComp<VendorMarkerComponent>(markerUid, out var marker) ||
                    marker.Replacement is not { } replacement ||
                    !TryComp(markerUid, out TransformComponent? markerTransform))
                {
                    continue;
                }

                var spawned = Spawn(replacement, markerTransform.Coordinates);
                _transform.SetLocalRotation(spawned, markerTransform.LocalRotation);

                if (marker.PreserveName)
                    _metaData.SetEntityName(spawned, MetaData(markerUid).EntityName);

                QueueDel(markerUid);
            }
        }
        finally
        {
            _pending.Clear();
        }
    }
}
