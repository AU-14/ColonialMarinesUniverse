using Content.Shared.CMU14.Audio;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Server._RMC14.Vehicle;

public sealed partial class VehicleAudioRelaySystem
{
    [Dependency] private CMUSharedAudioBudgetSystem _cmuBudget = default!;

    /// <summary>Relay an explicitly public transient without requiring an unfiltered source entity.</summary>
    public void RelayCMUTransientSound(ResolvedSoundSpecifier sound, AudioParams parameters,
        EntityUid source, EntityUid? excluded)
    {
        var coordinates = Transform(source).Coordinates;
        var soundMap = _transform.GetMapId(coordinates);
        if (soundMap == MapId.Nullspace)
            return;

        var maxDistance = parameters.MaxDistance;
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

            RelayToOccupants(interior, sound, parameters, distance, maxDistance, excluded);
        }
    }

}
