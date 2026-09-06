using Content.Client._RMC14.LinkAccount;
using Content.Client._RMC14.Roadmap;
using Content.Client.Changelog;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;

namespace Content.Client.Info
{
    public sealed class LinkBanner : BoxContainer
    {
        // The banner shares a grid with the lobby's action buttons, so it has to be built to the same
        // box: same column count, same separations, every button expanding to fill its cell. Buttons
        // that shrink-wrapped their own label left a row of mismatched nubs above a row of full-size
        // ones. Kept in step with the action grid in LobbyGui.xaml.
        private const int Columns = 2;
        private const int ButtonMinHeight = 28;
        private const int HSeparation = 6;
        private const int VSeparation = 4;

        private readonly IConfigurationManager _cfg;

        private ValueList<(CVarDef<string> cVar, Button button)> _infoLinks;

        public LinkBanner()
        {
            var buttons = new GridContainer
            {
                Columns = Columns,
                HorizontalExpand = true,
                HSeparationOverride = HSeparation,
                VSeparationOverride = VSeparation
            };
            AddChild(buttons);

            var uriOpener = IoCManager.Resolve<IUriOpener>();
            _cfg = IoCManager.Resolve<IConfigurationManager>();

            var rulesButton = NewLinkButton(Loc.GetString("server-info-rules-button"));
            rulesButton.OnPressed += args => new RulesAndInfoWindow().Open();
            buttons.AddChild(rulesButton);

            AddInfoButton("server-info-discord-button", CCVars.InfoLinksDiscord);
            AddInfoButton("server-info-website-button", CCVars.InfoLinksWebsite);
            AddInfoButton("server-info-wiki-button", CCVars.InfoLinksWiki);
            AddInfoButton("server-info-forum-button", CCVars.InfoLinksForum);
            AddInfoButton("server-info-telegram-button", CCVars.InfoLinksTelegram);

            var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
            var guidebookButton = NewLinkButton(Loc.GetString("server-info-guidebook-button"));
            guidebookButton.OnPressed += _ =>
            {
                guidebookController.ToggleGuidebook();
            };
            buttons.AddChild(guidebookButton);

            var changelogButton = new ChangelogButton();
            changelogButton.Visible = false;
            SizeLinkButton(changelogButton);
            changelogButton.OnPressed += args => UserInterfaceManager.GetUIController<ChangelogUIController>().ToggleWindow();
            buttons.AddChild(changelogButton);

            var roadmapButton = NewLinkButton(Loc.GetString("cm-ui-roadmap"));
            roadmapButton.AddStyleClass(StyleClass.Negative);
            roadmapButton.Visible = false;
            roadmapButton.OnPressed += _ => UserInterfaceManager.GetUIController<RoadmapUIController>().ToggleRoadmap();
            buttons.AddChild(roadmapButton);



            void AddInfoButton(string loc, CVarDef<string> cVar)
            {
                var button = NewLinkButton(Loc.GetString(loc));
                button.OnPressed += _ => uriOpener.OpenUri(_cfg.GetCVar(cVar));
                buttons.AddChild(button);
                _infoLinks.Add((cVar, button));
            }
        }

        private static Button NewLinkButton(string text)
        {
            var button = new Button
            {
                Text = text,
                StyleClasses = { StyleNano.StyleClassButtonBig }
            };
            SizeLinkButton(button);
            return button;
        }

        private static void SizeLinkButton(Button button)
        {
            button.MinHeight = ButtonMinHeight;
            button.HorizontalExpand = true;
        }

        protected override void EnteredTree()
        {
            // LinkBanner is constructed before the client even connects to the server due to UI refactor stuff.
            // We need to update these buttons when the UI is shown.

            base.EnteredTree();

            foreach (var (cVar, link) in _infoLinks)
            {
                link.Visible = _cfg.GetCVar(cVar) != "";
            }
        }
    }
}
