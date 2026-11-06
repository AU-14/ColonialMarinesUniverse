using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether the Early Warning Joe can be rolled. Off: its game rules end before picking anyone and an untaken
    /// Distress Signal ghost role is withdrawn; Joes already in the round keep playing. Toggled with the
    /// ewjoemechanics admin command.
    /// </summary>
    public static readonly CVarDef<bool> CMUEWJoeEnabled =
        CVarDef.Create("cmu.ew_joe.enabled", true, CVar.SERVER);
}
