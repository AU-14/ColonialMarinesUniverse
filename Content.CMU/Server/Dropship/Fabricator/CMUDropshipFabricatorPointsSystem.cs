using Content.Server.GameTicking;
using Content.Shared.CMU14.Dropship.Fabricator;

namespace Content.Server.CMU14.Dropship.Fabricator;

public sealed class CMUDropshipFabricatorPointsSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGetDropshipFabricatorStartingPointsEvent>(OnGetStartingPoints);
    }

    private void OnGetStartingPoints(ref CMUGetDropshipFabricatorStartingPointsEvent ev)
    {
        var preset = _gameTicker.CurrentPreset ?? _gameTicker.Preset;
        if (preset?.DropshipFabricatorStartingPoints is { } points)
            ev.Points = points;
    }
}
