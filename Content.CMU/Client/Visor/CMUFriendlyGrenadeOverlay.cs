using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Visor;
using Content.Shared.Tag;
using Content.Shared.Trigger.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Visor;

/// <summary>
/// Draws a red ring around primed grenades thrown or primed by friendlies, for someone looking through a squad optic.
/// </summary>
public sealed class CMUFriendlyGrenadeOverlay : Overlay
{
    private static readonly ProtoId<TagPrototype> GrenadeTag = "Grenade";
    private static readonly Color RingColor = Color.Red.WithAlpha(0.85f);
    private const float RingRadius = 0.6f;
    private const float RingThickness = 0.06f;
    private const int RingLayers = 4;

    private readonly IEntityManager _entity;
    private readonly IPlayerManager _player;
    private readonly GunIFFSystem _iff;
    private readonly TagSystem _tag;
    private readonly SharedTransformSystem _transform;

    private readonly HashSet<EntProtoId<IFFFactionComponent>> _viewerFactions = new();
    private readonly HashSet<EntProtoId<IFFFactionComponent>> _userFactions = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CMUFriendlyGrenadeOverlay(IEntityManager entity, IPlayerManager player)
    {
        _entity = entity;
        _player = player;
        _iff = entity.System<GunIFFSystem>();
        _tag = entity.System<TagSystem>();
        _transform = entity.System<SharedTransformSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { } viewer &&
               _entity.HasComponent<CMUSquadVisorWearerComponent>(viewer) &&
               _iff.TryGetFactions(viewer, _viewerFactions);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entity.EntityQueryEnumerator<ActiveTimerTriggerComponent, TimerTriggerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var timer, out var xform))
        {
            if (xform.MapID != args.MapId ||
                timer.User is not { } user ||
                !_tag.HasTag(uid, GrenadeTag) ||
                !IsFriendly(user))
            {
                continue;
            }

            var position = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Enlarged(RingRadius).Contains(position))
                continue;

            for (var i = 0; i < RingLayers; i++)
            {
                handle.DrawCircle(position, RingRadius + i * RingThickness / RingLayers, RingColor, filled: false);
            }
        }
    }

    private bool IsFriendly(EntityUid user)
    {
        if (!_iff.TryGetFactions(user, _userFactions))
            return false;

        return _userFactions.Overlaps(_viewerFactions);
    }
}
