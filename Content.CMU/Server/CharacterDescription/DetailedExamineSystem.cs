using Content.Server.EUI;
using Content.Server._CMU14.CharacterDescription.UI;
using Content.Shared._CMU14.CharacterDescription;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.CharacterDescription;

public sealed class DetailedExamineSystem : EntitySystem
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CharacterDescriptionComponent, GetVerbsEvent<Verb>>(AddDetailedExamineVerb);
    }

    private void AddDetailedExamineVerb(Entity<CharacterDescriptionComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        var detailsRange = _examine.IsInDetailsRange(args.User, ent);
        var player = actor.PlayerSession;
        var target = ent.Owner;

        var verb = new Verb
        {
            Text = Loc.GetString("detailed-examine-verb-text"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/examine.svg.192dpi.png")),
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("detailed-examine-verb-disabled"),
            Act = () => _euiManager.OpenEui(new DetailedExamineEui(GetNetEntity(target)), player),
        };

        args.Verbs.Add(verb);
    }
}
