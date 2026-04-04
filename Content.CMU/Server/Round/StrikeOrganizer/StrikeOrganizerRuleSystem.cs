using Content.Server.GameTicking.Rules;
using Content.Server.AU14.Systems;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server.AU14.Round.StrikeOrganizer;

public sealed class StrikeOrganizerRuleSystem : GameRuleSystem<StrikeOrganizerRuleComponent>
{
    [Dependency] private readonly WantedSystem _wantedSystem = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StrikeOrganizerComponent, ComponentStartup>(OnStrikeOrganizerSpawned);
    }

    private void OnStrikeOrganizerSpawned(EntityUid uid, StrikeOrganizerComponent component, ComponentStartup args)
    {
        var organizerName = _entityManager.GetComponentOrNull<MetaDataComponent>(uid)?.EntityName ?? "Unknown";

        var faxContent = "[color=#383838]█[/color][color=#ffffff]░░[/color][color=#8c0000]█ [color=#383838]█▄[/color] █ [/color][head=3]Colonial Marshall Bureau[/head]\n\n" +
            "[color=#383838]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/color]\n" +
            "[color=#8c0000]▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀[/color]\n\n" +
            "[head=2][color=goldenrod]Labor Unrest Alert[/color][/head]\n\n" +
            "[bold]To:[/bold] [italic]CMB Office Staff[/italic]\n" +
            "[bold]From:[/bold] [bold]CMB Sectoral HQ[/bold]\n" +
            "[color=#134975]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]\n" +
            "Sheriff,\n" +
            $"  We've received reports of a labor organizer, [bold]{organizerName}[/bold], stirring up unrest in your colony. " +
            "Keep an eye on the situation and ensure it doesn't get out of hand.\n\n" +
            "Signed,\n" +
            "[color=#dfc189][bolditalic]Regional HQ[/bolditalic][/color]\n" +
            "[color=#134975]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]";

        _wantedSystem.SendCustomFax(
            "Colony Marshal Bureau",
            "Labor Unrest Alert",
            faxContent,
            "paper_stamp-cmb",
            new System.Collections.Generic.List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#b0901b"), StampedName = "CMB" }
            });
    }
}

