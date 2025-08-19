using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Rules;
using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;
using Content.Shared.AU14.Objectives;
using Robust.Server.Player;

namespace Content.Server.AU14.Objectives;

public sealed class AuObjectiveSystem : AuSharedObjectiveSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;


    [Dependency] private readonly IEntityManager _entityManager = default!;

    public (int govforMinor, int govforMajor, int opforMinor, int opforMajor, int clfMinor, int clfMajor, int scientistMinor, int scientistMajor) ObjectivesAmount()
    {
        foreach (var comp in EntityManager.EntityQuery<ObjectiveMasterComponent>())
        {
            return (
                comp.GovforMinorObjectives,
                comp.GovforMajorObjectives,
                comp.OpforMinorObjectives,
                comp.OpforMajorObjectives,
                comp.CLFMinorObjectives,
                comp.CLFMajorObjectives,
                comp.ScientistMinorObjectives,
                comp.ScientistMajorObjectives
            );
        }
        var def = new ObjectiveMasterComponent();
        return (
            def.GovforMinorObjectives,
            def.GovforMajorObjectives,
            def.OpforMinorObjectives,
            def.OpforMajorObjectives,
            def.CLFMinorObjectives,
            def.CLFMajorObjectives,
            def.ScientistMinorObjectives,
            def.ScientistMajorObjectives
        );
    }

    private ObjectiveMasterComponent? _objectiveMaster = null;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AuObjectiveComponent, ComponentHandleState>(OnObjectiveHandleState);
    }

    private void OnObjectiveHandleState(EntityUid uid, AuObjectiveComponent component, ref ComponentHandleState args)
    {


        if (!component.Completed)
        {
            OnObjectiveCompleted(uid, component);
        }
    }

    private void OnObjectiveCompleted(EntityUid uid, AuObjectiveComponent objective)
    {
        if (_objectiveMaster == null)
            return;

        if (objective.CustomPoints == 0)
        {
            switch (objective.Faction)
            {
                case "govfor":
                    if (objective.ObjectiveLevel == 1)
                    {
                        _objectiveMaster.Winpointsgovfor -= 5;
                    }
                    else if (objective.ObjectiveLevel == 2)
                    {
                        _objectiveMaster.Winpointsgovfor -= 20;
                    }
                    break;
                case "opfor":
                    if (objective.ObjectiveLevel == 1)
                    {
                        _objectiveMaster.Winpointsopfor -= 5;
                    }
                    else if (objective.ObjectiveLevel == 2)
                    {
                        _objectiveMaster.Winpointsopfor -= 20;
                    }
                    break;
                case "clf":
                    if (objective.ObjectiveLevel == 1)
                    {
                        _objectiveMaster.Winpointsclf -= 5;
                    }
                    else if (objective.ObjectiveLevel == 2)
                    {
                        _objectiveMaster.Winpointsclf -= 20;
                    }
                    break;
                case "scientist":
                    if (objective.ObjectiveLevel == 1)
                    {
                        _objectiveMaster.Winpointsscientist -= 5;
                    }
                    else if (objective.ObjectiveLevel == 2)
                    {
                        _objectiveMaster.Winpointsscientist -= 20;
                    }
                    break;
            }
        }
        else
        {
            switch (objective.Faction)
            {
                case "govfor":
                    if (objective.ObjectiveLevel == 1)
                    {
                        _objectiveMaster.Winpointsgovfor -= objective.CustomPoints;
                    }
                    else if (objective.ObjectiveLevel == 2)
                    {
                        _objectiveMaster.Winpointsgovfor -= objective.CustomPoints;
                    }
                    break;
                case "opfor":
                    if (objective.ObjectiveLevel == 1)
                    {
                        _objectiveMaster.Winpointsopfor -= objective.CustomPoints;
                    }
                    else if (objective.ObjectiveLevel == 2)
                    {
                        _objectiveMaster.Winpointsopfor -= objective.CustomPoints;
                    }
                    break;
                case "clf":
                    if (objective.ObjectiveLevel == 1)
                    {
                        _objectiveMaster.Winpointsclf -= objective.CustomPoints;
                    }
                    else if (objective.ObjectiveLevel == 2)
                    {
                        _objectiveMaster.Winpointsclf -= objective.CustomPoints;
                    }
                    break;
                case "scientist":
                    if (objective.ObjectiveLevel == 1)
                    {
                        _objectiveMaster.Winpointsscientist -= objective.CustomPoints;
                    }
                    else if (objective.ObjectiveLevel == 2)
                    {
                        _objectiveMaster.Winpointsscientist -= objective.CustomPoints;
                    }
                    break;
            }
        }
    }


    private List<AuObjectiveComponent> GetObjectives()
            {
                var objectives = new List<AuObjectiveComponent>();
                var query = EntityQueryEnumerator<AuObjectiveComponent>();
                while (query.MoveNext(out var uid, out var comp))
                {
                    objectives.Add(comp);
                }

                return objectives;
            }



            public void Main()
            {
                var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                var presetId = ticker.Preset?.ID?.ToLowerInvariant();
                var govforMinor = new List<AuObjectiveComponent>();
                var govforMajor = new List<AuObjectiveComponent>();
                var opforMinor = new List<AuObjectiveComponent>();
                var opforMajor = new List<AuObjectiveComponent>();
                var clfMinor = new List<AuObjectiveComponent>();
                var clfMajor = new List<AuObjectiveComponent>();
                var scientistMinor = new List<AuObjectiveComponent>();
                var scientistMajor = new List<AuObjectiveComponent>();

                var allMasters = new List<ObjectiveMasterComponent>();
                foreach (var comp in EntityManager.EntityQuery<ObjectiveMasterComponent>())
                {
                    allMasters.Add(comp);
                }

                if (allMasters.Count == 0)
                {
                    _objectiveMaster = new ObjectiveMasterComponent();
                }
                else if (allMasters.Count == 1)
                {
                    _objectiveMaster = allMasters[0];
                }
                else
                {
                    // Try to find one matching the preset/mode
                    _objectiveMaster = allMasters.FirstOrDefault(m => m.Mode.ToLowerInvariant() == presetId) ??
                                      allMasters[0];
                }

                if (presetId == "insurgency")
                {
                    govforMinor = SelectObjectives("govfor", 1, _objectiveMaster);
                    govforMajor = SelectObjectives("govfor", 2, _objectiveMaster);
                    clfMinor = SelectObjectives("clf", 1, _objectiveMaster);
                    clfMajor = SelectObjectives("clf", 2, _objectiveMaster);
                }
                else if (presetId == "forceonforce")
                {
                    govforMinor = SelectObjectives("govfor", 1, _objectiveMaster);
                    govforMajor = SelectObjectives("govfor", 2, _objectiveMaster);
                    opforMinor = SelectObjectives("opfor", 1, _objectiveMaster);
                    opforMajor = SelectObjectives("opfor", 2, _objectiveMaster);
                }
                else if (presetId == "distresssignal")
                {
                    govforMinor = SelectObjectives("govfor", 1, _objectiveMaster);
                    govforMajor = SelectObjectives("govfor", 2, _objectiveMaster);
                }

                scientistMinor = SelectObjectives("scientist", 1, _objectiveMaster);
                scientistMajor = SelectObjectives("scientist", 2, _objectiveMaster);

                foreach (var obj in govforMinor)
                {
                    obj.Active = true;
                    obj.Faction = "govfor";
                }

                foreach (var obj in govforMajor)
                {
                    obj.Active = true;
                    obj.Faction = "govfor";
                }

                foreach (var obj in opforMinor)
                {
                    obj.Active = true;
                    obj.Faction = "opfor";
                }

                foreach (var obj in opforMajor)
                {
                    obj.Active = true;
                    obj.Faction = "opfor";
                }

                foreach (var obj in clfMinor)
                {
                    obj.Active = true;
                    obj.Faction = "clf";
                }

                foreach (var obj in clfMajor)
                {
                    obj.Active = true;
                    obj.Faction = "clf";
                }

                foreach (var obj in scientistMinor)
                {
                    obj.Active = true;
                    obj.Faction = "scientist";
                }
                foreach (var obj in scientistMajor)
                {
                    obj.Active = true;
                    obj.Faction = "scientist";
                }



            }

            private List<AuObjectiveComponent> SelectObjectives(string faction,
                int? objectiveLevel = null,
                ObjectiveMasterComponent? objectiveMaster = null)
            {

                var playercount = _playerManager.PlayerCount;
                var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                var presetId = ticker.Preset?.ID;


                return GetObjectives()
                    .Where(objective =>
                        objective.ApplicableModes.Contains(presetId?.ToString() ?? string.Empty)
                        && objective.Factions.Contains(faction)
                        && (objective.Maxplayers == 0 || objective.Maxplayers >= playercount)
                        && (objectiveLevel == null
                            ? (objective.ObjectiveLevel == 1 || objective.ObjectiveLevel == 2)
                            : (objective.ObjectiveLevel == objectiveLevel))
                    )
                    .ToList();
            }
        }
