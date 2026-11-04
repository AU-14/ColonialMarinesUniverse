using System.Linq;
using System.Text;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Intel;
using Content.Shared.CMU14.Intel;
using Content.Shared.Paper;

namespace Content.Server.CMU14.Round.Objectives.Type;

public sealed partial class ObjFetchAnalyzerSystem
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    /// <summary>
    /// Prints a readout of the areas holding the most intel that hasn't been processed yet: unread or
    /// unrecovered documents and devices, and data disks that haven't been cracked.
    /// </summary>
    private void PrintIntelSurvey(EntityUid analyzer, Entity<CMUIntelSurveyOnAnalyzeComponent> source)
    {
        var counts = new Dictionary<string, int>();

        void Count(EntityUid intel)
        {
            if (TerminatingOrDeleted(intel) || !_area.TryGetArea(intel, out _, out var areaProto))
                return;

            var name = areaProto.Name;
            counts[name] = counts.GetValueOrDefault(name) + 1;
        }

        var retrieve = EntityQueryEnumerator<IntelRetrieveItemObjectiveComponent>();
        while (retrieve.MoveNext(out var uid, out var comp))
        {
            if (comp.State != IntelObjectiveState.Complete)
                Count(uid);
        }

        var read = EntityQueryEnumerator<IntelReadObjectiveComponent>();
        while (read.MoveNext(out var uid, out var comp))
        {
            // Documents are already counted above while they're still waiting to be recovered.
            if (comp.State != IntelObjectiveState.Complete && !HasComp<IntelRetrieveItemObjectiveComponent>(uid))
                Count(uid);
        }

        var disks = EntityQueryEnumerator<CMUIntelDataDiskComponent>();
        while (disks.MoveNext(out var uid, out var disk))
        {
            if (!disk.Uploaded)
                Count(uid);
        }

        var top = counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(source.Comp.Areas)
            .ToList();

        var text = new StringBuilder();
        text.AppendLine($"[head=2]{source.Comp.Title}[/head]");
        text.AppendLine();
        if (top.Count == 0)
        {
            text.AppendLine("No remaining intel signatures found in the recorder's logs.");
        }
        else
        {
            text.AppendLine("Colony logs point to the heaviest concentrations of remaining intel in:");
            text.AppendLine();
            for (var i = 0; i < top.Count; i++)
                text.AppendLine($"{i + 1}. [bold]{top[i].Key}[/bold] ({top[i].Value} item{(top[i].Value == 1 ? "" : "s")})");
        }

        text.AppendLine();
        text.AppendLine("[italic]Readout reflects intel locations at the time the CIR-60 was activated and may be inaccurate.[/italic]");

        var paper = Spawn(source.Comp.Paper, Transform(analyzer).Coordinates);
        _paper.SetContent(paper, text.ToString());
        _metaData.SetEntityName(paper, "intel survey readout");
    }
}
