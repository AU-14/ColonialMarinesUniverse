using Content.Server.EUI;
using Content.Server.CMU14.CharacterDescription.UI;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.CharacterDescription;

public sealed partial class DetailedExamineSystem : EntitySystem
{
    [Dependency] private  EuiManager _euiManager = default!;
    [Dependency] private  ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CharacterDescriptionComponent, GetVerbsEvent<ExamineVerb>>(AddDetailedExamineVerb);
        SubscribeLocalEvent<CharacterDescriptionComponent, GetVerbsEvent<Verb>>(AddEditDescriptionVerb);
    }

    private void AddEditDescriptionVerb(Entity<CharacterDescriptionComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || args.User != args.Target)
            return;

        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        var player = actor.PlayerSession;
        var target = ent.Owner;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-edit-description-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/character.svg.192dpi.png")),
            Priority = -5,
            Act = () => _euiManager.OpenEui(new EditCharacterDescriptionEui(target), player),
        });
    }

    private void AddDetailedExamineVerb(Entity<CharacterDescriptionComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        // Anyone who can see the character can read their description; there is no details-range requirement.
        if (!_examine.CanExamine(args.User, ent))
            return;

        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        var player = actor.PlayerSession;
        var target = ent.Owner;

        var verb = new ExamineVerb
        {
            Text = Loc.GetString("detailed-examine-verb-text"),
            Message = Loc.GetString("detailed-examine-verb-message"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/human-head-eyes.svg.192dpi.png")),
            Act = () => _euiManager.OpenEui(new DetailedExamineEui(GetNetEntity(target)), player),
        };

        args.Verbs.Add(verb);
    }
}
