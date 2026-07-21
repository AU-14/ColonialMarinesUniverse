using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._RMC14.Audio;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.DoAfter;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Client.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

public abstract partial class XRFScannerSystem : EntitySystem
{
    [Dependency] private SharedResearchDataTerminalSystem _data = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private SharedContainerSystem _consys = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedDoAfterSystem _doafter = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IPrototypeManager _protoman = default!;
    [Dependency] private MetaDataSystem _mets = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    //                                              start      end    played audio?
    private Dictionary<Entity<XRFScannerComponent>, (TimeSpan, TimeSpan, bool)> processing = [];
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XRFScannerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<XRFScannerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<XRFScannerComponent, XRFDoAfterEvent>(WhenDoAfterEnds);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (processing.Count > 0)
        {
            foreach (var kvp in processing)
            {
                if (!kvp.Key.Comp.Processing)
                {
                    processing.Remove(kvp.Key);
                    continue;
                }
                if (!kvp.Value.Item3 && (_timing.CurTime - kvp.Value.Item1) / (kvp.Value.Item2 - kvp.Value.Item1) > 0.6)
                {
                    processing[kvp.Key] = (kvp.Value.Item1, kvp.Value.Item2, true);
                    _audio.PlayPvs(kvp.Key.Comp.PrintSound, kvp.Key);
                    continue;
                }
                if (_timing.CurTime > kvp.Value.Item2)
                {
                    _consys.TryGetContainer(kvp.Key.Owner, "sample", out var sample);
                    if (sample is null)
                    {
                        _audio.PlayPvs(kvp.Key.Comp.FailSound, kvp.Key);
                        kvp.Key.Comp.Processing = false;
                        processing.Remove(kvp.Key);
                        continue;
                    }
                    FinishProcess(kvp.Key, sample);
                    kvp.Key.Comp.Processing = false;
                    processing.Remove(kvp.Key);
                    continue;
                }
            }
        }
    }

    private void OnRestart(RoundRestartCleanupEvent args)
    {
        processing.Clear();
    }
    public void OnInteractUsing(Entity<XRFScannerComponent> ent, ref InteractUsingEvent args)
    {
        if (ent.Comp.Processing)
        {
            _popups.PopupEntity(Loc.GetString("research-xrf-scanner-processing"), ent);
            return;
        }
        //TODO: skillcheck here

        _consys.TryGetContainer(ent.Owner, "sample", out var sample);
        if (sample is null)
            return;
        if (!TryComp<VialComponent>(args.Used, out _))
        {
            _popups.PopupEntity(Loc.GetString("research-xrf-scanner-only-vials"), ent);
            return;
        }
        if (sample.ContainedEntities.Count > 0)
        {
            _popups.PopupEntity(Loc.GetString("research-xrf-scanner-full"), ent);
            return;
        }
        if (_consys.Insert(args.Used, sample))
        {
            _appearance.SetData(ent.Owner, XRFScannerVisuals.State, XRFScannerState.Sample);
            _popups.PopupEntity(Loc.GetString("research-xrf-scanner-config", ("USER", args.User)), ent);
            var dargs = new DoAfterArgs(EntityManager, args.User, 1, new XRFDoAfterEvent(), args.Target, args.Target, args.Target)
            {
                BlockDuplicate = true,
                CancelDuplicate = true,
                DuplicateCondition = DuplicateConditions.All,
                BreakOnMove = true,
                BreakOnDamage = true,
                BreakOnRest = true
            };
            _doafter.TryStartDoAfter(dargs);
        }

    }
    public void WhenDoAfterEnds(Entity<XRFScannerComponent> ent, ref XRFDoAfterEvent args)
    {
        _consys.TryGetContainer(ent.Owner, "sample", out var sample);
        if (sample is null)
            return;
        if (sample.Count == 0)
        {
            _popups.PopupEntity("research-xrf-scanner-conflict", ent.Owner);
            return;
        }
        ent.Comp.Processing = true;
        _solutions.TryGetSolution(sample.ContainedEntities[0], "beaker", out var solution);
        if (solution is not null)
        {
            if (solution.Value.Comp.Solution.Volume < 30 || solution.Value.Comp.Solution.Contents.Count > 1)
            {
                _appearance.SetData(ent.Owner, XRFScannerVisuals.State, XRFScannerState.Error);
            }
            else
            {
                _appearance.SetData(ent.Owner, XRFScannerVisuals.State, XRFScannerState.Processing);
            }
        }
        else
        {
            _appearance.SetData(ent.Owner, XRFScannerVisuals.State, XRFScannerState.Error);
        }
        if (!_net.IsClient)
            processing.Add(ent, (_timing.CurTime, _timing.CurTime + ent.Comp.Inefficiency, false));
            //Timer.Spawn(ent.Comp.Inefficiency, () => FinishProcess(ent, sample));
    }
    private void FinishProcess(Entity<XRFScannerComponent> ent, BaseContainer sample)
    {
        if (_solutions.TryGetSolution(sample.ContainedEntities[0], "beaker", out var solution))
        {
            var chems = solution.Value.Comp.Solution;
            if (sample.Count == 0 || chems.Volume < 30 || chems.Contents.Count > 1)
            {
                if (sample.Count == 0)
                {
                    PrintResult(ent, false, Loc.GetString("xrf-scanner-fail-missing"));
                    _audio.PlayPvs(ent.Comp.FailSound, ent);
                }
                else if (chems.Volume == FixedPoint2.Zero)
                {
                    PrintResult(ent, false, Loc.GetString("xrf-scanner-fail-empty"));
                    _audio.PlayPvs(ent.Comp.FailSound, ent);
                }
                else if (chems.Volume < 30)
                {
                    PrintResult(ent, false, Loc.GetString("xrf-scanner-fail-insufficient"));
                    _audio.PlayPvs(ent.Comp.FailSound, ent);
                }
                else if (chems.Contents.Count > 1)
                {
                    PrintResult(ent, false, Loc.GetString("xrf-scanner-fail-contaminated"));
                    _audio.PlayPvs(ent.Comp.FailSound, ent);
                }
                else
                {
                    PrintResult(ent, false, Loc.GetString("xrf-scanner-fail-unknown"));
                    _audio.PlayPvs(ent.Comp.FailSound, ent);
                }
                _appearance.SetData(ent.Owner, XRFScannerVisuals.State, XRFScannerState.Failed);
            }
            else
            {
                _appearance.SetData(ent.Owner, XRFScannerVisuals.State, XRFScannerState.Finished);
                PrintResult(ent, true, string.Empty);
            }
        }
        else
        {
            PrintResult(ent, false, Loc.GetString("xrf-scanner-fail-invalid"));
            _appearance.SetData(ent.Owner, XRFScannerVisuals.State, XRFScannerState.Failed);
            _audio.PlayPvs(ent.Comp.FailSound, ent);
        }
        ent.Comp.Sample++;
        ent.Comp.Processing = false;
        Dirty(ent);
    }

    public void OnInteractHand(Entity<XRFScannerComponent> ent, ref InteractHandEvent args)
    {
        if (ent.Comp.Processing)
        {
            _popups.PopupClient(Loc.GetString("research-xrf-scanner-processing"), args.User);
            return;
        }
        if (_consys.TryGetContainer(ent.Owner, "sample", out var container))
        {
            if (container.Count == 0)
            {
                _popups.PopupClient(Loc.GetString("research-xrf-scanner-empty"), args.User);
            }
            else
            {
                //_consys.Remove(container.ContainedEntities[0], container);
                _appearance.SetData(ent.Owner, XRFScannerVisuals.State, XRFScannerState.Scanner);
                _hands.PickupOrDrop(args.User, container.ContainedEntities[0]);
            }
        }
    }

    public void PrintResult(Entity<XRFScannerComponent> ent, bool result, string reason)
    {
        string contents = string.Empty;
        if (result)
        {
            if (!_consys.TryGetContainer(ent.Owner, "sample", out var sample))
                return;
            if (!_solutions.TryGetSolution(sample.ContainedEntities[0], "beaker", out var solution))
                return;
            var reagentname = solution.Value.Comp.Solution.Contents[0].Reagent.Prototype;
            var reagent = _protoman.GetInstances<ReagentPrototype>()[reagentname];
            _mets.SetEntityName(sample.ContainedEntities[0], string.Format("vial ({0})", reagent.LocalizedName));
            DirtyEntity(sample.ContainedEntities[0]);
            if (_net.IsServer)
            {
                var ev = new XRFScannedReagentEvent(reagent.ID, ent.Comp.Sample, GetNetEntity(ent.Owner));
                RaiseLocalEvent(ev);
                _audio.PlayPvs(ent.Comp.SuccessSound, ent.Owner);
            }
        }
        else
        {
            DirtyEntity(ent.Owner);
            if (_net.IsClient)
                return;
            var paper = SpawnNextToOrDrop("CMUWYPaper", ent.Owner);
            _mets.SetEntityName(paper, Loc.GetString("xrf-report-error"));
            contents += Loc.GetString("cmu-paper-header-wy") + '\n';
            //TODO: replace below with proper loc.getstring
            contents += Loc.GetString("cmu-paper-subheader-research-xrf-fail") + " #" + ent.Comp.Sample + '\n';
            contents += Loc.GetString("cmu-paper-research-fail-reason", ("REASON", reason)) + '\n';
            contents += Loc.GetString("cmu-paper-xrf-footer");
            _paper.SetContent(paper, contents);
            DirtyEntity(paper);
        }
    }
}
