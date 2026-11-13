using System.Numerics;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Round.Objectives;
using Content.Server.CMU14.Threats;
using Content.Shared.CMU14.Round.Objectives.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-119: the marines' "destroy the Clan Totems" objective. Objectives are rolled at random, so on a Bad Blood
/// round this puts the objective on the planet itself and switches it on, when the threat is selected and again
/// when the clan's rule starts. The objective pays only for a Totem that is really destroyed
/// (<c>requireDestruction</c>), so a Totem that is merely deleted pays nobody.
/// </summary>
public sealed partial class BadBloodTotemObjectiveSystem : EntitySystem
{
    public static readonly EntProtoId ObjectivePrototype = "CMUBadBloodTotemObjective";
    private static readonly EntProtoId RulePrototype = "CMUBadBloodCampRule";

    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private ObjectiveControlSystem _objectives = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ThreatSelectedEvent>(OnThreatSelected);
        SubscribeLocalEvent<BadBloodRuleStartedEvent>(OnRuleStarted);
    }

    private void OnThreatSelected(ref ThreatSelectedEvent args)
    {
        if (_auRound.SelectedThreat is { } threat && threat.WinConditions.Contains(RulePrototype.Id))
            EnsureObjective();
    }

    private void OnRuleStarted(ref BadBloodRuleStartedEvent args)
    {
        EnsureObjective();
    }

    /// <summary>
    /// The Totem objective on the round's planet, if it is there.
    /// </summary>
    public bool TryGetObjective(out Entity<CMUObjectiveComponent> objective)
    {
        objective = default;
        if (_objectives.GetPlanetMapId() is not { } planet || planet == MapId.Nullspace)
            return false;

        return TryGetObjective(planet, out objective);
    }

    private bool TryGetObjective(MapId map, out Entity<CMUObjectiveComponent> objective)
    {
        var query = EntityQueryEnumerator<CMUObjectiveComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var meta, out var xform))
        {
            if (xform.MapID != map || meta.EntityPrototype?.ID != ObjectivePrototype.Id || TerminatingOrDeleted(uid))
                continue;

            objective = (uid, comp);
            return true;
        }

        objective = default;
        return false;
    }

    /// <summary>
    /// Makes sure the Totem objective exists on the round's planet and is active. False when the round has no
    /// planet yet, or the objective would not activate. Calling it again changes nothing.
    /// </summary>
    public bool EnsureObjective()
    {
        if (_objectives.GetPlanetMapId() is not { } planet || planet == MapId.Nullspace)
            return false;

        if (!TryGetObjective(planet, out var objective))
        {
            var uid = Spawn(ObjectivePrototype, new MapCoordinates(Vector2.Zero, planet));
            if (!TryComp(uid, out CMUObjectiveComponent? comp))
                return false;

            objective = (uid, comp);
        }

        if (objective.Comp.Active)
            return true;

        // used up: the marines have already been paid for every repeat
        if (objective.Comp.MaxRepeatable is { } max && objective.Comp.TimesCompleted >= max)
            return false;

        // the activation below is the admin one and does not pick a faction, so the credit goes nowhere without this
        if (!objective.Comp.FactionNeutral && string.IsNullOrEmpty(objective.Comp.Faction) && objective.Comp.Factions.Count > 0)
            objective.Comp.Faction = objective.Comp.Factions[0];

        _objectives.ActivateInactiveObjectives(ObjectivePrototype.Id);
        return objective.Comp.Active;
    }
}
