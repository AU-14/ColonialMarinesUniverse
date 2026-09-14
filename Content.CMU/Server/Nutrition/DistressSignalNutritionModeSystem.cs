using Content.Server.GameTicking;
using Content.Shared.CMU14.Nutrition;
using Content.Shared.GameTicking;
using Content.Shared.Nutrition.Components;

namespace Content.Server.CMU14.Nutrition;

/// <summary>
/// Applies Distress Signal's slower hunger and thirst pacing to spawned players.
/// </summary>
public sealed partial class DistressSignalNutritionModeSystem : EntitySystem
{
    private const string DistressSignalPreset = "DistressSignal";

    [Dependency] private GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        var preset = _gameTicker.CurrentPreset ?? _gameTicker.Preset;
        if (preset?.ID != DistressSignalPreset || !HasComp<SatiationComponent>(args.Mob))
            return;

        EnsureComp<DistressSignalNutritionComponent>(args.Mob);
    }
}
