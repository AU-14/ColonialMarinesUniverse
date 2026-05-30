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
    }

    private void OnHeadbiteDoAfter(Entity<WendigoHeadbiteAudioComponent> ent, ref XenoHeadbiteDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;

        if (!_net.IsServer)
            return;

        var globalReady = IsGlobalReady(ent);

        // Play global directional screech if off cooldown; otherwise play close sound.
        if (globalReady && ent.Comp.GlobalSound != null)
        {
            var wendigoCoords = _transform.GetMoverCoordinates(ent);

            var outdoorParams = AudioParams.Default
                .WithMaxDistance(float.MaxValue)
                .WithRolloffFactor(0)
                .WithVolume(ent.Comp.GlobalVolume);

            var indoorParams = AudioParams.Default
                .WithMaxDistance(float.MaxValue)
                .WithRolloffFactor(0)
                .WithVolume(ent.Comp.GlobalIndoorVolume);

            var outdoorFilter = Filter.Empty();
            var indoorFilter = Filter.Empty();

            foreach (var session in Filter.Broadcast().Recipients)
            {
                if (session.AttachedEntity is not { } player)
                    continue;

                if (IsEntityRoofed(player))
                    indoorFilter.AddPlayer(session);
                else
                    outdoorFilter.AddPlayer(session);
            }

            if (outdoorFilter.Count > 0)
                _audio.PlayStatic(ent.Comp.GlobalSound, outdoorFilter, wendigoCoords, true, outdoorParams);

            if (indoorFilter.Count > 0)
                _audio.PlayStatic(ent.Comp.GlobalSound, indoorFilter, wendigoCoords, true, indoorParams);

            ent.Comp.LastGlobalPlayed = _timing.CurTime;
            ent.Comp.ScreechReady = false;
            Dirty(ent);
        }
        else if (ent.Comp.CloseSound != null)
        {
            _audio.PlayPvs(ent.Comp.CloseSound, ent);
        }
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
