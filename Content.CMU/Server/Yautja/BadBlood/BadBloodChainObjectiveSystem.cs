using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Round.Objectives;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.Database;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// COUPLING-501: shows the marine objective chain against the Bad Blood clan on the GOVFOR objectives console, and
/// keeps win points from handing GOVFOR a level-3 <c>InstantWin</c> objective while the clan's rule decides the round.
/// <para>
/// On rule start it suppresses GOVFOR's final objective and puts four level-1 <c>Boon</c> objectives on the planet
/// (gate, signature, uplink, strike), switched on at once. Each completes, without paying points, when the chain
/// raises the matching <see cref="BadBloodObjectiveStageEvent"/>. Modelled on
/// <see cref="BadBloodTotemObjectiveSystem.EnsureObjective"/>.
/// </para>
/// <para>
/// Verify-first (COUPLING-501): the round-start roll (<c>ObjectiveControlSystem.Main</c>) runs once, and only over
/// objectives that are inactive. These are activated as soon as they spawn, so the roll never takes them and they never
/// use a GOVFOR minor-objective slot, whether the rule starts before or after the roll.
/// </para>
/// </summary>
public sealed class BadBloodChainObjectiveSystem : EntitySystem
{
    public const string Faction = "govfor";

    public static readonly EntProtoId GateObjective = "CMUBadBloodGateObjective";
    public static readonly EntProtoId SignatureObjective = "CMUBadBloodSignatureObjective";
    public static readonly EntProtoId UplinkObjective = "CMUBadBloodUplinkObjective";
    public static readonly EntProtoId StrikeObjective = "CMUBadBloodStrikeObjective";

    public static readonly EntProtoId[] Objectives = [GateObjective, SignatureObjective, UplinkObjective, StrikeObjective];

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ObjectiveControlSystem _objectives = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodRuleStartedEvent>(OnRuleStarted);
        SubscribeLocalEvent<BadBloodObjectiveStageEvent>(OnStage);
    }

    private void OnRuleStarted(ref BadBloodRuleStartedEvent args)
    {
        _objectives.SuppressFinalObjective(Faction);
        EnsureObjectives();
    }

    private void OnStage(ref BadBloodObjectiveStageEvent args)
    {
        if (ObjectiveFor(args.Stage) is not { } proto)
            return;

        if (!TryGetObjective(proto, out var objective))
        {
            // the rule started before the planet was known: put it there now
            if (!EnsureObjective(proto) || !TryGetObjective(proto, out objective))
                return;
        }

        if (IsCompleted(objective.Comp))
            return;

        _objectives.CompleteObjectiveForFaction(objective.Owner, objective.Comp, Faction, awardPoints: false);
        if (!IsCompleted(objective.Comp))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"BadBlood obj: GOVFOR objective {proto.Id} completed on stage {args.Stage} ({args.Detail})");
    }

    /// <summary>
    /// The objective a stage completes, or null for a stage with no objective.
    /// </summary>
    public static EntProtoId? ObjectiveFor(BadBloodObjectiveStage stage)
    {
        return stage switch
        {
            BadBloodObjectiveStage.GateMet => (EntProtoId?) GateObjective,
            BadBloodObjectiveStage.Signature => SignatureObjective,
            BadBloodObjectiveStage.Uplink => UplinkObjective,
            // completes when the strike is fired, so marines see it: StrikeCompleted ends the round in the same tick
            BadBloodObjectiveStage.StrikeStarted => StrikeObjective,
            _ => null,
        };
    }

    /// <summary>
    /// Makes sure all four chain objectives are on the round's planet and active. Returns how many are.
    /// </summary>
    public int EnsureObjectives()
    {
        var count = 0;
        foreach (var proto in Objectives)
        {
            if (EnsureObjective(proto))
                count++;
        }

        return count;
    }

    /// <summary>
    /// The chain objective <paramref name="proto"/> on the round's planet, if it is there.
    /// </summary>
    public bool TryGetObjective(EntProtoId proto, out Entity<CMUObjectiveComponent> objective)
    {
        objective = default;
        if (_objectives.GetPlanetMapId() is not { } planet || planet == MapId.Nullspace)
            return false;

        var query = EntityQueryEnumerator<CMUObjectiveComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var meta, out var xform))
        {
            if (xform.MapID != planet || meta.EntityPrototype?.ID != proto.Id || TerminatingOrDeleted(uid))
                continue;

            objective = (uid, comp);
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when GOVFOR has completed the chain objective <paramref name="proto"/>.
    /// </summary>
    public bool IsObjectiveCompleted(EntProtoId proto)
    {
        return TryGetObjective(proto, out var objective) && IsCompleted(objective.Comp);
    }

    private bool EnsureObjective(EntProtoId proto)
    {
        if (_objectives.GetPlanetMapId() is not { } planet || planet == MapId.Nullspace)
            return false;

        if (!TryGetObjective(proto, out var objective))
        {
            var uid = Spawn(proto, new MapCoordinates(Vector2.Zero, planet));
            if (!TryComp(uid, out CMUObjectiveComponent? comp))
                return false;

            objective = (uid, comp);
        }

        if (objective.Comp.Active)
            return true;

        // the activation below does not pick a faction, so the console would not list it without this
        if (!objective.Comp.FactionNeutral && string.IsNullOrEmpty(objective.Comp.Faction) && objective.Comp.Factions.Count > 0)
            objective.Comp.Faction = objective.Comp.Factions[0];

        _objectives.ActivateInactiveObjectives(proto.Id);
        return objective.Comp.Active;
    }

    private static bool IsCompleted(CMUObjectiveComponent objective)
    {
        return objective.StatusesPerFaction.TryGetValue(Faction, out var status) &&
               status == CMUObjectiveComponent.ObjectiveStatus.Completed;
    }
}
