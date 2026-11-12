// ReSharper disable CheckNamespace
namespace Content.Server.GameTicking.Presets;

public sealed partial class GamePresetPrototype
{
    // off = no ANPRC direction finding this round: no fixed-net bearings, no jammer fixes.
    // sweep, intercept and key breaking still work
    [DataField]
    public bool AnprcDirectionFinding = true;
}
