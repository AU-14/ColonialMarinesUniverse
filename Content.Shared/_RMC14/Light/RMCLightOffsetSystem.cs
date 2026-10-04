using System.Numerics;
using Content.Shared._RMC14.Map; // CMU14
using Content.Shared._RMC14.Sprite;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Light;

public sealed partial class RMCLightOffsetSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedRMCSpriteSystem _sprite = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!; // CMU14

    private static readonly Vector2 OffsetLightWallFace = new(0f, -0.495f);
    // CMU14 field: the engine culls shadow casting lights whose origin sits inside an
    // occluder, so lights mapped onto a wall tile must start past its hull
    private static readonly Vector2 OffsetLightPastWallFace = new(0f, -0.55f); // CMU14
    //private readonly HashSet<EntityUid> ToUpdate = new(); // CMU14

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCLightOffsetComponent, ComponentStartup>(OnLightStartup);
        SubscribeLocalEvent<RMCLightOffsetComponent, MapInitEvent>(OnLightUpdate);
        SubscribeLocalEvent<RMCLightOffsetComponent, EntParentChangedMessage>(OnLightUpdate);
    }

    private void OnLightStartup(Entity<RMCLightOffsetComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            OffsetLight(ent);
    }

    private void OnLightUpdate<T>(Entity<RMCLightOffsetComponent> ent, ref T args)
    {
        if (!TryComp(ent, out MetaDataComponent? metaData) ||
            metaData.EntityLifeStage < EntityLifeStage.MapInitialized)
        {
            return;
        }

        //ToUpdate.Add(ent); // CMU14: write only, never read

        if (_net.IsClient)
            return;

        if (TerminatingOrDeleted(ent))
            return;

        OffsetLight(ent);
    }

    private void OffsetLight(Entity<RMCLightOffsetComponent> ent)
    {
        var sprite = EnsureComp<SpriteSetRenderOrderComponent>(ent);
        switch (Transform(ent).LocalRotation.GetDir())
        {
            case Direction.South:
                _sprite.SetOffset(ent, new Vector2(0.45f, -0.32f));
                break;
            case Direction.East:
                _sprite.SetOffset(ent, new Vector2(0.7f, -1.45f));
                break;
            case Direction.North:
                _sprite.SetOffset(ent, new Vector2(-0.5f, -1.5f));
                break;
            case Direction.West:
                _sprite.SetOffset(ent, new Vector2(-0.7f, -0.4f));
                break;
        }

        ApplyPointLightOffset(ent);

        Dirty(ent, sprite);
    }

    private void ApplyPointLightOffset(EntityUid uid)
    {
        if (!_pointLight.TryGetLight(uid, out var light))
            return;

        // CMU14: keep the light origin outside any occluder, so only escape past
        // the wall face when the faced tile the origin lands in is clear
        var facing = Transform(uid).LocalRotation.GetDir();
        var offset = TileHasOccluder(uid) && !TileHasOccluder(uid, facing)
            ? OffsetLightPastWallFace
            : OffsetLightWallFace;
        if (light.Offset == offset)
            return;

        light.Offset = offset;
        Dirty(uid, light);
    }

    // CMU14 method: wall occluders use the full tile hull, so an enabled
    // occluder on the probed tile covers any origin pushed into it
    private bool TileHasOccluder(EntityUid uid, Direction? offset = null)
    {
        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(uid, offset);
        while (anchored.MoveNext(out var other))
        {
            if (TryComp<OccluderComponent>(other, out var occluder)
                && occluder.Enabled)
            {
                return true;
            }
        }

        return false;
    }
}
