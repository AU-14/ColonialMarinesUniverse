using Content.Server.GameTicking;
using Content.Server.AU14.VendorMarker;
using Content.Server.Chat.Managers;
using Content.Shared.AU14;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Shared._RMC14.Rules;
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

        // Fetch the selected planet entity and its RMCPlanetMapPrototypeComponent
        EntityUid? selectedPlanet = null;
        RMCPlanetMapPrototypeComponent? planetComp = null;
        foreach (var ent in _entityManager.EntityQuery<RMCPlanetMapPrototypeComponent>())
        {
            selectedPlanet = ent.Owner;
            planetComp = ent;
            break; // Assume only one planet is active/selected
        }

        // Fallback to default platoon if none selected, using planet component
        if (planetComp != null)
        {
            if (govPlatoon == null && !string.IsNullOrEmpty(planetComp.DefaultGovforPlatoon))
                govPlatoon = _prototypeManager.Index<PlatoonPrototype>(planetComp.DefaultGovforPlatoon);
            if (opPlatoon == null && !string.IsNullOrEmpty(planetComp.DefaultOpforPlatoon))
                opPlatoon = _prototypeManager.Index<PlatoonPrototype>(planetComp.DefaultOpforPlatoon);
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
