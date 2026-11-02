using Content.Client.Guidebook;
using Content.Shared.CMU14.Fob;
using Content.Shared.Verbs;

namespace Content.Client.CMU14.Fob;

public sealed partial class CMUFobGuideSystem : EntitySystem
{
    [Dependency] private GuidebookSystem _guidebook = default!;

    public override void Initialize() => SubscribeLocalEvent<CMUFobInstructionsComponent, GetVerbsEvent<ExamineVerb>>(OnHelp);

    private void OnHelp(Entity<CMUFobInstructionsComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        args.Verbs.Add(new ExamineVerb
        {
            Text = Loc.GetString("cmu-fob-guide-open"), ClientExclusive = true, CloseMenu = true,
            Act = () => _guidebook.OpenHelp(["CMUFobGuide"]),
        });
    }
}
