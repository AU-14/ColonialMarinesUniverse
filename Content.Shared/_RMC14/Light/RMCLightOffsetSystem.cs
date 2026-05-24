using System.Numerics;
using Content.Shared._RMC14.Sprite;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Light;

public sealed partial class RMCLightOffsetSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedRMCSpriteSystem _sprite = default!;

    private static readonly Vector2 OffsetLightSouth = new(0f, -0.5f);
    private static readonly Vector2 OffsetLightEastWest = new(0f, -0.75f);
    private static readonly Vector2 OffsetLightNorth = new(0f, -1f);

    private readonly HashSet<EntityUid> ToUpdate = new();


    public override void Initialize()
    {
        SubscribeLocalEvent<RMCLightOffsetComponent, MapInitEvent>(OnLightUpdate);
        SubscribeLocalEvent<RMCLightOffsetComponent, EntParentChangedMessage>(OnLightUpdate);
    }

    private void OnLightUpdate<T>(Entity<RMCLightOffsetComponent> ent, ref T args)
    {
        if (!TryComp(ent, out MetaDataComponent? metaData) ||
            metaData.EntityLifeStage < EntityLifeStage.MapInitialized)
        {
            return;
        }

        ToUpdate.Add(ent);

        if (_net.IsClient)
            return;

        if (TerminatingOrDeleted(ent))
            return;

        var sprite = EnsureComp<SpriteSetRenderOrderComponent>(ent);
        var direction = Transform(ent).LocalRotation.GetDir();
        ApplyPointLightOffset(ent, direction);
        /*
        switch (direction)
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
        */

        Dirty(ent, sprite);
    }

    private void ApplyPointLightOffset(EntityUid uid, Direction direction)
    {
        if (!_pointLight.TryGetLight(uid, out var light))
            return;

        var offset = direction switch
        {
            Direction.North => OffsetLightNorth,
            Direction.East or Direction.West => OffsetLightEastWest,
            _ => OffsetLightSouth,
        };

        if (light.Offset == offset)
            return;

        light.Offset = offset;
        Dirty(uid, light);
    }
}
