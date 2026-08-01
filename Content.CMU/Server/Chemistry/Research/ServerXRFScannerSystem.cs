using Content.Server._AU14.Chemistry.Reagents;
using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._AU14.Chemistry.Research;
using Content.Shared._CMU14.Chemistry.Reagent;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Paper;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Server._AU14.Chemistry.Research;

public sealed partial class ServerXRFScannerSystem : XRFScannerSystem
{
    [Dependency] private ServerReagentGeneratorSystem _generator = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private MetaDataSystem _mets = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private ServerResearchDataTerminalSystem _rdat = default!;
    [Dependency] private IGameTiming _time = default!;
    [Dependency] private SharedRequisitionsSystem _req = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XRFScannedReagentEvent>(OnReagentScanned);
    }
    private void OnReagentScanned(XRFScannedReagentEvent args)
    {
        var dat = _generator.CreateReport(args.Reagent, false, args.SampleNum);
        if (dat is null || !_protoMan.GetInstances<ReagentPrototype>().TryGetValue(args.Reagent, out var chem))
            return;
        var properties = _protoMan.GetInstances<ReagentPropertyPrototype>();
        GeneratedReagentData? GRD = null;
        HashSet<ReagentPropertyPrototype> chemprops = [];
        //first check if we have this somewhere so we don't have to rip the properties out with our bare hands
        if (_generator.ProceduralReagentData.TryGetValue(args.Reagent, out var gdat))
        {
            GRD = gdat;
            foreach (var prop in gdat.Effects.Keys)
            {
                if (properties.TryGetValue(prop, out var protoprop))
                    chemprops.Add(protoprop);
            }
        }
        else if (_generator.ReagentData.TryGetValue(args.Reagent, out var ngdat))
        {
            GRD = ngdat;
            foreach (var prop in ngdat.Effects.Keys)
            {
                if (properties.TryGetValue(prop, out var protoprop))
                    chemprops.Add(protoprop);
            }
        }
        else
        {
            //and now we suffer!
            GRD = _generator.ConvertToGRD(chem);
            foreach (var prop in GRD.Value.Effects.Keys)
            {
                if (properties.TryGetValue(prop, out var protoprop))
                    chemprops.Add(protoprop);
            }
        }
        if (chem.Class < ReagentClass.Special || (chem.Class >= ReagentClass.Special && dat.Value.Completed))
            _generator.SaveNewProperties(chemprops);
        EntityUid scanner = GetEntity(args.Scanner);
        if (chem.Class >= ReagentClass.Special && !_generator.IdentifiedChemicals.ContainsKey(chem.ID))
        {
            //todo: statistics
            //todo: do something when DNA Disintegrating is discovered
            string faction = string.Empty;
            if (TryComp<XRFScannerComponent>(scanner, out var scomp))
            {
                faction = scomp.Faction;
            }
            _rdat.CompleteChemical(chem, faction, scanner);
        }
        if (scanner == EntityUid.Invalid) //PANIC!!!!!!
            return;
        EntityUid? paper = EntityUid.Invalid;
        switch (dat.Value.Icon)
        {
            case ResearchReportIconEnum.Full:
                TrySpawnNextTo("CMUResearchReportFull", scanner, out paper);
                break;
            case ResearchReportIconEnum.Partial:
                TrySpawnNextTo("CMUResearchReportPartial", scanner, out paper);
                break;
            case ResearchReportIconEnum.Synthesis:
                TrySpawnNextTo("CMUResearchReportSynthesis", scanner, out paper);
                break;
            default:
                TrySpawnNextTo("CMUWYPaper", scanner, out paper);
                break;
        }
        if (paper is null)
            return;
        var realpaper = paper.Value;
        string name = Loc.GetString("research-report-analysis-name", ("NAME1", chem.LocalizedName), ("NAME2", dat.Value.Name));
        string contents = Loc.GetString("cmu-paper-header-wy") + '\n';
        contents += Loc.GetString("cmu-paper-subheader-xrf-analysis", ("NUMBER", args.SampleNum),
            ("NAME", chem.LocalizedName)) + '\n';
        contents += dat.Value.Info + '\n';
        contents += Loc.GetString("cmu-paper-xrf-footer");
        EnsureComp<ResearchReportComponent>(realpaper, out var comp);
        comp.Data = GRD.Value;
        comp.Valid = dat.Value.Valid;
        comp.Completed = dat.Value.Completed;
        _rdat.ResearchData.TryAdd( _rdat.ResearchData.Count - 1,
            (GRD.Value.ID, contents, _time.CurTime, false, GRD.Value, comp.Valid, comp.Completed));
        _mets.SetEntityName(realpaper, name);
        _paper.SetContent(realpaper, contents);
        DirtyEntity(realpaper);
        DirtyEntity(scanner);
    }
}
