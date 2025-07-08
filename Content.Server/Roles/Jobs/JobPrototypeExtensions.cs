using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared.Roles;

namespace Content.Server.Roles.Jobs;

public static class JobPrototypeExtensions
{
    public static string GetGamemodeName(this JobPrototype job, GameTicker ticker)
    {
        var id = ticker.CurrentPreset?.ID ?? ticker.Preset?.ID;
        return job.GetGamemodeName(id);
    }

    public static string GetGamemodeName(this JobPrototype job, GamePresetPrototype? preset)
    {
        return job.GetGamemodeName(preset?.ID);
    }
}
