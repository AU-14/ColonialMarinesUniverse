using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship;

public sealed class MohawkFlareWarningSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly Dictionary<EntityUid, TimeSpan> _warned = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<MohawkChinGunFlareLockedEvent>(OnFlareLocked);
    }

    private void OnFlareLocked(ref MohawkChinGunFlareLockedEvent args)
    {
        if (_warned.TryGetValue(args.Flare, out var last) && _timing.CurTime - last < TimeSpan.FromSeconds(30))
            return;
        _warned[args.Flare] = _timing.CurTime;
        var center = _transform.GetMapCoordinates(args.Flare);
        var message = Loc.GetString("cmu-mohawk-flare-warning");
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } entity)
                continue;
            var position = _transform.GetMapCoordinates(entity);
            if (position.MapId != center.MapId ||
                Vector2.DistanceSquared(position.Position, center.Position) > args.Radius * args.Radius)
                continue;
            _chat.DispatchServerMessage(session, message);
            _popup.PopupEntity(message, entity, entity, PopupType.LargeCaution);
        }
    }

    public override void Update(float frameTime)
    {
        foreach (var (flare, time) in _warned)
            if (TerminatingOrDeleted(flare) || _timing.CurTime - time >= TimeSpan.FromSeconds(30))
                _warned.Remove(flare);
    }
}
