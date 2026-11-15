using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Objectives.Prototypes;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CharacterInfo;

/// <summary>
/// CMU14 (Early Warning Joe, playtest 3): the Joe's objective ladder has its own panel, fed by
/// <see cref="EWJoeStatusComponent"/>, so the character window's upstream objective list skips it.
/// The objectives stay on the mind for the ladder, the round-end summary and admin tools.
/// </summary>
public sealed partial class CharacterInfoSystem
{
    private static readonly ProtoId<ObjectiveIssuerPrototype> CMUEarlyWarningIssuer = "GrandMachine";

    private bool CMUHideObjective(EntityUid entity, EntityUid objective)
    {
        // by issuer, not by the viewing body: a Joe looking from a ghost must not see the ladder either
        return TryComp(objective, out ObjectiveComponent? comp)
            && comp.Issuer == CMUEarlyWarningIssuer;
    }
}
