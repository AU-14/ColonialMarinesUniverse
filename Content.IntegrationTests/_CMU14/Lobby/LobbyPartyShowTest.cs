using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Lobby;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Lobby;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Lobby;

[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.LobbyPartyTime), true)]
public sealed class LobbyPartyShowTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true };

    [SetUp]
    public async Task EnableShows()
    {
        await OverrideCVar(Side.Server, CCVars.LobbyPartyTimeFlyby, true);
        await OverrideCVar(Side.Server, CCVars.LobbyPartyTimeParade, true);
    }

    public LobbyPartyShowTest()
    {
        PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);
    }

    [Test]
    public async Task QueuedParadeWaitsForArrivalAndKeepsPortraitsHidden()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var panel = new LobbyLineupPanel();
            ui.WindowRoot.AddChild(panel);
            // Headless ticks do not render. Pump the engine's public frame method to exercise the
            // real order: control updates, layout, then deferred attachment of the stage overlay.
            var update = ui.GetType().GetMethod("FrameUpdate")!;
            void Frame() => update.Invoke(ui, new object[] { new FrameEventArgs(0.05f) });
            try
            {
                panel.SetShowcase(LobbyLineupShowcaseCommand.CreateEntries(CProtoMan, 6), initialShow: LobbyPartyShow.Parade);
                Frame();
                Assert.That(ui.WindowRoot.Children.OfType<LobbyPartyShowControl>(), Is.Empty,
                    "An unmeasured lineup must finish its arrival before lending previews to the parade.");
                for (var frame = 0; frame < 90; frame++)
                {
                    // The headless window has no framebuffer dimensions. Supply the viewport that a
                    // native client would give both the lineup and its full-screen transition layer.
                    var size = new Vector2(1280, 900);
                    panel.Measure(size);
                    panel.Arrange(new UIBox2(Vector2.Zero, size));
                    foreach (var stage in ui.WindowRoot.Children.Where(child => child is LobbyLineupStageTransition or LobbyPartyShowControl))
                    {
                        stage.Measure(size);
                        stage.Arrange(new UIBox2(Vector2.Zero, size));
                    }
                    Frame();
                }
                Assert.That(ui.WindowRoot.Children.OfType<LobbyPartyShowControl>().ToArray(), Has.Length.EqualTo(1));
                var cards = Descendants(panel).OfType<LobbyLineupCard>().ToArray();
                Assert.That(cards, Has.Length.EqualTo(6));
                foreach (var card in cards)
                {
                    Assert.That(card.StageMoving, Is.True, "Arrival cleanup must not reclaim the parade's cast.");
                    Assert.That(card.FindControl<SpriteView>("Preview").Visible, Is.False);
                }
            }
            finally
            {
                panel.Orphan();
                panel.Dispose();
                Frame();
            }
            Assert.That(ui.WindowRoot.Children.OfType<LobbyPartyShowControl>(), Is.Empty);
        });
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        yield return control;
        foreach (var child in control.Children)
        foreach (var descendant in Descendants(child))
            yield return descendant;
    }

    [TestCase(false, LobbyPartyShow.Parade)]
    [TestCase(true, LobbyPartyShow.Parade)]
    [TestCase(false, LobbyPartyShow.SupplyScramble)]
    [TestCase(true, LobbyPartyShow.SupplyScramble)]
    public async Task DisablingParadeCancelsItsPendingOrActiveShow(bool pending, LobbyPartyShow kind)
    {
        LobbyLineupPanel? panel = null;
        LobbyPartyShowControl? active = null;
        var ui = Client.ResolveDependency<IUserInterfaceManager>();
        var update = ui.GetType().GetMethod("FrameUpdate")!;
        void Frame() => update.Invoke(ui, new object[] { new FrameEventArgs(0.05f) });
        try
        {
            await Client.WaitAssertion(() =>
            {
                panel = new LobbyLineupPanel();
                ui.WindowRoot.AddChild(panel);
                panel.SetShowcase(LobbyLineupShowcaseCommand.CreateEntries(CProtoMan, 6), initialShow: kind);
                if (pending)
                    return;
                for (var frame = 0; frame < 90; frame++)
                {
                    var size = new Vector2(1280, 900);
                    panel.Measure(size);
                    panel.Arrange(new UIBox2(Vector2.Zero, size));
                    foreach (var stage in ui.WindowRoot.Children.Where(child => child is LobbyLineupStageTransition or LobbyPartyShowControl))
                    {
                        stage.Measure(size);
                        stage.Arrange(new UIBox2(Vector2.Zero, size));
                    }
                    Frame();
                }
                active = ui.WindowRoot.Children.OfType<LobbyPartyShowControl>().Single();
            });
            await Server.WaitPost(() => Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.LobbyPartyTimeParade, false));
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() =>
            {
                Frame();
                Assert.That(panel!.FindControl<Button>("Parade").Visible, Is.False);
                Assert.That(panel.FindControl<Button>("SupplyScramble").Visible, Is.False);
                Assert.That(panel.FindControl<Button>("Flyby").Visible, Is.True);
                Assert.That(panel.FindControl<Button>("StopShow").Visible, Is.False,
                    "A pending show must also be cancelled when its switch is disabled.");
                Assert.That(ui.WindowRoot.Children.OfType<LobbyPartyShowControl>(), Is.Empty);
                if (active != null)
                {
                    Assert.That(active.Finished, Is.True);
                    Assert.That(Descendants(panel).OfType<LobbyLineupCard>().All(card => !card.StageMoving), Is.True);
                }
            });
        }
        finally
        {
            await Client.WaitPost(() => { panel?.Orphan(); panel?.Dispose(); Frame(); });
            await Server.WaitPost(() => Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.LobbyPartyTimeParade, true));
            await Pair.RunTicksSync(5);
        }
    }

    [TestCase(LobbyPartyShow.Parade)]
    [TestCase(LobbyPartyShow.SupplyScramble)]
    public async Task ShowsRequireReadinessAndShareOneCooldown(LobbyPartyShow kind)
    {
        var ticker = SEntMan.System<GameTicker>();
        var social = CEntMan.System<LobbyLineupSystem>();
        var received = new List<LobbyPartyShowEvent>();
        await Client.WaitPost(() => social.ShowReceived += received.Add);
        try
        {
            await Server.WaitPost(() => ticker.ToggleReady(ServerSession!, false));
            await Client.WaitPost(() => social.RequestShow(kind));
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() => Assert.That(received, Is.Empty));
            await Server.WaitPost(() => ticker.ToggleReady(ServerSession!, true));
            await Pair.RunTicksSync(5);
            await Client.WaitPost(() => social.RequestShow((LobbyPartyShow) byte.MaxValue));
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() => Assert.That(received, Is.Empty));
            await Client.WaitPost(() =>
            {
                social.RequestShow(kind);
                social.RequestShow(LobbyPartyShow.Flyby);
            });
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() =>
            {
                Assert.That(received, Has.Count.EqualTo(1), "A flyby must not overlap a parade requested on the same tick.");
                Assert.That(received[0].Show, Is.EqualTo(kind));
                Assert.That(received[0].Automatic, Is.False);
                Assert.That(received[0].Participants, Is.EquivalentTo(new[] { Client.User!.Value }));
            });
            await Server.WaitPost(() => Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.LobbyPartyTimeFlyby, false));
            await Client.WaitPost(() => social.RequestShow(LobbyPartyShow.Flyby));
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() => Assert.That(received, Has.Count.EqualTo(1), "A disabled variant must never broadcast a show."));
        }
        finally
        {
            await Client.WaitPost(() => social.ShowReceived -= received.Add);
            await Server.WaitPost(() =>
            {
                ticker.ToggleReady(ServerSession!, false);
                Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.LobbyPartyTimeFlyby, true);
            });
            await Pair.RunTicksSync(5);
        }
    }

    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, false, false)]
    public async Task VariantSwitchesIndependentlyEnableTheLineupAndSelectOnlyAllowedShows(bool basic, bool flyby, bool parade)
    {
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        var ticker = SEntMan.System<GameTicker>();
        LobbyLineupPanel? panel = null;
        try
        {
            await Server.WaitAssertion(() =>
            {
                cfg.SetCVar(CCVars.LobbyPartyTime, basic);
                cfg.SetCVar(CCVars.LobbyPartyTimeFlyby, flyby);
                cfg.SetCVar(CCVars.LobbyPartyTimeParade, parade);
                ticker.ToggleReady(ServerSession!, true);
                Assert.That(ticker.GetLobbyLineup().Count > 0, Is.EqualTo(basic || flyby || parade));
                foreach (var preferred in new[] { LobbyPartyShow.Flyby, LobbyPartyShow.Parade })
                {
                    Assert.That(LobbyPartySettings.TryNextShow(cfg, preferred, out var selected), Is.EqualTo(flyby || parade));
                    if (flyby || parade)
                        Assert.That(selected, Is.EqualTo(flyby && parade ? preferred : flyby ? LobbyPartyShow.Flyby : LobbyPartyShow.Parade));
                }
                var social = SEntMan.System<Content.Server.CMU14.Lobby.LobbyLineupSystem>();
                if (!flyby)
                    Assert.That(social.TryShow(ServerSession!, LobbyPartyShow.Flyby), Is.False);
                if (!parade)
                    Assert.That(social.TryShow(ServerSession!, LobbyPartyShow.Parade), Is.False);
            });
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() =>
            {
                panel = new LobbyLineupPanel();
                Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.AddChild(panel);
                panel.SetShowcase(LobbyLineupShowcaseCommand.CreateEntries(CProtoMan, 6));
                Assert.That(panel.Visible, Is.EqualTo(basic || flyby || parade));
                if (!panel.Visible)
                    return;
                Assert.That(panel.FindControl<Button>("Flyby").Visible, Is.EqualTo(flyby));
                Assert.That(panel.FindControl<Button>("Parade").Visible, Is.EqualTo(parade));
            });
        }
        finally
        {
            await Client.WaitPost(() => { panel?.Orphan(); panel?.Dispose(); });
            await Server.WaitPost(() =>
            {
                ticker.ToggleReady(ServerSession!, false);
                cfg.SetCVar(CCVars.LobbyPartyTime, true);
                cfg.SetCVar(CCVars.LobbyPartyTimeFlyby, true);
                cfg.SetCVar(CCVars.LobbyPartyTimeParade, true);
            });
            await Pair.RunTicksSync(5);
        }
    }

    [TestCase(LobbyPartyShow.Parade, false)]
    [TestCase(LobbyPartyShow.Flyby, false)]
    [TestCase(LobbyPartyShow.Parade, true)]
    [TestCase(LobbyPartyShow.Flyby, true)]
    [TestCase(LobbyPartyShow.SupplyScramble, false)]
    [TestCase(LobbyPartyShow.SupplyScramble, true)]
    public async Task ShowReturnsBorrowedPreviewsAndSurvivesUnready(LobbyPartyShow kind, bool reduced)
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var holder = new Control();
            ui.WindowRoot.AddChild(holder);
            var entries = LobbyLineupShowcaseCommand.CreateEntries(CProtoMan, 6);
            var cards = new List<LobbyLineupCard>();
            LobbyPartyShowControl? show = null;
            try
            {
                foreach (var entry in entries)
                {
                    var card = new LobbyLineupCard();
                    holder.AddChild(card);
                    card.SetEntry(entry, cards.Count);
                    cards.Add(card);
                }
                var entities = cards.Select(card => card.StageEntity!.Value).ToArray();
                show = new LobbyPartyShowControl(new LobbyPartyShowEvent(kind, 7281,
                    entries.Select(entry => entry.UserId).ToList(), false), cards, reduced, false);
                ui.WindowRoot.AddChild(show);
                show.Measure(new Vector2(1280, 720));
                show.Arrange(new UIBox2(Vector2.Zero, new Vector2(1280, 720)));
                var scenery = show.Children.OfType<SpriteView>().Where(view => view.Entity != null)
                    .Select(view => view.Entity!.Value.Owner).Except(entities).ToArray();
                Assert.That(scenery.Length, Is.EqualTo(kind == LobbyPartyShow.Parade ? 3 : 0),
                    "The convoy should carry three actual flag entities.");
                Assert.That(cards.Count(card => card.StageMoving), Is.EqualTo(6),
                    "Both routines must bring the entire readied cast onto the stage.");
                show.Advance(3);
                // Unready/disconnect deletes the card's preview while its borrowed view is on the stage.
                cards[0].Orphan();
                cards[0].Dispose();
                for (var elapsed = 0; elapsed < 30; elapsed++)
                    show.Advance(1);
                Assert.That(show.Finished, Is.True);
                show.Release();
                show.Release();
                foreach (var card in cards.Skip(1))
                    Assert.That(card.StageMoving, Is.False);
                foreach (var entity in entities.Skip(1))
                    Assert.That(CEntMan.Deleted(entity), Is.False, "The show must never delete a card-owned preview.");
                foreach (var entity in scenery)
                    Assert.That(CEntMan.Deleted(entity), Is.True, "Owned flag previews must be deleted when the show ends.");
            }
            finally
            {
                show?.Release();
                holder.Orphan();
                holder.Dispose();
            }
        });
    }
}

[TestFixture]
public sealed class LobbyPartyChoreographyTest
{
    [TestCase(1)]
    [TestCase(60)]
    [TestCase(256)]
    public void WholeFlybyCastFitsOnTheGroundAndReducedMotionStaysStill(int count)
    {
        foreach (var size in new[] { new Vector2(640, 480), new Vector2(1920, 1080), new Vector2(3440, 1440) })
        {
            var positions = new HashSet<Vector2>();
            for (var i = 0; i < count; i++)
            {
                var pose = LobbyPartyChoreography.FlybyCrew(2, i, count, size, -100, false, 7);
                Assert.That(pose.Position.X, Is.InRange(0, size.X));
                Assert.That(pose.Position.Y, Is.InRange(0, size.Y));
                Assert.That(positions.Add(pose.Position), Is.True, "Each crewmember needs their own place on the ground.");
                var reduced = LobbyPartyChoreography.FlybyCrew(2, i, count, size, -100, true, 7);
                Assert.That(reduced, Is.EqualTo(LobbyPartyChoreography.FlybyCrew(8, i, count, size, 7, true, 7)));
                Assert.That(reduced.Rotation, Is.Zero);
            }
            if (count > 1)
            {
                Assert.That(positions.Min(p => p.X), Is.LessThan(size.X * 0.2f));
                Assert.That(positions.Max(p => p.X), Is.GreaterThan(size.X * 0.8f));
                Assert.That(positions.Min(p => p.Y), Is.LessThan(size.Y * 0.4f));
                Assert.That(positions.Max(p => p.Y), Is.GreaterThan(size.Y * 0.7f),
                    "The cast must occupy the battlefield instead of staying in two clusters.");
            }
        }
    }

    [Test]
    public void DiveAimsForwardDuringTheBurstAndRecoversBeforeLeaving()
    {
        foreach (var size in new[] { new Vector2(640, 480), new Vector2(1920, 1080), new Vector2(3440, 1440) })
        for (var pass = 0; pass < 2; pass++)
        {
            var focus = new Vector2(pass == 0 ? 0.395f : 0.61f, 0.7f);
            for (var shot = 0; shot < LobbyPartyChoreography.ShotsPerPass; shot++)
            {
                var pose = LobbyPartyChoreography.Jet(LobbyPartyChoreography.ShotTime(pass, shot), pass, size, focus);
                var toTarget = Vector2.Normalize(focus * size - pose.Position);
                Assert.That(Vector2.Dot(toTarget, pose.Heading), Is.GreaterThan(0.995f),
                    "GAU fire must leave the nose toward the target, including wide and small windows.");
                Assert.That(pose.Pitch, Is.InRange(0.35f, 0.6f), "The attack should use a shallow dip.");
                Assert.That(MathF.Abs(pose.Heading.Y), Is.LessThan(0.45f), "Avoid a steep nose-down attack.");
            }
            var recovery = LobbyPartyChoreography.Jet(LobbyPartyChoreography.PassStart(pass) + 5, pass, size, focus);
            Assert.That(recovery.Pitch, Is.LessThan(0.1f));
            Assert.That(recovery.Heading.Y, Is.LessThan(0), "The nose should lift toward the top of the screen on exit.");
            Assert.That(Math.Abs(recovery.Bank), Is.GreaterThan(0.5f));
        }
    }

    [TestCase(1)]
    [TestCase(60)]
    [TestCase(256)]
    public void EntireParadeEntersFromLeftAndLeavesRight(int count)
    {
        foreach (var size in new[] { new Vector2(640, 480), new Vector2(1920, 1080), new Vector2(3440, 1440) })
        {
            for (var i = 0; i < count; i++)
            {
                var start = LobbyPartyChoreography.March(0, i, count, size, false, 7);
                var end = LobbyPartyChoreography.March(LobbyPartyChoreography.ParadeDuration, i, count, size, false, 7);
                Assert.That(start.Position.X, Is.LessThan(0));
                Assert.That(end.Position.X, Is.GreaterThan(size.X));
                var staticPose = LobbyPartyChoreography.March(2, i, count, size, true, 7);
                Assert.That(staticPose, Is.EqualTo(LobbyPartyChoreography.March(4, i, count, size, true, 7)));
                Assert.That(staticPose.Rotation, Is.Zero);
            }
        }
    }

    [Test]
    public void GunRunsAlternateAndMissilesArriveBeforeTheShowEnds()
    {
        var size = new Vector2(1920, 1080);
        for (var pass = 0; pass < LobbyPartyChoreography.PassCount; pass++)
        {
            var start = LobbyPartyChoreography.PassStart(pass);
            var before = LobbyPartyChoreography.Jet(start, pass, size).Position.X;
            var after = LobbyPartyChoreography.Jet(start + LobbyPartyChoreography.PassDuration, pass, size).Position.X;
            Assert.That(Math.Min(before, after), Is.LessThan(-size.X * 0.2f));
            Assert.That(Math.Max(before, after), Is.GreaterThan(size.X * 1.2f));
            Assert.That(after > before, Is.EqualTo(pass % 2 == 0));
        }
        Assert.That(LobbyPartyChoreography.MissileTime(1) + 4.5f, Is.LessThan(LobbyPartyChoreography.FlybyDuration));
        Assert.That(LobbyPartyShowEvent.Cooldown, Is.GreaterThan(LobbyPartyChoreography.ParadeDuration));
    }
}
