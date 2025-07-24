using Content.Server.GameTicking;
using Content.Server.AU14.VendorMarker;
using Content.Server.Chat.Managers;
using Content.Shared.AU14;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Shared.GameTicking.Components;

namespace Content.Server.AU14.Round;

public sealed class PlatoonSpawnRuleSystem : GameRuleSystem<PlatoonSpawnRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameMapManager _gameMapManager = default!;

    // Store selected platoons in the system
    public PlatoonPrototype? SelectedGovforPlatoon { get; set; }
    public PlatoonPrototype? SelectedOpforPlatoon { get; set; }

    protected override void Started(EntityUid uid, PlatoonSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Get selected platoons from the system
        var govPlatoon = SelectedGovforPlatoon;
        var opPlatoon = SelectedOpforPlatoon;

        // Fallback to default platoon if none selected
        var selectedMap = _gameMapManager.GetSelectedMap();
        if (selectedMap != null)
        {
            if (govPlatoon == null && !string.IsNullOrEmpty(selectedMap.DefaultGovforPlatoon))
                govPlatoon = _prototypeManager.Index<PlatoonPrototype>(selectedMap.DefaultGovforPlatoon);
            if (opPlatoon == null && !string.IsNullOrEmpty(selectedMap.DefaultOpforPlatoon))
                opPlatoon = _prototypeManager.Index<PlatoonPrototype>(selectedMap.DefaultOpforPlatoon);
        }

        // Find all vendor markers in the map
        var query = _entityManager.EntityQuery<VendorMarkerComponent>(true);
        foreach (var marker in query)
        {


            var markerClass = marker.Class;
            var markerUid = marker.Owner;
            var transform = _entityManager.GetComponent<TransformComponent>(markerUid);

            // Determine which platoon to use
            PlatoonPrototype? platoon = null;
            if (marker.Govfor && govPlatoon != null)
                platoon = govPlatoon;
            else if (marker.Opfor && opPlatoon != null)
                platoon = opPlatoon;
            else
                continue;

            // Get vendor prototype for this marker class
            if (!platoon.VendorMarkersByClass.TryGetValue(markerClass, out var vendorProtoId))
                continue;

            if (!_prototypeManager.TryIndex<EntityPrototype>(vendorProtoId, out var vendorProto))
                continue;

            // Spawn vendor at marker location
            _entityManager.SpawnEntity(vendorProto.ID, transform.Coordinates);
        }
    }
}
