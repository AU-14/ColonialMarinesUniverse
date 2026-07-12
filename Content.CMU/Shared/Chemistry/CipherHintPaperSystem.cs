using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared.Paper;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._CMU14.Chemistry;

public sealed partial class CipherHintPaperSystem : EntitySystem
{
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedReagentGeneratorSystem _gen = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<CipherHintPaperComponent, MapInitEvent>(OnMapInit);
    }


    private void OnMapInit(Entity<CipherHintPaperComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<PaperComponent>(ent.Owner, out var paper))
        {
            var ciph = _gen.UnfoldedCombinations["Ciphering"];
            string content = string.Empty;
            content += Loc.GetString("cmu-paper-ciph-hint-header") + '\n';
            content += Loc.GetString("cmu-paper-ciph-hint-subheader") + '\n';
            content += Loc.GetString("cmu-paper-ciph-hint", ("CIPH", "Ciphering"), ("A", ciph[0]), ("B", ciph[1]), ("C", ciph[2])) + '\n';
            content += Loc.GetString("cmu-paper-ciph-hint-footer");
            _paper.SetContent(ent.Owner, content);
        }
    }
}
