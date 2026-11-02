using Content.Client.Guidebook;
using Content.Shared.CMU14.Fob;
using Content.Shared.Guidebook;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

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
            // List collection expressions emit CollectionsMarshal.SetCount, which the content sandbox forbids.
            Act = () => _guidebook.OpenHelp(new List<ProtoId<GuideEntryPrototype>> { "CMUFobGuide" }),
        });
    }
}
