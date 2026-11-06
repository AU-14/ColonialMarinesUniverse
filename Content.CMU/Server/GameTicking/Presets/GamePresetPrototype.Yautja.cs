// ReSharper disable CheckNamespace
namespace Content.Server.GameTicking.Presets;

public sealed partial class GamePresetPrototype
{
    // off for modes where a stranded pred on the planet makes no sense (colony fall)
    [DataField]
    public bool StrandedYautjaEnabled = true;
}
