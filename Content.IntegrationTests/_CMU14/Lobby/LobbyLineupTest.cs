using System.Linq;
using System.Numerics;
using Content.Client.GameTicking.Managers;
using Content.Client.Lobby;
using Content.Client.Lobby.UI.ProfileEditorControls;
using Content.Client.CMU14.Lobby;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.Preferences;
using Content.Shared.CMU14.Lobby;
using Content.Shared.Chat;
using Content.Shared.Roles;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Lobby;

[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.LobbyPartyTime), true)]
public sealed class LobbyLineupTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true };

    [Test]
    public async Task CrossCardImpactsLayerOverMovesAndReleaseDisconnectedTargets()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var holder = new Control();
            ui.WindowRoot.AddChild(holder);
            try
            {
                var entries = LobbyLineupShowcaseCommand.CreateEntries(CProtoMan, 3);
                var cards = new List<LobbyLineupCard>();
                foreach (var entry in entries)
                {
                    var card = new LobbyLineupCard { AmbientEnabled = false };
                    holder.AddChild(card);
                    card.SetEntry(entry, cards.Count);
                    cards.Add(card);
                }
                var effects = new LobbyLineupInteractionOverlay();
                holder.AddChild(effects);
                cards[0].PlayEmote(LobbyLineupEmote.BurstFire);
                cards[1].PlayEmote(LobbyLineupEmote.Dance);
                var danceRevision = cards[1].EmoteRevision;
                effects.Add(cards[0], cards[1], LobbyLineupEmote.BurstFire, 17);
                effects.Advance(0.8f);
                Assert.That(cards[1].IsReacting, Is.False, "The first tracer has not arrived yet.");
                effects.Advance(0.05f);
                Assert.That(cards[1].IsReacting, Is.True);
                Assert.That(cards[1].IsPerforming, Is.True);
                Assert.That(cards[1].EmoteRevision, Is.EqualTo(danceRevision), "Hits must not replace the recipient's dance.");

                cards[2].PlayEmote(LobbyLineupEmote.PieToss);
                effects.Add(cards[2], cards[1], LobbyLineupEmote.PieToss, 12);
                effects.Advance(1.5f);
                Assert.That(effects.ActiveCount, Is.EqualTo(2), "A pie and a volley must coexist.");
                Assert.That(cards[1].EmoteRevision, Is.EqualTo(danceRevision));
                cards[1].PlayEmote(LobbyLineupEmote.Backflip);
                Assert.That(cards[1].React(LobbyLineupEmote.PieToss, 0), Is.EqualTo(LobbyLineupReaction.Duck),
                    "The visual effect must receive the same dodge outcome as the character pose.");
                var targetEntity = cards[1].StageEntity!.Value;
                cards[1].Orphan();
                cards[1].Dispose();
                effects.Advance(0.1f);
                Assert.That(CEntMan.Deleted(targetEntity), Is.True);
                Assert.That(effects.ActiveCount, Is.Zero);
                Assert.That(cards[0].HasInteractionAim, Is.False);

                effects.Add(cards[0], cards[2], LobbyLineupEmote.BurstFire, 4);
                cards[0].PlayEmote(LobbyLineupEmote.Salute);
                effects.Advance(0.1f);
                Assert.That(effects.ActiveCount, Is.Zero, "Changing a move must cancel its unfinished projectiles.");
                effects.Clear();
            }
            finally
            {
                holder.Orphan();
                holder.Dispose();
            }
        });
    }

    [Test]
    public async Task EmotesRequireReadinessAndRejectInvalidOrRepeatedRequests()
    {
        var ticker = SEntMan.System<GameTicker>();
        var social = CEntMan.System<Content.Client.CMU14.Lobby.LobbyLineupSystem>();
        var received = new List<LobbyLineupEmoteEvent>();
        await Client.WaitPost(() => social.EmoteReceived += received.Add);
        try
        {
            await Server.WaitPost(() => ticker.ToggleReady(ServerSession!, false));
            await Client.WaitPost(() => social.RequestEmote(LobbyLineupEmote.Wave));
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() => Assert.That(received, Is.Empty, "An unready player has no character to animate."));

            await Server.WaitPost(() => ticker.ToggleReady(ServerSession!, true));
            await Pair.RunTicksSync(5);
            await Client.WaitPost(() => social.RequestEmote((LobbyLineupEmote) byte.MaxValue));
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() => Assert.That(received, Is.Empty, "Unknown network enum values must be rejected."));

            await Client.WaitPost(() =>
            {
                social.RequestEmote(LobbyLineupEmote.SquadWorkout);
                social.RequestEmote(LobbyLineupEmote.Wave);
            });
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() =>
            {
                Assert.That(received, Has.Count.EqualTo(1), "Rapid follow-up actions must be rate limited.");
                Assert.That(received[0].Sender, Is.EqualTo(Client.User!.Value));
                Assert.That(received[0].Emote, Is.EqualTo(LobbyLineupEmote.SquadWorkout));
                Assert.That(received[0].Participants, Is.EquivalentTo(new[] { Client.User.Value }));
            });
        }
        finally
        {
            await Client.WaitPost(() => social.EmoteReceived -= received.Add);
            await Server.WaitPost(() => ticker.ToggleReady(ServerSession!, false));
            await Pair.RunTicksSync(5);
        }
    }

    [Test]
    public async Task ShowcaseKeepsEveryCharacterOnStageWithoutScrolling()
    {
        await Client.WaitAssertion(() =>
        {
            var lobby = (LobbyState) Client.ResolveDependency<IStateManager>().CurrentState;
            var panel = lobby.Lobby!.Lineup;
            var entries = LobbyLineupShowcaseCommand.CreateEntries(CProtoMan);
            Assert.That(entries, Has.Count.EqualTo(60));
            try
            {
                panel.SetShowcase(entries);
                Assert.That(Descendants(panel).OfType<ScrollContainer>(), Is.Empty);
                var board = Descendants(panel).OfType<LobbyLineupGrid>().Single(grid => grid.Sections);
                foreach (var card in Descendants(board).OfType<LobbyLineupCard>())
                    card.ShowBubble("Tactical moonwalk. Nobody put this in the report.");
                foreach (var size in new[] { new Vector2(480, 220), new Vector2(1050, 500), new Vector2(750, 320) })
                {
                    board.Measure(size);
                    board.Arrange(new UIBox2(Vector2.Zero, size));
                    // Font/bubble sizes settle after the resize. Unchanged text must not invalidate layout forever.
                    board.Measure(size);
                    board.Arrange(new UIBox2(Vector2.Zero, size));
                    Assert.That(board.IsMeasureValid, Is.True);
                    var cards = Descendants(board).OfType<LobbyLineupCard>().ToArray();
                    Assert.That(cards, Has.Length.EqualTo(entries.Count));
                    foreach (var card in cards)
                    {
                        var relative = card.GlobalPosition - board.GlobalPosition;
                        Assert.Multiple(() =>
                        {
                            Assert.That(card.VisibleInTree, Is.True);
                            Assert.That(card.Size.X, Is.GreaterThan(0));
                            Assert.That(card.Size.Y, Is.GreaterThan(0));
                            Assert.That(relative.X, Is.GreaterThanOrEqualTo(-0.1f));
                            Assert.That(relative.Y, Is.GreaterThanOrEqualTo(-0.1f));
                            Assert.That(relative.X + card.Size.X, Is.LessThanOrEqualTo(size.X + 0.1f));
                            Assert.That(relative.Y + card.Size.Y, Is.LessThanOrEqualTo(size.Y + 0.1f));
                        });
                    }
                }
            }
            finally
            {
                panel.SetShowcase(null);
            }
        });
    }

    [TestCase("AU14JobGOVFORPlatCo", "GOVFOR/command")]
    [TestCase("AU14JobGOVFORSquadRifleman", "GOVFOR/SquadGovforBravo")]
    public async Task ReadyLineupTracksAppearanceAndCleansUpPreviews(string job, string section)
    {
        var preferences = Server.ResolveDependency<IServerPreferencesManager>();
        var user = Client.User!.Value;
        var ticker = SEntMan.System<GameTicker>();
        var clientTicker = CEntMan.System<ClientGameTicker>();
        HumanoidCharacterProfile original = null;
        EntityUid preview = default;
        try
        {
            await Server.WaitPost(() =>
            {
                original = preferences.GetPreferences(user).SelectedCharacter;
                var profile = new HumanoidCharacterProfile()
                    .WithName("Lineup Tester")
                    .WithSpecies(original.Species)
                    .WithCharacterAppearance(original.Appearance)
                    .WithSquadPreference("SquadGovforBravo")
                    .WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority> { [job] = JobPriority.High });
                preferences.SetProfile(user, 0, profile).Wait();
                ticker.ToggleReady(ServerSession!, true);
            });
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() =>
            {
                var entry = clientTicker.LobbyLineup.Single();
                Assert.That(entry.Name, Is.EqualTo("Lineup Tester"));
                Assert.That(entry.Job?.Id, Is.EqualTo(job));
                Assert.That(entry.SectionId, Is.EqualTo(section));
                preview = GetLineupPreview();
                Assert.That(CEntMan.EntityExists(preview), Is.True);
                Assert.That(entry.ChatKey, Is.Not.Null);
                var lobby = (LobbyState) Client.ResolveDependency<IStateManager>().CurrentState;
                var chat = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();
                const string message = "[bold]test[/bold]";
                chat.ProcessChatMessage(new ChatMessage(ChatChannel.OOC, message, "test", default, entry.ChatKey));
                var card = Descendants(lobby.Lobby!.Lineup).OfType<LobbyLineupCard>().Single();
                var bubble = Descendants(card).OfType<Label>().Single(label => label.Text == message);
                Assert.That(bubble.VisibleInTree, Is.True, "The public chat key must match the actual character.");
                chat.OnDeleteChatMessagesBy(new MsgDeleteChatMessagesBy { Key = entry.ChatKey.Value, Entities = new() });
                Assert.That(bubble.VisibleInTree, Is.False, "Moderation deletions must also remove transient bubbles.");
                Assert.That(card.ToolTip, Does.Not.Contain(message), "Deleted messages must not linger in hover text.");
            });

            await Server.WaitPost(ticker.UpdateInfoText);
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() => Assert.That(GetLineupPreview(), Is.EqualTo(preview),
                "Unchanged roster packets must reuse the existing preview entity."));

            await Server.WaitPost(() => preferences.SetProfile(user, 0,
                preferences.GetPreferences(user).SelectedCharacter.WithName("Updated Fighter")).Wait());
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() =>
            {
                Assert.That(clientTicker.LobbyLineup.Single().Name, Is.EqualTo("Updated Fighter"));
                Assert.That(CEntMan.EntityExists(preview), Is.False, "Replacing a preview must delete the old dummy.");
                preview = GetLineupPreview();
            });

            await Server.WaitPost(() => ticker.ToggleReady(ServerSession!, false));
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                Assert.That(clientTicker.LobbyLineup, Is.Empty);
                Assert.That(CEntMan.EntityExists(preview), Is.False, "Unreadying must release preview entities.");
            });

            await Server.WaitPost(() => ticker.ToggleReadyAll(true));
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() => Assert.That(clientTicker.LobbyLineup, Has.Count.EqualTo(1)));
            await Server.WaitPost(() => ticker.ToggleReadyAll(false));
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() => Assert.That(clientTicker.LobbyLineup, Is.Empty));
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                ticker.ToggleReady(ServerSession!, false);
                if (original != null)
                    preferences.SetProfile(user, 0, original).Wait();
            });
            await Pair.RunTicksSync(5);
        }
    }

    private EntityUid GetLineupPreview()
    {
        var lobby = (LobbyState) Client.ResolveDependency<IStateManager>().CurrentState;
        return Descendants(lobby.Lobby!.Lineup).OfType<ProfilePreviewSpriteView>().Single().PreviewDummy;
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
