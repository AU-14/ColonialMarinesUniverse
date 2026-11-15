using Content.Shared.Chemistry.Reagent;

namespace Content.Server.CMU14.Chemistry.Reagents;

public sealed partial class ServerReagentGeneratorSystem
{
    private string GetReactionIndicatorReport(ReagentPrototype reagent)
    {
        if (reagent.Intensity <= 0 || reagent.Duration <= 0 || reagent.Radius <= 0)
        {
            return Loc.GetString("research-report-reaction-header") +
                   "\nDATABASE ERROR. \nWY_FTP RETURN CODE 0b1000100110\n";
        }

        // These are physical fuel measurements, not transferable metabolism properties.
        // Keep them out of GeneratedReagentData and the simulator's property library.
        return Loc.GetString("cmu-research-report-fuel-profile",
                   ("intensity", reagent.Intensity),
                   ("duration", reagent.Duration),
                   ("radius", reagent.Radius),
                   ("intensityMod", reagent.IntensityMod),
                   ("durationMod", reagent.DurationMod),
                   ("radiusMod", reagent.RadiusMod)) + '\n';
    }
}
