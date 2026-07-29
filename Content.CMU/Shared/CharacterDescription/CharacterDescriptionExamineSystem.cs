using Content.Shared.Examine;
using Robust.Shared.Utility;

namespace Content.Shared._CMU14.CharacterDescription;

public sealed class CharacterDescriptionExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CharacterDescriptionComponent, ExaminedEvent>(OnExamined);
    }

    private const string ShortExamineColor = "#ADD8E6";

    private void OnExamined(Entity<CharacterDescriptionComponent> ent, ref ExaminedEvent args)
    {
        if (string.IsNullOrWhiteSpace(ent.Comp.ShortExamine))
            return;

        var text = FormattedMessage.EscapeText(ent.Comp.ShortExamine);
        args.PushMarkup($"[color={ShortExamineColor}]{text}[/color]");
    }
}
