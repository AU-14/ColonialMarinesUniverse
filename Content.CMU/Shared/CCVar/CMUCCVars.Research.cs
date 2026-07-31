using Robust.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// How long it will take for the research data terminals to show new chems after one has been picked.
    /// </summary>
    public static readonly CVarDef<float> PickWaitTime =
        CVarDef.Create("cmu.research.picktime", 360f, CVar.SERVERONLY | CVar.ARCHIVE);
    /// <summary>
    /// How long it will take for the research data terminals to show new chems.
    /// </summary>
    public static readonly CVarDef<float> RefreshTime =
        CVarDef.Create("cmu.research.refreshtime", 180f, CVar.SERVERONLY | CVar.ARCHIVE);
    /// <summary>
    /// How many different contracts are available to pick on the data terminals.
    /// </summary>
    public static readonly CVarDef<int> TerminalChems =
        CVarDef.Create("cmu.research.terminalchems", 6, CVar.SERVERONLY | CVar.ARCHIVE);
    /// <summary>
    /// How much money is rewarded per research credit when a chemical is scanned.
    /// </summary>
    public static readonly CVarDef<float> CashRewardMult =
        CVarDef.Create("cmu.research.cashrewardmult", 500f, CVar.SERVERONLY | CVar.ARCHIVE);
}
