using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.GameTicking;

namespace Content.Server.CMU14.ZLevelBuilding;

/// <summary>Lobby-only overrides, copied into each world's immutable generation recipe at first use.</summary>
public sealed partial class CMUCavernConfigSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private AuRoundSystem _round = default!;

    private CMUCavernSettings _settings = new();
    private int _seed;
    private bool _overridden;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Reset());
    }

    private void Reset()
    {
        _settings = new();
        _seed = 0;
        _overridden = false;
    }

    public string Status()
    {
        var status = Loc.GetString("cmu-caverns-status",
            ("seed", _seed == 0 ? "random" : _seed.ToString()), ("size", _settings.RegionSize),
            ("height", _settings.BandHeight), ("variant", _settings.Variant?.ToString() ?? "mixed"),
            ("layout", _settings.Layout?.ToString() ?? "mixed"));
        status += "\n" + DescribeBiomes();
        var worlds = EntityQueryEnumerator<CMUCavernWorldComponent>();
        while (worlds.MoveNext(out var uid, out var world))
            status += "\n" + Loc.GetString("cmu-caverns-world-seed", ("world", Name(uid)), ("seed", world.Seed));
        return status;
    }

    public bool Configure(string option, string value, string? weight, out string feedback)
    {
        feedback = Loc.GetString("cmu-caverns-invalid");
        if (weight != null && !option.Equals("biome", StringComparison.OrdinalIgnoreCase))
            return false;
        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby)
        {
            feedback = Loc.GetString("cmu-caverns-locked");
            return false;
        }
        // Admin previews may already have materialized terrain during the lobby. Never change its recipe.
        var worlds = EntityQueryEnumerator<CMUCavernWorldComponent>();
        if (worlds.MoveNext(out _, out _))
        {
            feedback = Loc.GetString("cmu-caverns-generated");
            return false;
        }

        switch (option.ToLowerInvariant())
        {
            case "reset" when value.Length == 0:
                Reset();
                break;
            case "preset" when Enum.TryParse<CMUCavernBiomePreset>(value, true, out var preset) && Enum.IsDefined(preset):
                _settings.BiomeWeights = PresetWeights(preset);
                _settings.Variant = null;
                break;
            case "biome" when Enum.TryParse<CMUCavernVariant>(value, true, out var biome) && Enum.IsDefined(biome) &&
                              int.TryParse(weight, out var amount) && amount is >= 0 and <= 1000:
                var palette = _settings.BiomeWeights == null
                    ? Enum.GetValues<CMUCavernVariant>().ToDictionary(v => v, _ => 1)
                    : new Dictionary<CMUCavernVariant, int>(_settings.BiomeWeights);
                palette[biome] = amount;
                if (!palette.Values.Any(w => w > 0))
                {
                    feedback = Loc.GetString("cmu-caverns-empty-palette");
                    return false;
                }
                _settings.BiomeWeights = palette;
                _settings.Variant = null;
                break;
            case "seed" when value == "random":
                _seed = 0;
                break;
            case "seed" when int.TryParse(value, out var seed) && seed > 0:
                _seed = seed;
                break;
            case "size" when int.TryParse(value, out var size) && size is >= 48 and <= 160:
                _settings.RegionSize = size;
                break;
            case "height" when int.TryParse(value, out var height) && height is >= 4 and <= 9:
                _settings.BandHeight = height;
                break;
            case "variant" when value == "mixed":
                _settings.Variant = null;
                break;
            case "variant" when Enum.TryParse<CMUCavernVariant>(value, true, out var variant) && Enum.IsDefined(variant):
                _settings.Variant = variant;
                break;
            case "layout" when value == "mixed":
                _settings.Layout = null;
                break;
            case "layout" when Enum.TryParse<CMUCavernLayout>(value, true, out var layout) && Enum.IsDefined(layout):
                _settings.Layout = layout;
                break;
            default:
                return false;
        }
        _overridden = !option.Equals("reset", StringComparison.OrdinalIgnoreCase);
        feedback = Status();
        return true;
    }

    public void Apply(CMUCavernWorldComponent world, CMUCavernSettings defaults)
    {
        var insurgency = _round.SelectedPreset?.ID == "Insurgency";
        var recipe = insurgency && _overridden ? _settings : defaults;
        world.Settings = new CMUCavernSettings
        {
            StartDepth = recipe.StartDepth,
            RegionSize = recipe.RegionSize,
            BandHeight = recipe.BandHeight,
            Variant = recipe.Variant,
            Layout = recipe.Layout,
            BiomeWeights = recipe.BiomeWeights == null ? null : new(recipe.BiomeWeights),
        };
        if (insurgency && _overridden)
            world.Seed = _seed;
    }
}
