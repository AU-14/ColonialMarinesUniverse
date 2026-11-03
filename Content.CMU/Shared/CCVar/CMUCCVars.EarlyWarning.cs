using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Enables the Early Warning Joe. Off: the antag is not rolled or made, and the Joes already in the round are
    /// suspended (actions greyed out, directives paused, no respawn, no bullet resonance) until it is back on.
    /// Replicated because the resonance check runs on clients too. Toggled with the ewjoemechanics admin command.
    /// </summary>
    public static readonly CVarDef<bool> CMUEWJoeEnabled =
        CVarDef.Create("cmu.ew_joe.enabled", true, CVar.SERVER | CVar.REPLICATED);
}
