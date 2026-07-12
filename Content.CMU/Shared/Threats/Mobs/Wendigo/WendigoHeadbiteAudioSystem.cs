using Content.Shared._RMC14.Xenonids.Headbite;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Threats.Mobs.Wendigo;

public sealed partial class WendigoHeadbiteAudioSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WendigoHeadbiteAudioComponent, XenoHeadbiteDoAfterEvent>(OnHeadbiteDoAfter,
            after: [typeof(XenoHeadbiteSystem)]);

        SubscribeNetworkEvent<WendigoScreechEvent>(OnScreechEvent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        EntityQueryEnumerator<WendigoHeadbiteAudioComponent> query
            = EntityQueryEnumerator<WendigoHeadbiteAudioComponent>();
        while (query.MoveNext(out EntityUid uid, out WendigoHeadbiteAudioComponent? comp))
        {
            if (comp.ScreechReady)
                continue;

            if (comp.LastGlobalPlayed == null || _timing.CurTime >= comp.LastGlobalPlayed.Value + comp.GlobalCooldown)
            {
                comp.ScreechReady = true;
                Dirty(uid, comp);
                _popup.PopupEntity(Loc.GetString("rmc-wendigo-screech-ready"), uid, uid, PopupType.Medium);
            }
        }
    }

    private void OnHeadbiteDoAfter(Entity<WendigoHeadbiteAudioComponent> ent, ref XenoHeadbiteDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;

        if (!_net.IsServer)
            return;

        bool globalReady = IsGlobalReady(ent);

        if (globalReady && ent.Comp.GlobalSound != null)
        {
            EntityCoordinates coords = _transform.GetMoverCoordinates(ent);
            NetCoordinates netCoords = GetNetCoordinates(coords);

            AudioParams outdoorParams = AudioParams.Default
                .WithMaxDistance(5000f)
                .WithRolloffFactor(0)
                .WithVolume(ent.Comp.GlobalVolume);

            AudioParams indoorParams = AudioParams.Default
                .WithMaxDistance(5000f)
                .WithRolloffFactor(0)
                .WithVolume(ent.Comp.GlobalIndoorVolume);

            foreach (ICommonSession session in Filter.Broadcast().Recipients)
            {
                if (session.AttachedEntity is not { } player)
                    continue;

                AudioParams audioParams = IsEntityRoofed(player) ? indoorParams : outdoorParams;
                RaiseNetworkEvent(new WendigoScreechEvent(ent.Comp.GlobalSound, audioParams, netCoords), session);
            }

            ent.Comp.LastGlobalPlayed = _timing.CurTime;
            ent.Comp.ScreechReady = false;
            Dirty(ent);
        }
        else if (ent.Comp.CloseSound != null) _audio.PlayPvs(ent.Comp.CloseSound, ent);
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

    private bool IsEntityRoofed(EntityUid entity)
    {
        TransformComponent xform = Transform(entity);

        if (xform.GridUid is not { } gridUid)
            return false;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return false;

        if (!TryComp(gridUid, out RoofComponent? roof))
            return false;

        Vector2i indices = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        return _roof.IsRooved((gridUid, grid, roof), indices);
    }
}
