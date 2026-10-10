using System;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Vehicle;

public sealed class VehicleAudioRelaySystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float MuffleVolumeDb = -8f;
    private const float MaxFalloffDb = -20f;

    // CMU14: Z-level audio owns AudioComponent's move subscription. Queue its sources
    // until the audio API has finished setting global/private audience restrictions.
    private readonly HashSet<EntityUid> _pending = new();
    private readonly List<EntityUid> _buffer = new();

    public void QueueSound(EntityUid uid) => _pending.Add(uid);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _buffer.Clear();
        _buffer.AddRange(_pending);
        _pending.Clear();
        foreach (var uid in _buffer)
        {
            if (!TerminatingOrDeleted(uid) && TryComp(uid, out AudioComponent? audio))
                RelaySound((uid, audio));
        }
    }

    private void RelaySound(Entity<AudioComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.Global || comp.IncludedEntities != null || comp.Params.Loop || comp.FileName.Length == 0)
            return;

        var coordinates = Transform(ent).Coordinates;
        var soundMap = _transform.GetMapId(coordinates);
        if (soundMap == MapId.Nullspace)
            return;

        var maxDistance = comp.Params.MaxDistance;
        if (maxDistance <= 0f)
            maxDistance = AudioParams.Default.MaxDistance;

        var links = EntityQueryEnumerator<VehicleInteriorLinkComponent>();
        while (links.MoveNext(out _, out var link))
        {
            var vehicle = link.Vehicle;
            if (Deleted(vehicle) || !TryComp(vehicle, out VehicleInteriorComponent? interior))
                continue;

            if (interior.Passengers.Count == 0 && interior.Xenos.Count == 0)
                continue;

            if (!TryComp(vehicle, out TransformComponent? vehicleXform) || vehicleXform.MapID != soundMap)
                continue;

            if (!coordinates.TryDistance(EntityManager, _transform, vehicleXform.Coordinates, out var distance) ||
                distance > maxDistance)
            {
                continue;
            }

            RelayToOccupants(interior, comp, distance, maxDistance);
        }
    }

    private void RelayToOccupants(VehicleInteriorComponent interior, AudioComponent source, float distance, float maxDistance)
    {
        var specifier = new ResolvedPathSpecifier(source.FileName);
        var falloff = Math.Clamp(distance / maxDistance, 0f, 1f) * MaxFalloffDb;
        var sourceParams = source.Params;
        var audioParams = sourceParams.WithVolume(sourceParams.Volume + falloff + MuffleVolumeDb);

        foreach (var occupant in interior.Passengers)
        {
            RelayToOccupant(occupant, specifier, audioParams);
        }

        foreach (var occupant in interior.Xenos)
        {
            RelayToOccupant(occupant, specifier, audioParams);
        }
    }

    private void RelayToOccupant(EntityUid occupant, ResolvedSoundSpecifier specifier, AudioParams audioParams)
    {
        if (TerminatingOrDeleted(occupant) ||
            !TryComp(occupant, out ActorComponent? actor) ||
            IsListeningOutside(occupant))
        {
            return;
        }

        _audio.PlayGlobal(specifier, actor.PlayerSession, audioParams);
    }

    private bool IsListeningOutside(EntityUid occupant)
    {
        return TryComp(occupant, out VehicleViewToggleComponent? toggle) && toggle.IsOutside;
    }
}
