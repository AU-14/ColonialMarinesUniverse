// ReSharper disable CheckNamespace
namespace Content.Server.GameTicking.Presets;

public sealed partial class GamePresetPrototype
{
    // opt-in: only distress signal and force on force open the stranded slot
    [DataField]
    public bool StrandedYautjaEnabled;
}
