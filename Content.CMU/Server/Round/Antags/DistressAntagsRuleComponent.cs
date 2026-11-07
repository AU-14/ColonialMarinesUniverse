using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags;

/// <summary>
/// Rolls a per-round weighted coin flip for each antag rule in a table and adds the rules that win.
/// </summary>
[RegisterComponent]
public sealed partial class DistressAntagsRuleComponent : Component
{
    /// <summary>
    /// This preset's roll table: game rule prototype to the chance it is added this round.
    /// Null uses <see cref="DistressAntagsRuleSystem.AntagRulePrototypes"/> (the Distress Signal table).
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, float>? Rules;
}
