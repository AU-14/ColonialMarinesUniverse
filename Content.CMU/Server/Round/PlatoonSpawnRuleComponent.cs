using System.Threading;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.CMU14;

namespace Content.Server.CMU14.Round;

/// <summary>
/// Gamerule that ends the round after a period of inactivity.
/// </summary>
[RegisterComponent, Access(typeof(PlatoonSpawnRuleSystem))]
public sealed partial class PlatoonSpawnRuleComponent : Component
{
}
