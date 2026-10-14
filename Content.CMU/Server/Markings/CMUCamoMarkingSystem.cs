using Content.Server.Humanoid;
using Content.Shared._RMC14.Item;
using Content.Shared.CMU14.Markings;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Markings;

/// <summary>
///     Swaps camo conforming underwear for the version matching the map's camouflage when a player spawns.
/// </summary>
public sealed class CMUCamoMarkingSystem : EntitySystem
{
    private static readonly HumanoidVisualLayers[] Layers =
    [
        HumanoidVisualLayers.UndergarmentTop,
        HumanoidVisualLayers.UndergarmentBottom,
    ];

    [Dependency] private HumanoidOrganAppearanceSystem _appearance = default!;
    [Dependency] private ItemCamouflageSystem _camouflage = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        ApplyCamo(args.Mob);
    }

    public void ApplyCamo(EntityUid body)
    {
        var camo = _camouflage.CurrentMapCamouflage;
        foreach (var layer in Layers)
        {
            if (!_appearance.TryGetMarkings(body, layer, out var organ, out _, out var markings))
                continue;

            var changed = false;
            for (var i = 0; i < markings.Count; i++)
            {
                if (!_prototypes.TryIndex<CMUCamoMarkingPrototype>(markings[i].MarkingId.Id, out var camoMarking) ||
                    !camoMarking.Variants.TryGetValue(camo, out var variant))
                {
                    continue;
                }

                markings[i] = new Marking(variant, markings[i].MarkingColors);
                changed = true;
            }

            if (changed)
                _appearance.SetMarkings(body, organ, layer, markings);
        }
    }
}
