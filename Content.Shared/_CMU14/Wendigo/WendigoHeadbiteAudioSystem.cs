using Content.Shared._RMC14.Xenonids.Headbite;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Wendigo;

public sealed class WendigoHeadbiteAudioSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedRoofSystem _roof = default!;

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

        // Always play close sound for nearby players.
        if (ent.Comp.CloseSound != null)
            _audio.PlayPvs(ent.Comp.CloseSound, ent);

        // Play directional global sound for distant players if cooldown has expired.
        if (ent.Comp.GlobalSound != null)
        {
            var now = _timing.CurTime;
            if (ent.Comp.LastGlobalPlayed != null &&
                now < ent.Comp.LastGlobalPlayed.Value + ent.Comp.GlobalCooldown)
            {
                return;
            }

            var pvsPlayers = new HashSet<ICommonSession>(Filter.Pvs(ent).Recipients);
            var wendigoCoords = _transform.GetMoverCoordinates(ent);

            // No distance attenuation so everyone hears it, but positioned for directionality.
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
                if (pvsPlayers.Contains(session))
                    continue;

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

            ent.Comp.LastGlobalPlayed = now;
            Dirty(ent);
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
