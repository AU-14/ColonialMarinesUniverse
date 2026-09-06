using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.Chat;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Client._CMU14.Interface;
using Content.Client._CMU14.UserInterface.Options;
using Content.Client.Lobby;
using Content.Client.Options.UI;
using Content.Client.Options.UI.Tabs;
using Content.Client.Stylesheets;
using Content.Client.Resources;
using Content.Client.Voting;
using Content.Client.Voting.UI;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.Lobby;

[TestFixture, NonParallelizable]
public sealed class OptionsThemeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [Test]
    public async Task KeybindRowsCollapseWithTheirCategoryAndCmuSettingsHaveOneHome()
    {
        await Client.WaitAssertion(() =>
        {
            using var menu = new OptionsMenu();
            menu.OpenCentered();
            menu.Tabs.CurrentTab = 2;
            var keybinds = menu.FindControl<KeyRebindTab>("KeyRebindTab");
            var sections = Descendants(keybinds).OfType<CmuOptionSection>().ToArray();
            var emotes = sections.Single(section => section.Title == Loc.GetString("ui-options-header-cmu-emotes"));
            var general = sections.Single(section => section.Title == Loc.GetString("ui-options-header-general"));

            Assert.Multiple(() =>
            {
                Assert.That(Descendants(emotes).OfType<OptionButton>().Count(), Is.EqualTo(8));
                Assert.That(Descendants(general).OfType<CheckBox>().Count(), Is.EqualTo(3));
                Assert.That(emotes.Expanded, Is.False);
                Assert.That(general.Expanded, Is.False);
            });

            var emotePicker = Descendants(emotes).OfType<OptionButton>().First();
            emotes.Expanded = true;
            Assert.That(emotePicker.VisibleInTree, Is.True);
            emotes.Expanded = false;
            Assert.That(emotePicker.VisibleInTree, Is.False);

            var cmu = menu.FindControl<CmuTab>("CmuTab");
            var accessibility = menu.FindControl<AccessibilityTab>("AccessibilityTab");
            foreach (var name in new[]
                     {
                         "ExplosionScreenShakeEnabledCheckBox", "ExplosionScreenShakeIgnoreFarCheckBox",
                         "FirearmScreenShakeEnabledCheckBox", "MuteScriptedSoundsCheckBox",
                     })
            {
                Assert.That(Descendants(cmu).Count(control => control.Name == name), Is.EqualTo(1), name);
                Assert.That(Descendants(accessibility).Any(control => control.Name == name), Is.False, name);
            }

            Assert.That(Descendants(cmu).OfType<CheckBox>().Single(control => control.Name == "ChatCrtHazeCheckBox"), Is.Not.Null);
            Assert.That(Descendants(cmu).OfType<OptionColorSlider>().Single(control => control.Name == "CrtUiColorSlider"), Is.Not.Null);
        });
    }

    [Test]
    public async Task ThemePreviewsRefreshLobbyAndChatAndHazeIsIndependent()
    {
        await Client.WaitAssertion(() =>
        {
            var config = Client.ResolveDependency<IConfigurationManager>();
            var styles = Client.ResolveDependency<IStylesheetManager>();
            var lobby = ((LobbyState) Client.ResolveDependency<IStateManager>().CurrentState).Lobby!;
            config.SetCVar(CCVars.CrtUiEnabled, true);
            config.SetCVar(CCVars.CrtUiColor, CCVars.CrtUiColorDefault);
            config.SetCVar(CCVars.CMUCrtMenuEffect, true);
            config.SetCVar(CCVars.CMUChatCrtHaze, true);

            try
            {
                var backing = lobby.FindControl<PanelContainer>("LobbyCrtBacking");
                var haze = lobby.FindControl<CrtScanlineOverlay>("ChatScanlines");
                var screen = lobby.FindControl<CrtScreenControl>("LobbyCrt");
                var green = ((StyleBoxFlat) backing.PanelOverride!).BackgroundColor;

                styles.PreviewCrtUi(true, "#58CCFF");
                var blue = ((StyleBoxFlat) backing.PanelOverride!).BackgroundColor;
                Assert.That(blue, Is.Not.EqualTo(green));
                Assert.That(blue.B, Is.GreaterThan(blue.G));
                AssertChatTabsReadable(lobby.Chat.TabBar);

                config.SetCVar(CCVars.CMUChatCrtHaze, false);
                Assert.That(haze.Visible, Is.False);
                Assert.That(screen.Visible, Is.True);
                config.SetCVar(CCVars.CMUChatCrtHaze, true);
                Assert.That(haze.Visible, Is.True);

                styles.PreviewCrtUi(false, "#58CCFF");
                var neutral = ((StyleBoxFlat) backing.PanelOverride!).BackgroundColor;
                Assert.That(neutral.R, Is.EqualTo(neutral.G));
                Assert.That(haze.Visible, Is.False);
                Assert.That(screen.Visible, Is.False);
                Assert.That(lobby.CharacterPreview.IgnoreAllegianceToggle.Label.FontOverride, Is.Null);
                AssertChatTabsReadable(lobby.Chat.TabBar);

                styles.ResetCrtUiPreview();
                Assert.That(((StyleBoxFlat) backing.PanelOverride!).BackgroundColor, Is.EqualTo(green));
                Assert.That(haze.Visible, Is.True);
                Assert.That(screen.Visible, Is.True);
                Assert.That(config.GetCVar(CCVars.CMUChatRowTint), Is.EqualTo(CCVars.CMUChatRowTintFull));
            }
            finally
            {
                styles.ResetCrtUiPreview();
            }
        });
    }

    [Test]
    public async Task FontChoicesUpdateExistingOverridesAndRestoreTheOriginalFont()
    {
        await Client.WaitAssertion(() =>
        {
            var config = Client.ResolveDependency<IConfigurationManager>();
            var resources = Client.ResolveDependency<IResourceCache>();
            var original = resources.NotoStack(size: 16);
            var styles = Client.ResolveDependency<IStylesheetManager>();
            using var label = new Label
            {
                Text = "Mixed case UI text", FontOverride = original, Stylesheet = styles.SheetNano,
            };
            Client.ResolveDependency<IUserInterfaceManager>().StateRoot.AddChild(label);
            foreach (var family in new[]
                     {
                         CCVars.CMUUiFontNotoSans, CCVars.CMUUiFontComicSans, CCVars.CMUUiFontRobotoMono,
                         CCVars.CMUUiFontCozette, CCVars.CMUUiFontNotoSansDisplay,
                     })
            {
                config.SetCVar(CCVars.CMUUiFont, family);
                Assert.That(label.Stylesheet, Is.SameAs(styles.SheetNano), family);
                Assert.That(label.FontOverride, Is.Not.SameAs(original), family);
                Assert.That(label.FontOverride, Is.TypeOf<StackedFont>(), family);
                Assert.That(((VectorFont) ((StackedFont) label.FontOverride!).Stack[0]).Size, Is.EqualTo(16), family);
                Assert.That(FontTag.GetSizeForFontTag(new Stack<Font>([label.FontOverride]),
                    new MarkupNode("bold")), Is.EqualTo(16), "Formatting must preserve the chosen text size.");
            }

            config.SetCVar(CCVars.CMUUiFont, "unknown-font");
            Assert.That(label.FontOverride, Is.SameAs(original), "Unknown saved choices fall back safely.");
            config.SetCVar(CCVars.CMUUiFont, CCVars.CMUUiFontTheme);
            Assert.That(label.FontOverride, Is.SameAs(original));
        });
    }

    [Test]
    public async Task FirstTimeSetupCanBeDeferredAndCompletionIsRemembered()
    {
        await Client.WaitAssertion(() =>
        {
            var config = Client.ResolveDependency<IConfigurationManager>();
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var initial = ui.AllRoots.SelectMany(Descendants).OfType<CmuUiSetupWindow>().Single();
            Assert.That(CmuUiSetupWindow.NeedsSetup(config), Is.True);
            initial.Close();
            Assert.That(config.GetCVar(CCVars.CMUUiConfigured), Is.False);
            Assert.That(CmuUiSetupWindow.NeedsSetup(config), Is.True);

            using var setup = new CmuUiSetupWindow();
            setup.OpenCentered();
            var appearance = Descendants(setup).OfType<CmuAppearanceOptions>().Single();
            Assert.That(Descendants(appearance).OfType<OptionDropDown>()
                .Single(control => control.Name == "UiFontDropDown").Button.ItemCount, Is.EqualTo(6));
            Descendants(appearance).OfType<CheckBox>()
                .Single(control => control.Name == "CrtUiEnabledCheckBox").Pressed = false;
            setup.SaveAndContinue();
            Assert.That(setup.IsOpen, Is.False);
            Assert.That(config.GetCVar(CCVars.CrtUiEnabled), Is.False);
            Assert.That(config.GetCVar(CCVars.CMUUiConfigured), Is.True);
            Assert.That(CmuUiSetupWindow.NeedsSetup(config), Is.False);

            config.SetCVar(CCVars.CMUUiConfigured, false);
            Assert.That(CmuUiSetupWindow.NeedsSetup(config), Is.False,
                "Returning players with custom settings should not be prompted.");
            config.SetCVar(CCVars.CrtUiEnabled, true);
            config.SetCVar(CCVars.CMUChatRowTint, CCVars.CMUChatRowTintMuted);
            Assert.That(CmuUiSetupWindow.NeedsSetup(config), Is.True,
                "The former default tint is not evidence that a returning player configured the UI.");
        });
    }

    [Test]
    public async Task VoteLabelsKeepContrastAcrossCustomColoursAndThemeChanges()
    {
        await Client.WaitAssertion(() =>
        {
            var styles = Client.ResolveDependency<IStylesheetManager>();
            var vote = new VoteManager.ActiveVote(42)
            {
                Title = "Choose the next map", Initiator = "Player", DisplayVotes = true, OurVote = 0,
                Entries = [new VoteManager.VoteEntry("Option one") { Votes = 1 }, new VoteManager.VoteEntry("Option two")],
            };
            using var popup = new VotePopup(vote);
            foreach (var colour in new[] { "#281029", "#0000FF", "#FF0000", "#FFFF00", "#FFFFFF", "#000000" })
            {
                styles.PreviewCrtUi(true, colour);
                popup.UpdateData();
                var background = ((CrtStyleBox) popup.FindControl<PanelContainer>("VotePanel").PanelOverride!).BackgroundColor;
                Assert.That(background.A, Is.EqualTo(1f));
                Assert.That(popup.Modulate.A, Is.EqualTo(1f));
                Assert.That(Contrast(popup.FindControl<Label>("VoteCaller").FontColorOverride!.Value, background),
                    Is.GreaterThanOrEqualTo(4.5), colour);
                foreach (var button in popup.FindControl<GridContainer>("VoteOptionsContainer").Children.OfType<Button>())
                {
                    var box = (CmuVoteBarStyleBox) button.StyleBoxOverride!;
                    foreach (var label in button.Children.OfType<Label>())
                    {
                        Assert.That(Contrast(label.FontColorOverride!.Value, box.TrackColor), Is.GreaterThanOrEqualTo(4.5), colour);
                        Assert.That(Contrast(label.FontColorOverride!.Value, box.FillColor), Is.GreaterThanOrEqualTo(4.5), colour);
                    }
                }
            }

            styles.PreviewCrtUi(false, "#281029");
            foreach (var button in popup.FindControl<GridContainer>("VoteOptionsContainer").Children.OfType<Button>())
                Assert.That(button.StyleBoxOverride, Is.Null);
            styles.ResetCrtUiPreview();
        });
    }

    [Test]
    public async Task ChatHistoryChangesFollowLatestAndManualScrollingStaysPut()
    {
        await Client.WaitAssertion(() =>
        {
            using var panel = new ChatLogPanel();
            var scroll = Descendants(panel).OfType<ScrollContainer>().Single();
            var gutter = Descendants(panel).OfType<VScrollBar>()
                .Single(bar => bar.Parent is not ScrollContainer);
            var latest = Descendants(panel).OfType<Button>().Single();
            var frameUpdate = typeof(ChatLogPanel).GetMethod("FrameUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var bounds = new UIBox2(Vector2.Zero, new Vector2(300, 160));

            void Settle()
            {
                for (var frame = 0; frame < 16; frame++)
                {
                    panel.Measure(bounds.Size);
                    panel.Arrange(bounds);
                    frameUpdate.Invoke(panel, [new FrameEventArgs(1f / 60f)]);
                }
            }

            void Populate(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    var text = $"Message {i}";
                    panel.AddMessage(new ChatMessage(ChatChannel.OOC, text, "", default, null),
                        FormattedMessage.FromUnformatted(text), Color.White);
                }
            }

            Populate(80);
            Settle();
            Assert.That(scroll.VScroll, Is.GreaterThan(0));
            Assert.That(gutter.IsAtEnd, Is.True);
            Assert.That(latest.Visible, Is.False);

            // Relayout can clamp an offset without any input, particularly while replacing a tab.
            scroll.VScroll = 0;
            Settle();
            Assert.That(gutter.IsAtEnd, Is.True);
            Assert.That(latest.Visible, Is.False);

            gutter.Value = 0;
            Settle();
            Assert.That(scroll.VScroll, Is.Zero);
            Assert.That(latest.Visible, Is.True);
            Populate(1);
            Settle();
            Assert.That(scroll.VScroll, Is.Zero, "New messages must not interrupt reading older messages.");

            panel.Clear();
            Populate(20);
            panel.ScrollToBottom();
            Settle();
            Assert.That(gutter.IsAtEnd, Is.True);
            Assert.That(latest.Visible, Is.False, "Switching history resumes following the latest messages.");
        });
    }


    [Test]
    public async Task SetupWrapsTextAndKeepsOptionsWithinTheWindow()
    {
        await Client.WaitAssertion(() =>
        {
            var config = Client.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.CMUUiFont, CCVars.CMUUiFontComicSans);
            using var setup = new CmuUiSetupWindow();
            setup.OpenCentered();
            var bounds = new UIBox2(Vector2.Zero, new Vector2(560, 520));
            setup.Measure(bounds.Size);
            setup.Arrange(bounds);
            var scroll = Descendants(setup).OfType<ScrollContainer>().Single();
            var introduction = setup.FindControl<RichTextLabel>("Introduction");
            Assert.That(introduction.Width, Is.LessThanOrEqualTo(scroll.Width));
            Assert.That(introduction.Height, Is.GreaterThan(30), "The introduction must wrap at narrow widths.");
            foreach (var option in Descendants(setup).OfType<OptionDropDown>())
            {
                var button = option.Button;
                var rightEdge = button.GlobalPosition.X + button.Width;
                Assert.That(rightEdge, Is.LessThanOrEqualTo(scroll.GlobalPosition.X + scroll.Width), option.Name);
            }
            config.SetCVar(CCVars.CMUUiFont, CCVars.CMUUiFontTheme);
        });
    }

    private static double Contrast(Color foreground, Color background)
    {
        static double Linear(float component) => component <= 0.04045
            ? component / 12.92
            : Math.Pow((component + 0.055) / 1.055, 2.4);
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        var a = Luminance(foreground);
        var b = Luminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
    private static void AssertChatTabsReadable(Control tabBar)
    {
        var buttons = tabBar.Children.OfType<ChatTabButton>().ToArray();
        Assert.That(buttons, Has.Length.GreaterThan(1));
        foreach (var button in buttons)
        {
            button.ForceRunStyleUpdate();
            Assert.That(button.Modulate, Is.EqualTo(Color.White));
            Assert.That(button.Label.FontColorOverride, Is.EqualTo(button.Pressed
                ? CrtTerminalPalette.TextBright
                : CrtTerminalPalette.Text));
            Assert.That(button.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var box), Is.True);
            Assert.That(box, Is.TypeOf<StyleBoxFlat>());
            Assert.That(((StyleBoxFlat) box!).BackgroundColor.A, Is.EqualTo(1f));
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
