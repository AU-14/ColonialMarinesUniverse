using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chat;
using Content.Shared.CMU14.Basketball;
using Content.Shared.DeviceLinking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Basketball;

/// <summary>
///     Hoops and reset buttons only drive the scoreboards they are multitool-linked to.
/// </summary>
[TestFixture]
[TestOf(typeof(CMUBasketballSystem))]
public sealed class CMUBasketballTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task LinkedScoreboardsTrackBasketsAndReset()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var basketball = SEntMan.System<CMUBasketballSystem>();
            var deviceLink = SEntMan.System<SharedDeviceLinkSystem>();

            var left = SEntMan.SpawnEntity("CMUBasketballHoopLeft", map.GridCoords);
            var right = SEntMan.SpawnEntity("CMUBasketballHoopRight", map.GridCoords);
            var board = SEntMan.SpawnEntity("CMUBasketballScoreboard", map.GridCoords);
            var mirror = SEntMan.SpawnEntity("CMUBasketballScoreboard", map.GridCoords);
            var unlinked = SEntMan.SpawnEntity("CMUBasketballScoreboard", map.GridCoords);
            var reset = SEntMan.SpawnEntity("CMUBasketballReset", map.GridCoords);
            var ball = SEntMan.SpawnEntity("Basketball", map.GridCoords);

            deviceLink.LinkDefaults(null, left, board);
            deviceLink.LinkDefaults(null, right, board);
            deviceLink.LinkDefaults(null, left, mirror);
            deviceLink.LinkDefaults(null, reset, board);

            var leftHoop = (left, SEntMan.GetComponent<CMUBasketballHoopComponent>(left));
            var rightHoop = (right, SEntMan.GetComponent<CMUBasketballHoopComponent>(right));
            var boardComp = SEntMan.GetComponent<CMUBasketballScoreboardComponent>(board);
            var mirrorComp = SEntMan.GetComponent<CMUBasketballScoreboardComponent>(mirror);
            var unlinkedComp = SEntMan.GetComponent<CMUBasketballScoreboardComponent>(unlinked);

            Assert.That(basketball.TryDunkItem(leftHoop, ball), Is.True);
            Assert.That(basketball.TryDunkItem(rightHoop, ball), Is.True);
            Assert.That(basketball.TryDunkItem(rightHoop, ball), Is.True);

            Assert.That(boardComp.LeftScore, Is.EqualTo(2));
            Assert.That(boardComp.RightScore, Is.EqualTo(4));
            Assert.That(mirrorComp.LeftScore, Is.EqualTo(2), "Every scoreboard linked to a hoop gets its baskets.");
            Assert.That(mirrorComp.RightScore, Is.Zero, "A scoreboard only counts the hoops linked to it.");
            Assert.That(unlinkedComp.LeftScore + unlinkedComp.RightScore, Is.Zero);

            var resetEnt = (reset, SEntMan.GetComponent<CMUBasketballResetComponent>(reset));
            Assert.That(basketball.TryResetCourt(resetEnt), Is.True);
            Assert.That(boardComp.LeftScore + boardComp.RightScore, Is.Zero);
            Assert.That(mirrorComp.LeftScore, Is.EqualTo(2), "The reset only clears the scoreboards linked to it.");
            Assert.That(basketball.TryResetCourt(resetEnt), Is.False, "The reset button has a cooldown.");

            var lonelyReset = SEntMan.SpawnEntity("CMUBasketballReset", map.GridCoords);
            Assert.That(basketball.TryResetCourt((lonelyReset, SEntMan.GetComponent<CMUBasketballResetComponent>(lonelyReset))),
                Is.False);
        });
    }

    [Test]
    public async Task ScoreboardAnnouncesTeamNames()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var basketball = SEntMan.System<CMUBasketballSystem>();
            var deviceLink = SEntMan.System<SharedDeviceLinkSystem>();
            var left = SEntMan.SpawnEntity("CMUBasketballHoopLeft", map.GridCoords);
            var right = SEntMan.SpawnEntity("CMUBasketballHoopRight", map.GridCoords);
            var board = SEntMan.SpawnEntity("CMUBasketballScoreboard", map.GridCoords);
            var ball = SEntMan.SpawnEntity("Basketball", map.GridCoords);
            var spoken = SEntMan.EnsureComponent<CMUBasketballSpeechProbeComponent>(board);
            deviceLink.LinkDefaults(null, left, board);
            deviceLink.LinkDefaults(null, right, board);

            var boardEnt = (board, SEntMan.GetComponent<CMUBasketballScoreboardComponent>(board));
            var limit = boardEnt.Item2.TeamNameLimit;

            // The dialog raises this event on the scoreboard once the player submits a name.
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var nameEvent = new CMUBasketballTeamNameEvent(SEntMan.GetNetEntity(user), CMUBasketballTeam.Left, "  Marines\n");
            SEntMan.EventBus.RaiseLocalEvent(board, (object) nameEvent);
            basketball.SetTeamName(boardEnt, CMUBasketballTeam.Right, new string('X', limit + 10));

            Assert.That(basketball.GetTeamName(boardEnt, CMUBasketballTeam.Left), Is.EqualTo("Marines"));
            Assert.That(basketball.GetTeamName(boardEnt, CMUBasketballTeam.Right), Has.Length.EqualTo(limit));

            spoken.Messages.Clear();
            basketball.TryDunkItem((left, SEntMan.GetComponent<CMUBasketballHoopComponent>(left)), ball);
            Assert.That(spoken.Messages, Has.Some.Contains("Marines"),
                "The scoreboard says which team scored, like a player speaking.");

            basketball.SetTeamName(boardEnt, CMUBasketballTeam.Left, "   ");
            Assert.That(basketball.GetTeamName(boardEnt, CMUBasketballTeam.Left), Is.EqualTo("Left"),
                "A blank name resets the side to its default.");
        });
    }

    [Test]
    public async Task FirstToTargetWinsAndLocksSettings()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var basketball = SEntMan.System<CMUBasketballSystem>();
            var deviceLink = SEntMan.System<SharedDeviceLinkSystem>();
            var left = SEntMan.SpawnEntity("CMUBasketballHoopLeft", map.GridCoords);
            var right = SEntMan.SpawnEntity("CMUBasketballHoopRight", map.GridCoords);
            var board = SEntMan.SpawnEntity("CMUBasketballScoreboard", map.GridCoords);
            var spoken = SEntMan.EnsureComponent<CMUBasketballSpeechProbeComponent>(board);
            var reset = SEntMan.SpawnEntity("CMUBasketballReset", map.GridCoords);
            var ball = SEntMan.SpawnEntity("Basketball", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            deviceLink.LinkDefaults(null, left, board);
            deviceLink.LinkDefaults(null, right, board);
            deviceLink.LinkDefaults(null, reset, board);

            Assert.That(SEntMan.GetComponent<MetaDataComponent>(left).EntityName, Is.EqualTo("left basketball hoop"),
                "Entity names come from the locale files so forks can translate them.");

            var comp = SEntMan.GetComponent<CMUBasketballScoreboardComponent>(board);
            var boardEnt = (board, comp);
            var leftHoop = (left, SEntMan.GetComponent<CMUBasketballHoopComponent>(left));
            var rightHoop = (right, SEntMan.GetComponent<CMUBasketballHoopComponent>(right));
            var netUser = SEntMan.GetNetEntity(user);

            // Pick "first to 11" from the menu before the game starts.
            SEntMan.EventBus.RaiseLocalEvent(board, (object) new CMUBasketballTargetScoreEvent(netUser, 11));
            Assert.That(comp.TargetScore, Is.EqualTo(11));
            SEntMan.EventBus.RaiseLocalEvent(board, (object) new CMUBasketballTargetScoreEvent(netUser, 7));
            Assert.That(comp.TargetScore, Is.EqualTo(11), "Scores the menu didn't offer are ignored.");

            basketball.TryDunkItem(rightHoop, ball);
            Assert.That(basketball.IsGameInProgress(boardEnt), Is.True);

            SEntMan.EventBus.RaiseLocalEvent(board,
                (object) new CMUBasketballTeamNameEvent(netUser, CMUBasketballTeam.Left, "Late"));
            SEntMan.EventBus.RaiseLocalEvent(board, (object) new CMUBasketballTargetScoreEvent(netUser, 31));
            Assert.That(comp.LeftTeamName, Is.Null, "Teams can't be renamed mid-game.");
            Assert.That(comp.TargetScore, Is.EqualTo(11), "The winning score can't move mid-game.");

            spoken.Messages.Clear();
            for (var i = 0; i < 6; i++)
            {
                basketball.TryDunkItem(leftHoop, ball);
            }

            Assert.That(comp.Winner, Is.EqualTo(CMUBasketballTeam.Left));
            Assert.That(comp.LeftScore, Is.EqualTo(12));
            Assert.That(spoken.Messages, Has.Some.Contains("wins, 12 to 2"));

            basketball.TryDunkItem(rightHoop, ball);
            Assert.That(comp.RightScore, Is.EqualTo(2), "A finished game ignores baskets.");
            Assert.That(basketball.IsGameInProgress(boardEnt), Is.False, "Settings unlock once the game is won.");

            basketball.TryResetCourt((reset, SEntMan.GetComponent<CMUBasketballResetComponent>(reset)));
            Assert.That(comp.Winner, Is.Null);
            Assert.That(comp.LeftScore + comp.RightScore, Is.Zero);
            Assert.That(comp.TargetScore, Is.EqualTo(11), "Reset keeps the chosen winning score.");
        });
    }

    [Test]
    public async Task RussianLocaleCoversEveryString()
    {
        await Server.WaitAssertion(() =>
        {
            var loc = Server.ResolveDependency<ILocalizationManager>();
            var english = new CultureInfo("en-US");
            var russian = new CultureInfo("ru-RU");
            var keys = new List<string>();
            foreach (var line in File.ReadLines(FindLocale("en-US")))
            {
                var match = Regex.Match(line, @"^([a-zA-Z][\w-]*) =");
                if (match.Success)
                    keys.Add(match.Groups[1].Value);
            }

            Assert.That(keys, Is.Not.Empty);

            if (!loc.HasCulture(russian))
                loc.LoadCulture(russian);

            try
            {
                loc.SetCulture(russian);
                var args = new (string, object)[]
                {
                    ("team", "Морпехи"), ("points", 2), ("score", 21), ("otherScore", 15), ("side", "left"),
                    ("name", "Морпехи"), ("target", "До 21 очка"), ("left", "А"), ("right", "Б"),
                    ("leftScore", 1), ("rightScore", 2),
                };

                foreach (var key in keys)
                {
                    Assert.That(loc.TryGetString(key, out _, args), Is.True, $"ru-RU is missing {key}");
                }

                Assert.That(loc.GetString("cmu-basketball-shot", ("team", "Морпехи"), ("points", 2)),
                    Is.EqualTo("Команда «Морпехи» забивает 2 очка!"));
                Assert.That(loc.GetString("cmu-basketball-target-score", ("score", 21)), Is.EqualTo("До 21 очка"));
                Assert.That(loc.GetString("cmu-basketball-target-score", ("score", 11)), Is.EqualTo("До 11 очков"));
                Assert.That(loc.GetString("ent-CMUBasketballScoreboard"), Is.EqualTo("баскетбольное табло"));
            }
            finally
            {
                loc.SetCulture(english);
            }
        });
    }

    private static string FindLocale(string culture)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null)
        {
            var path = Path.Combine(dir.FullName, "Content.CMU", "Resources", "Locale", culture,
                "CMU14", "basketball", "basketball.ftl");
            if (File.Exists(path))
                return path;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Couldn't find the {culture} basketball locale.");
    }

    [Test]
    public async Task MultitoolIsNotDunked()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hands = SEntMan.System<SharedHandsSystem>();
            var deviceLink = SEntMan.System<SharedDeviceLinkSystem>();

            var hoop = SEntMan.SpawnEntity("CMUBasketballHoopLeft", map.GridCoords);
            var board = SEntMan.SpawnEntity("CMUBasketballScoreboard", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var multitool = SEntMan.SpawnEntity("CMMultitool", map.GridCoords);
            deviceLink.LinkDefaults(null, hoop, board);

            Assert.That(hands.TryPickupAnyHand(user, multitool), Is.True);
            var ev = new InteractUsingEvent(user, multitool, hoop, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(hoop, ev);

            Assert.That(ev.Handled, Is.False, "The multitool must reach the device linking interaction.");
            Assert.That(hands.IsHolding(user, multitool), Is.True);
            Assert.That(SEntMan.GetComponent<CMUBasketballScoreboardComponent>(board).LeftScore, Is.Zero);
        });
    }
}

/// <summary>
///     Records what the entity it is added to says in character, so tests can check the scoreboard's speech.
/// </summary>
[RegisterComponent]
public sealed partial class CMUBasketballSpeechProbeComponent : Component
{
    public readonly List<string> Messages = new();
}

/// <summary>
///     Only listens on entities with <see cref="CMUBasketballSpeechProbeComponent"/>, since test systems are
///     loaded into every integration test server.
/// </summary>
public sealed partial class CMUBasketballSpeechProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBasketballSpeechProbeComponent, EntitySpokeEvent>(OnSpoke);
    }

    private static void OnSpoke(EntityUid uid, CMUBasketballSpeechProbeComponent comp, EntitySpokeEvent args)
    {
        comp.Messages.Add(args.Message);
    }
}
