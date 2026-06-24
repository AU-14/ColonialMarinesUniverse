using Content.Shared._RMC14.Xenonids.Headbite;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Wendigo;

public sealed partial class WendigoHeadbiteAudioSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WendigoHeadbiteAudioComponent, XenoHeadbiteDoAfterEvent>(
            OnHeadbiteDoAfter,
            after: [typeof(XenoHeadbiteSystem)]);

        SubscribeNetworkEvent<WendigoScreechEvent>(OnScreechEvent);
    }

    private void OnHeadbiteDoAfter(Entity<WendigoHeadbiteAudioComponent> ent, ref XenoHeadbiteDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;

        if (!_net.IsServer)
            return;

        var globalReady = IsGlobalReady(ent);

        if (globalReady && ent.Comp.GlobalSound != null)
        {
            var coords = _transform.GetMoverCoordinates(ent);
            var netCoords = GetNetCoordinates(coords);

            var outdoorParams = AudioParams.Default
                .WithMaxDistance(5000f)
                .WithRolloffFactor(0)
                .WithVolume(ent.Comp.GlobalVolume);

            var indoorParams = AudioParams.Default
                .WithMaxDistance(5000f)
                .WithRolloffFactor(0)
                .WithVolume(ent.Comp.GlobalIndoorVolume);

            foreach (var session in Filter.Broadcast().Recipients)
            {
                if (session.AttachedEntity is not { } player)
                    continue;

                var audioParams = IsEntityRoofed(player) ? indoorParams : outdoorParams;
                RaiseNetworkEvent(new WendigoScreechEvent(ent.Comp.GlobalSound, audioParams, netCoords), session);
            }

            ent.Comp.LastGlobalPlayed = _timing.CurTime;
            ent.Comp.ScreechReady = false;
            Dirty(ent);
        }
        else if (ent.Comp.CloseSound != null)
        {
            _audio.PlayPvs(ent.Comp.CloseSound, ent);
        }
    }

    private void OnScreechEvent(WendigoScreechEvent ev)
    {
        if (_net.IsServer)
            return;

        _audio.PlayStatic(ev.Sound, Filter.Local(), GetCoordinates(ev.Coordinates), true, ev.AudioParams);
    }

    private bool IsGlobalReady(Entity<WendigoHeadbiteAudioComponent> ent)
    {
        if (ent.Comp.LastGlobalPlayed == null)
            return true;

        return _timing.CurTime >= ent.Comp.LastGlobalPlayed.Value + ent.Comp.GlobalCooldown;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<WendigoHeadbiteAudioComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ScreechReady)
                continue;

            if (comp.LastGlobalPlayed == null ||
                _timing.CurTime >= comp.LastGlobalPlayed.Value + comp.GlobalCooldown)
            {
                comp.ScreechReady = true;
                Dirty(uid, comp);
                _popup.PopupEntity(Loc.GetString("rmc-wendigo-screech-ready"), uid, uid, PopupType.Medium);
            }
        }
    }

    private bool IsEntityRoofed(EntityUid entity)
    {
        var xform = Transform(entity);

        if (xform.GridUid is not { } gridUid)
            return false;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        if (!TryComp<RoofComponent>(gridUid, out var roof))
            return false;

        var indices = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        return _roof.IsRooved((gridUid, grid, roof), indices);
    }
}
