using System;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Research;

public abstract partial class SharedChemicalSimulatorSystem
{
    private static readonly ProtoId<DatasetPrototype> ConflictingPropertiesDataset = "CMUConflictingProperties";

    // reads the dataset directly instead of the generator's unfolded list, that one is
    // only filled after a map load and would make the warning flicker in and out
    public bool TryGetConflictWarning(Entity<ChemSimulatorComponent> ent, [NotNullWhen(true)] out string? warning)
    {
        warning = null;
        if (ent.Comp.Mode != ChemSimulatorMode.Relate && ent.Comp.Mode != ChemSimulatorMode.Add)
            return false;

        if (ent.Comp.ReferenceProperty is not { } refProp)
            return false;

        if (!TryGetReportData(ent, "target", out var target) ||
            !TryGetReportData(ent, "reference", out var reference) ||
            !reference.Effects.TryGetValue(refProp, out var refLevel))
            return false;

        if (!_protoman.TryIndex(ConflictingPropertiesDataset, out var dataset))
            return false;

        // relate pulls the target property out before inserting, so it can't be the one that clashes
        var replaced = ent.Comp.Mode == ChemSimulatorMode.Relate ? ent.Comp.TargetProperty : null;
        var props = _protoman.GetInstances<ReagentPropertyPrototype>();

        foreach (var pair in dataset.Values)
        {
            var split = pair.Split(',');
            if (split.Length < 2)
                continue;

            string? other = null;
            if (split[0] == refProp)
                other = split[1];
            else if (split[1] == refProp)
                other = split[0];

            if (other == null || other == replaced || !target.Effects.TryGetValue(other, out var otherLevel))
                continue;

            if (!props.TryGetValue(refProp, out var refProto) || !props.TryGetValue(other, out var otherProto))
                continue;

            // mirrors ServerReagentGeneratorSystem.InsertProperty: levels subtract, the higher one survives
            var key = otherLevel > refLevel ? "research-sim-status-conflict-target-survives"
                : otherLevel < refLevel ? "research-sim-status-conflict-reference-survives"
                : "research-sim-status-conflict-cancel";

            warning = Loc.GetString(key,
                ("REFERENCE", refProto.LocalizedName),
                ("TARGET", otherProto.LocalizedName),
                ("LEVEL", Math.Abs(otherLevel - refLevel)));
            return true;
        }

        return false;
    }

    private bool TryGetReportData(Entity<ChemSimulatorComponent> ent, string container, out GeneratedReagentData data)
    {
        data = default;
        if (!_con.TryGetContainer(ent.Owner, container, out var con) || con.Count == 0 ||
            !TryComp<ResearchReportComponent>(con.ContainedEntities[0], out var report) ||
            report.Data is not { } reportData)
            return false;

        data = reportData;
        return true;
    }
}
