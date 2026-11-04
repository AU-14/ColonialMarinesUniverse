using System.Linq;
using Content.Server.CMU14.Chemistry.Reagents;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Yautja;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Chemistry.Reagent;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Yautja;

public sealed partial class CMUYautjaExperimentSystem : EntitySystem
{
    public const string SampleWell = "cmuExperiment";
    private static readonly EntProtoId<SkillDefinitionComponent> ResearchSkill = "RMCSkillResearch";
    private static readonly VerbCategory Experiments = new("cmu-yautja-experiment-category", null);
    private static readonly ProtoId<CMUYautjaExperimentPrototype> Overload = "CMUYautjaStatic";
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private CMUYautjaEffectsSystem _effects = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private ServerReagentGeneratorSystem _generator = default!;
    [Dependency] private SharedResearchDataTerminalSystem _research = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUYautjaExperimentComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUYautjaExperimentComponent, InteractUsingEvent>(OnInteract);
        SubscribeLocalEvent<CMUYautjaExperimentComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<CMUYautjaExperimentComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<CMUYautjaExperimentComponent, CMUYautjaExperimentDoAfterEvent>(OnFinished);
        SubscribeLocalEvent<CMUYautjaExperimentComponent, DoAfterAttemptEvent<CMUYautjaExperimentDoAfterEvent>>(OnAttempt);
        Subs.BuiEvents<CMUYautjaExperimentComponent>(CMUYautjaExperimentUiKey.Key, subs =>
        {
            subs.Event<CMUYautjaExperimentMessage>(OnUiMessage);
        });
    }

    private void OnUiMessage(Entity<CMUYautjaExperimentComponent> ent, ref CMUYautjaExperimentMessage args)
    {
        switch (args.Command)
        {
            case CMUYautjaExperimentCommand.Select when args.Recipe is { } recipe:
                SelectExperiment(ent, args.Actor, recipe);
                break;
            case CMUYautjaExperimentCommand.Purge:
                Purge(ent, args.Actor);
                break;
            case CMUYautjaExperimentCommand.Authorize:
                Authorize(ent, args.Actor);
                break;
        }
    }

    private void OnMapInit(Entity<CMUYautjaExperimentComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.RequiredExperiments.Count != 0)
            return;
        var candidates = _prototypes.EnumeratePrototypes<CMUYautjaExperimentPrototype>()
            .Select(recipe => new ProtoId<CMUYautjaExperimentPrototype>(recipe.ID)).ToList();
        _random.Shuffle(candidates);
        ent.Comp.RequiredExperiments.UnionWith(candidates.Take(CMUYautjaExperimentComponent.RequiredCount));
    }

    private bool OnFloor(EntityUid uid)
    {
        var xform = Transform(uid);
        return xform.GridUid is { } grid && xform.ParentUid == grid &&
               HasComp<MapGridComponent>(grid) && !_containers.IsEntityInContainer(uid);
    }

    private bool CanResearch(EntityUid bracer, EntityUid user) =>
        HasComp<HumanoidProfileComponent>(user) && !HasComp<YautjaComponent>(user) &&
        _skills.HasSkill(user, ResearchSkill, 1) && OnFloor(bracer) &&
        _actionBlocker.CanInteract(user, bracer) &&
        _interaction.InRangeUnobstructed(user, bracer);

    private void Tell(EntityUid bracer, LocId message, params (string, object)[] args)
    {
        _chat.TrySendInGameICMessage(bracer, Loc.GetString(message, args), InGameICChatType.Speak,
            false, ignoreActionBlocker: true);
    }

    private string Instructions(CMUYautjaExperimentPrototype recipe)
    {
        var chemicals = recipe.Reagent is { } reagent
            ? Loc.GetString("cmu-yautja-experiment-standard-requirement", ("amount", recipe.SampleAmount),
                ("reagent", _reagents.Index(reagent).LocalizedName))
            : Loc.GetString("cmu-yautja-experiment-generated-requirement",
                ("amount", recipe.SampleAmount), ("tier", recipe.MinimumTier),
                ("property", _prototypes.Index(recipe.Property!.Value).LocalizedName), ("level", recipe.PropertyLevel));
        return Loc.GetString("cmu-yautja-experiment-instructions",
            ("name", Loc.GetString(recipe.Name)), ("tool", Loc.GetString(_prototypes.Index(recipe.Tool).ToolName)),
            ("chemicals", chemicals), ("seconds", (int) recipe.Duration.TotalSeconds), ("heat", recipe.Heat)) +
            "\n" + Loc.GetString(recipe.Lore);
    }

    public string ChallengeClues(CMUYautjaExperimentComponent comp)
    {
        var index = 0;
        return string.Join("\n", comp.RequiredExperiments.OrderBy(id => id.Id).Select(id =>
            Loc.GetString(comp.Discoveries.Contains(id) ? "cmu-yautja-experiment-seal-open" : "cmu-yautja-experiment-seal-clue",
                ("number", ++index), ("clue", Loc.GetString(_prototypes.Index(id).Clue)),
                ("name", Loc.GetString(_prototypes.Index(id).Name)))));
    }

    public bool SelectExperiment(EntityUid uid, EntityUid user, ProtoId<CMUYautjaExperimentPrototype> id)
    {
        if (!TryComp<CMUYautjaExperimentComponent>(uid, out var comp) || !CanResearch(uid, user) ||
            comp.Operator != null || !_prototypes.TryIndex(id, out var recipe))
            return false;
        comp.Selected = id;
        ShowGuide((uid, comp), user);
        return true;
    }

    private void OnVerbs(Entity<CMUYautjaExperimentComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanResearch(ent, args.User))
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-yautja-experiment-guide-title"), Category = Experiments,
            Act = () => ShowGuide(ent, user),
            Priority = 2,
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-yautja-experiment-purge"), Category = Experiments,
            Act = () => Purge(ent, user), Disabled = ent.Comp.Operator != null,
        });
        if (ent.Comp.Unlocked)
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-yautja-experiment-authorize"), Category = Experiments,
                Act = () => Authorize(ent, user),
            });
    }

    private void OnExamine(Entity<CMUYautjaExperimentComponent> ent, ref ExaminedEvent args)
    {
        args.PushText(Loc.GetString("cmu-yautja-experiment-entry"));
        if (!_skills.HasSkill(args.Examiner, ResearchSkill, 1))
            return;
        args.PushText(Loc.GetString("cmu-yautja-experiment-seals-short",
            ("count", ent.Comp.RequiredExperiments.Count(ent.Comp.Discoveries.Contains))));
        args.PushText(Loc.GetString("cmu-yautja-experiment-strain-short", ("heat", ent.Comp.Heat)));
        if (ent.Comp.Unlocked)
            args.PushText(Loc.GetString("cmu-yautja-experiment-unlocked-help"));
    }

    private void ShowGuide(Entity<CMUYautjaExperimentComponent> ent, EntityUid user)
    {
        if (!CanResearch(ent, user))
            return;
        UpdateGuide(ent);
        _ui.TryOpenUi(ent.Owner, CMUYautjaExperimentUiKey.Key, user);
    }

    private void UpdateGuide(Entity<CMUYautjaExperimentComponent> ent)
    {
        var seals = ent.Comp.RequiredExperiments.OrderBy(id => id.Id).Select(id =>
            new CMUYautjaExperimentSeal(Loc.GetString(_prototypes.Index(id).Clue),
                ent.Comp.Discoveries.Contains(id) ? Loc.GetString(_prototypes.Index(id).Name) : null)).ToList();
        var samples = Loc.GetString("cmu-yautja-experiment-samples-empty");
        if (_solutions.TryGetSolution(ent.Owner, SampleWell, out _, out var well))
            samples = Loc.GetString("cmu-yautja-experiment-well", ("volume", well.Volume),
                ("contents", string.Join(", ", well.Contents.Select(reagent =>
                    $"{reagent.Quantity}u {_reagents.Index(reagent.Reagent.Prototype).LocalizedName}"))));
        _ui.SetUiState(ent.Owner, CMUYautjaExperimentUiKey.Key,
            new CMUYautjaExperimentUiState(ent.Comp.Selected, seals, new(ent.Comp.Discoveries),
                ent.Comp.Heat, ent.Comp.Unlocked, ent.Comp.Operator != null, samples));
    }

    private void OnInteract(Entity<CMUYautjaExperimentComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || HasComp<YautjaComponent>(args.User))
            return;
        args.Handled = true;
        if (!CanResearch(ent, args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-experiment-floor"), ent, args.User, PopupType.SmallCaution);
            return;
        }
        if (ent.Comp.Operator != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-experiment-busy"), ent, args.User);
            return;
        }
        if (_solutions.TryGetDrainableSolution(args.Used, out var source, out var sample) &&
            _solutions.TryGetSolution(ent.Owner, SampleWell, out var destination, out var well))
        {
            var amount = FixedPoint2.Min(10, sample.Volume, well.AvailableVolume);
            if (amount > 0)
            {
                var aliquot = _solutions.SplitSolution(source.Value, amount);
                _solutions.TryAddSolution(destination.Value, aliquot);
                Tell(ent, "cmu-yautja-experiment-sample", ("amount", amount));
                UpdateGuide(ent);
            }
            return;
        }
        StartExperiment(ent, args.User, args.Used);
    }

    private bool HasReagents(EntityUid uid, CMUYautjaExperimentPrototype recipe) =>
        FindSample(uid, recipe) != null;

    private string? FindSample(EntityUid uid, CMUYautjaExperimentPrototype recipe)
    {
        if (!_solutions.TryGetSolution(uid, SampleWell, out _, out var solution))
            return null;

        if (recipe.Reagent is { } stock)
            return solution.GetTotalPrototypeQuantity(stock.Id) >= recipe.SampleAmount ? stock.Id : null;
        if (recipe.Property is not { } property)
            return null;

        // Match actual round-generated chemistry, including simulator derivatives. Stock reagents
        // with similar effects cannot substitute, and tiny amounts cannot qualify a diluted sample.
        foreach (var (reagent, _) in solution.Contents)
        {
            var id = reagent.Prototype;
            if (_generator.ProceduralReagentData.TryGetValue(id, out var data) &&
                _generator.IdentifiedChemicals.ContainsKey(id) && data.GenTier >= recipe.MinimumTier &&
                data.Effects.GetValueOrDefault(property.Id) >= recipe.PropertyLevel &&
                solution.GetTotalPrototypeQuantity(id) >= recipe.SampleAmount)
                return id;
        }
        return null;
    }

    public bool StartExperiment(Entity<CMUYautjaExperimentComponent> ent, EntityUid user, EntityUid tool)
    {
        var recipe = _prototypes.Index(ent.Comp.Selected);
        if (!CanResearch(ent, user) || ent.Comp.Operator != null || _hands.GetActiveItem(user) != tool ||
            !HasReagents(ent, recipe) ||
            !_tools.HasQuality(tool, recipe.Tool))
        {
            _popup.PopupEntity(Instructions(recipe), ent, user, PopupType.MediumCaution);
            return false;
        }
        var attempt = new ToolUseAttemptEvent(user, 0);
        if (recipe.Charge < 0 && (!TryComp<YautjaBracerComponent>(ent, out var power) || power.Charge < -recipe.Charge))
        {
            Tell(ent, "cmu-yautja-experiment-power");
            return false;
        }
        RaiseLocalEvent(tool, attempt);
        if (attempt.Cancelled)
            return false;
        var ev = new CMUYautjaExperimentDoAfterEvent { Experiment = recipe.ID };
        var doAfter = new DoAfterArgs(EntityManager, user, recipe.Duration, ev, ent, target: ent, used: tool)
        {
            BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
            AttemptFrequency = AttemptFrequency.EveryTick, DistanceThreshold = 1.5f,
            BlockDuplicate = true, CancelDuplicate = false, ForceVisible = true,
        };
        ent.Comp.Operator = user;
        if (!_doAfter.TryStartDoAfter(doAfter, out var id))
        {
            ent.Comp.Operator = null;
            return false;
        }
        // Instant admin DoAfters can finish synchronously inside TryStartDoAfter.
        if (ent.Comp.Operator == user)
            ent.Comp.Pending = id;
        Tell(ent, "cmu-yautja-experiment-start", ("name", Loc.GetString(recipe.Name)));
        _audio.PlayPvs(recipe.Sound, ent);
        UpdateGuide(ent);
        return true;
    }

    private void OnAttempt(Entity<CMUYautjaExperimentComponent> ent, ref DoAfterAttemptEvent<CMUYautjaExperimentDoAfterEvent> args)
    {
        var user = args.DoAfter.Args.User;
        if (!CanResearch(ent, user) || args.DoAfter.Args.Used is not { } tool ||
            _hands.GetActiveItem(user) != tool || !HasReagents(ent, _prototypes.Index(args.Event.Experiment)))
        {
            args.Cancel();
            return;
        }
        var attempt = new ToolUseAttemptEvent(user, 0);
        RaiseLocalEvent(tool, attempt);
        if (attempt.Cancelled)
            args.Cancel();
    }

    private void OnFinished(Entity<CMUYautjaExperimentComponent> ent, ref CMUYautjaExperimentDoAfterEvent args)
    {
        if (args.Handled || ent.Comp.Operator != args.User ||
            ent.Comp.Pending is { } pending && pending != args.DoAfter.Id)
            return;
        ent.Comp.Operator = null;
        ent.Comp.Pending = null;
        args.Handled = true;
        UpdateGuide(ent);
        var recipe = _prototypes.Index(args.Experiment);
        if (args.Cancelled || !CanResearch(ent, args.User) || FindSample(ent, recipe) is not { } sample)
            return;
        if (!_solutions.TryGetSolution(ent.Owner, SampleWell, out var well, out _))
            return;
        if (recipe.Charge < 0 && (!TryComp<YautjaBracerComponent>(ent, out var power) ||
            !_power.TryDrainPower((ent.Owner, power), args.User, -recipe.Charge, false)))
            return;
        _solutions.RemoveReagent(well.Value, sample, recipe.SampleAmount);
        ent.Comp.Heat = Math.Max(0, ent.Comp.Heat + recipe.Heat);
        PlayExperiment(ent, ent.Comp.Heat >= 8 ? Overload : recipe.ID);
        if (args.Used is { } used && TryComp<ToolComponent>(used, out var tool))
            _tools.PlayToolSound(used, tool, args.User, false);
        if (ent.Comp.Heat >= 8)
        {
            ent.Comp.Heat = 3;
            if (TryComp<YautjaBracerComponent>(ent, out var bracer))
                _power.RemovePower((ent.Owner, bracer), FixedPoint2.Min(bracer.Charge, 300));
            Tell(ent, "cmu-yautja-experiment-overload");
            UpdateGuide(ent);
            return;
        }
        Tell(ent, recipe.Response);
        if (recipe.Visual == CMUYautjaExperimentVisual.Radar)
        {
            var count = _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 6f)
                .Count(uid => TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState != MobState.Dead);
            Tell(ent, "cmu-yautja-experiment-radar", ("count", count));
        }
        if (recipe.Charge > 0 && TryComp<YautjaBracerComponent>(ent, out var charge))
            _power.RegenPower((ent.Owner, charge), recipe.Charge);
        if (recipe.Output is { } output)
            Spawn(output, Transform(ent).Coordinates);
        var discovery = ent.Comp.Discoveries.Add(recipe.ID);
        if (discovery && ent.Comp.RequiredExperiments.Contains(recipe.ID))
        {
            // A solved seal vents its own core; the five chosen procedures need no extra cooling recipe.
            ent.Comp.Heat = 0;
            Tell(ent, "cmu-yautja-experiment-seal-accepted",
                ("count", ent.Comp.RequiredExperiments.Count(ent.Comp.Discoveries.Contains)));
        }
        else if (discovery && !ent.Comp.Unlocked)
            Tell(ent, "cmu-yautja-experiment-seal-unrelated");
        if (!ent.Comp.Unlocked && ent.Comp.RequiredExperiments.Count == CMUYautjaExperimentComponent.RequiredCount &&
            ent.Comp.Discoveries.IsSupersetOf(ent.Comp.RequiredExperiments))
        {
            ent.Comp.Unlocked = true;
            Authorize(ent, args.User);
        }
        if (discovery)
        {
            var faction = _research.GetFaction(ent);
            _research.UpdateClearance(_research.GetCredits(faction) + recipe.ResearchReward, -1, faction,
                _research.GetSampleData(faction) + recipe.ResearchReward);
            var note = Spawn("Paper", Transform(ent).Coordinates);
            _paper.SetContent(note, Loc.GetString("cmu-yautja-experiment-notes", ("procedure", Instructions(recipe)),
                ("response", Loc.GetString(recipe.Response)), ("reward", recipe.ResearchReward),
                ("clues", ChallengeClues(ent.Comp))));
        }
        UpdateGuide(ent);
    }

    /// <summary>Shared by completed procedures and the admin showcase so their timing and audio agree.</summary>
    public Entity<CMUYautjaEffectComponent> PlayExperiment(EntityUid bracer, ProtoId<CMUYautjaExperimentPrototype> id)
    {
        var recipe = _prototypes.Index(id);
        var effect = _effects.Emit(CMUYautjaEffect.Experiment, Transform(bracer).Coordinates,
            CMUYautjaExperimentPrototype.EffectDuration);
        effect.Comp.Experiment = id;
        Dirty(effect);
        var light = EnsureComp<Robust.Server.GameObjects.PointLightComponent>(effect.Owner);
        _lights.SetColor(effect.Owner, recipe.Color, light);
        _lights.SetRadius(effect.Owner, 2.7f, light);
        _lights.SetEnergy(effect.Owner, .05f, light);
        _audio.PlayPvs(recipe.Sound, effect.Owner);
        // The release is tied to the effect entity, even if the device is picked up.
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(CMUYautjaExperimentPrototype.EffectDuration *
            CMUYautjaExperimentPrototype.ReleaseAt), () =>
        {
            if (!TerminatingOrDeleted(effect.Owner))
                _audio.PlayPvs(recipe.ReleaseSound, effect.Owner);
        });
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(CMUYautjaExperimentPrototype.EffectDuration *
            CMUYautjaExperimentPrototype.CollapseAt), () =>
        {
            if (!TerminatingOrDeleted(effect.Owner))
                _audio.PlayPvs(recipe.CollapseSound, effect.Owner);
        });
        return effect;
    }

    private void Authorize(Entity<CMUYautjaExperimentComponent> ent, EntityUid user)
    {
        if (!ent.Comp.Unlocked || !CanResearch(ent, user) || ent.Comp.Operator != null)
            return;
        EnsureComp<YautjaTechAuthorizedComponent>(user);
        EnsureComp<CMUYautjaResearchOperatorComponent>(user);
        Tell(ent, "cmu-yautja-experiment-authorized");
    }

    private void Purge(Entity<CMUYautjaExperimentComponent> ent, EntityUid user)
    {
        if (!CanResearch(ent, user) || ent.Comp.Operator != null ||
            !_solutions.TryGetSolution(ent.Owner, SampleWell, out var well, out _))
            return;
        _solutions.RemoveAllSolution(well.Value);
        Tell(ent, "cmu-yautja-experiment-purged");
        UpdateGuide(ent);
    }
}
