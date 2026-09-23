using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.TacticalMap;
using System.Reflection;
using Moq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Fixture inputs model the existing, separately published faction and squad feeds.
    [TestCase(false)]
    [TestCase(true)]
    public async Task FirstSurveyReplyIncludesCurrentAuthorizedRoleIcons(bool dropFirstRequest)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        NetEntity console = default;
        CMUReconHandshakeTestSystem probe = null;
        CMUReconHandshakeTestSystem serverProbe = null;
        var commander = new TacticalMapBlip
        {
            Indices = new(2, 3), Color = Color.Cyan,
            Image = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "rmc_commander"),
        };
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                var map = SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                map.GovforBlips = new() { [_actor.Id] = commander };
                map.OpforBlips = new() { [_console.Id] = commander with { Indices = new(7, 7) } };
                map.NextUpdate = TimeSpan.MaxValue;
                foreach (var faction in map.NextUpdatePerFaction.Keys.ToArray()) map.NextUpdatePerFaction[faction] = TimeSpan.MaxValue;
                SComp<TacticalMapComputerComponent>(_console).Blips.Clear();
                Server.PlayerMan.SetAttachedEntity(session, _actor);
                console = SEntMan.GetNetEntity(_console);
                serverProbe = SEntMan.System<CMUReconHandshakeTestSystem>();
                serverProbe.Target = _console;
                serverProbe.DropRequest = dropFirstRequest;
            });
            await Pair.RunUntilSynced();
            await Client.WaitPost(() =>
            {
                probe = CEntMan.System<CMUReconHandshakeTestSystem>();
                probe.Target = CEntMan.GetEntity(console);
                // Initial icons must accompany metadata, not depend on a later contact refresh packet.
                probe.DropContacts = true;
            });
            await Server.WaitPost(() => _ui.TryOpenUi(_console, Key, _actor));
            await Pair.RunSeconds(1);
            await Client.WaitAssertion(() =>
            {
                var view = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView;
                Assert.That(view.Scene, Is.Not.Null, "A dropped opening request should recover within one second.");
                Assert.That(view.TrackedContacts.Select(c => c.Blip), Is.EquivalentTo(new[] { commander }),
                    "The first survey reply must contain the commander icon and omit unauthorized enemy contacts.");
            });
        }
        finally
        {
            await Client.WaitPost(() => { if (probe != null) { probe.DropContacts = false; probe.Target = default; } });
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                if (serverProbe != null) { serverProbe.Target = default; serverProbe.DropRequest = false; }
                Server.PlayerMan.SetAttachedEntity(session, original);
            });
            await Pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task PersonalMapReceivesRoleIconsAndFreshSquadPositionsOverTheWire()
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid marine = default;
        var commander = new TacticalMapBlip { Indices = new(2, 3), Image = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "rmc_commander"), Color = Color.Cyan };
        var leader = commander with { Indices = new(4, 5), Image = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "leader"), FireteamNumber = 2 };
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                marine = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(1.5f)));
                Server.PlayerMan.SetAttachedEntity(session, marine);
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(marine, new OpenTacticalMapActionEvent { Performer = marine }));
            await Pair.RunTicksSync(40);
            await Server.WaitPost(() =>
            {
                // Model exactly the two independently published feeds on the normal personal map.
                var user = SComp<TacticalMapUserComponent>(marine);
                user.Marines = true;
                user.Opfor = false;
                user.HasSquad = true;
                user.MarineBlips = new() { [_actor.Id] = commander, [_console.Id] = leader with { Indices = new(0, 0) } };
                user.SquadBlips = new() { [_console.Id] = leader };
                user.OpforBlips = new() { [marine.Id] = commander with { Indices = new(7, 7) } };
            });
            await Pair.RunTicksSync(20);
            await Client.WaitAssertion(() =>
            {
                var view = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView;
                Assert.That(view.TrackedContacts, Is.EquivalentTo(new CMUReconContact[] { new(0, commander), new(0, leader) }),
                    "Preserve actual job sprites, faction colours and fireteam badges; prefer live squad positions and exclude disabled factions.");
            });
            leader = leader with { Indices = new(6, 5) };
            await Server.WaitPost(() => SComp<TacticalMapUserComponent>(marine).SquadBlips[_console.Id] = leader);
            await Pair.RunTicksSync(20);
            await Client.WaitAssertion(() =>
            {
                var view = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView;
                Assert.That(view.TrackedContacts.Single(c => c.Blip.FireteamNumber == 2).Blip.Indices, Is.EqualTo(new Vector2i(6, 5)));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (marine.IsValid()) _ui.CloseUi(marine, TacticalMapUserUi.Key, marine);
                Server.PlayerMan.SetAttachedEntity(session, original);
                if (marine.IsValid()) SEntMan.DeleteEntity(marine);
            });
            await Pair.RunUntilSynced();
        }
    }
#pragma warning restore RA0002

    [TestCase(0)]
    [TestCase(600)]
    public async Task WarmReopenReusesFrozenTerrainAndOnlyTransfersMissingChunks(int idleSeconds)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        NetEntity console = default;
        CMUReconHandshakeTestSystem probe = null;
        CMUReconSnapshotMessage saved = null;
        object terrain = null;
        object appearance = null;
        object occupancy = null;
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                Server.PlayerMan.SetAttachedEntity(session, _actor);
                console = SEntMan.GetNetEntity(_console);
            });
            await Pair.RunUntilSynced();
            await Client.WaitPost(() =>
            {
                probe = CEntMan.System<CMUReconHandshakeTestSystem>();
                probe.Target = CEntMan.GetEntity(console);
                probe.Chunks = probe.ChunkBytes = 0;
            });
            await Server.WaitPost(() => _ui.TryOpenUi(_console, Key, _actor));
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                saved = window.SurveyView.Scene!;
                Assert.That(saved.LoadedChunks, Is.EqualTo(saved.TotalChunks));
                Assert.That(saved.Revisions, Has.All.GreaterThan(0));
                Assert.That(probe.ChunkBytes, Is.LessThan(saved.TotalChunks * 800), "Repeated floor cells should compress on the wire.");
                var view = window.SurveyView;
                while (((System.Collections.ICollection) RenderField(view, "_pendingUploads")).Count > 0)
                    Dispatch(view, "UploadPending");
                terrain = RenderField(view, "_terrain");
                appearance = RenderField(view, "_appearance");
                occupancy = RenderField(view, "_occupancy");
                TestContext.Out.WriteLine($"Cold fixture: {probe.Chunks} chunks, {probe.ChunkBytes} estimated geometry bytes.");
                window.Close();
            });
            await Pair.RunTicksSync(15);
            await Client.WaitAssertion(() =>
            {
                // Advance only the cache's observed wall clock; no real ten-minute wait or
                // changes to network/simulation time are needed to exercise idle eviction.
                var timing = Client.ResolveDependency<IGameTiming>();
                var clock = typeof(GameTiming).GetField("_realTimer", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var originalClock = clock.GetValue(timing);
                var elapsed = timing.RealTime + TimeSpan.FromSeconds(idleSeconds);
                var agedClock = new Mock<IStopwatch>();
                agedClock.SetupGet(watch => watch.Elapsed).Returns(elapsed);
                try
                {
                    clock.SetValue(timing, agedClock.Object);
                    CEntMan.System<CMUReconstructionCacheSystem>().Update(0);
                }
                finally
                {
                    clock.SetValue(timing, originalClock);
                }
            });
            await Client.WaitPost(() => probe.Chunks = probe.ChunkBytes = 0);
            await Server.WaitPost(() => _ui.TryOpenUi(_console, Key, _actor));
            await Pair.RunTicksSync(20);
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                Assert.That(probe.Snapshot.ReuseGeometry, Is.True);
                Assert.That(window.IsRefreshing, Is.False);
                Assert.That(window.SurveyView.Scene!.Cells, Is.SameAs(saved.Cells));
                Assert.That(probe.Chunks, Is.Zero, "An unchanged reopening must not transfer a second terrain baseline.");
                Assert.That(RenderField(window.SurveyView, "_terrain"), Is.SameAs(terrain),
                    "Reopening must retain uploaded terrain, not allocate and refill a new texture.");
                Assert.That(RenderField(window.SurveyView, "_appearance"), Is.SameAs(appearance));
                Assert.That(RenderField(window.SurveyView, "_occupancy"), Is.SameAs(occupancy));
                Assert.That(((System.Collections.ICollection) RenderField(window.SurveyView, "_pendingUploads")).Count,
                    Is.Zero, "A completed map must have no terrain uploads to repeat on reopening.");
                TestContext.Out.WriteLine($"Warm fixture: {probe.Chunks} chunks, {probe.ChunkBytes} geometry bytes.");
                // Retain a partial baseline as well as a complete one (e.g. closing during first load).
                window.SurveyView.Scene.Revisions[0] = 0;
                window.SurveyView.Scene.LoadedChunks--;
                window.Close();
            });
            await Pair.RunTicksSync(15);
            await Server.WaitPost(() => SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(_upper, new Vector2(3.5f))));
            await Client.WaitPost(() => probe.Chunks = probe.ChunkBytes = 0);
            await Server.WaitPost(() => _ui.TryOpenUi(_console, Key, _actor));
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                var scene = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView.Scene!;
                var tile = new Vector2i(3, 3) - scene.Origin;
                Assert.That(scene.Cells[CMUReconGeometry.Index(tile.X, tile.Y, 1, scene.Width, scene.Height)], Is.EqualTo((byte) CMUReconMaterial.Floor),
                    "Reopening must not reveal a wall built after the initial survey.");
                Assert.That(probe.Snapshot.ReuseGeometry, Is.True, "An incomplete cached baseline must also be reusable.");
                Assert.That(scene.LoadedChunks, Is.EqualTo(scene.TotalChunks));
                Assert.That(probe.Chunks, Is.EqualTo(1), "Only resend the missing chunk from the original survey.");
            });
        }
        finally
        {
            await Client.WaitPost(() => { if (probe != null) probe.Target = default; });
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                Server.PlayerMan.SetAttachedEntity(session, original);
            });
            await Pair.RunUntilSynced();
        }
    }

    private static object RenderField(CMUReconstructionControl view, string name)
    {
        var render = typeof(CMUReconstructionControl).GetField("_render", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
        var member = char.ToUpperInvariant(name[1]) + name[2..];
        return typeof(CMUReconRenderData).GetField(member, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(render)!;
    }

    [Test]
    public async Task ReadOnlySurveyHidesDrawingToolsAndRestoresThemWhenAuthorized()
    {
        CMUReconSnapshotMessage scene = null;
        await Server.WaitPost(() => scene = _recon.BuildSnapshot(_console, _actor)!);
        await Client.WaitAssertion(() =>
        {
            using var window = new CMUReconstructionWindow();
            window.OpenCentered();
            scene.CanOrder = false;
            window.Receive(scene);
            void AssertTools(bool visible)
            {
                foreach (var name in new[] { "Pencil", "PlaceText", "Send", "Colors", "StrokeWidth", "MarkerText", "Undo", "Clear" })
                    Assert.That(window.FindControl<Control>(name).VisibleInTree, Is.EqualTo(visible), name);
                foreach (var name in new[] { "TopDown", "Reset", "MapSelection", "Floor", "Contacts" })
                    Assert.That(window.FindControl<Control>(name).VisibleInTree, Is.True, name);
            }
            AssertTools(false);
            Assert.That(window.SurveyView.DrawingEnabled || window.SurveyView.TextEnabled, Is.False);
            window.Receive(new CMUReconPatchMessage(scene.Generation, [], [], true));
            AssertTools(true);
            window.FindControl<CheckBox>("Pencil").Pressed = true;
            window.Receive(new CMUReconPatchMessage(scene.Generation, [], [], false));
            AssertTools(false);
            Assert.That(window.SurveyView.DrawingEnabled || window.SurveyView.TextEnabled, Is.False);
            window.Close();
        });
    }

    [Test]
    public async Task RefreshAcceptsCommanderAndLeaderIconsBeforeTerrainCompletes()
    {
        CMUReconSnapshotMessage saved = null;
        await Pair.RunTicksSync(40);
        await Server.WaitPost(() => saved = _recon.BuildSnapshot(_console, _actor)!);
        await Client.WaitAssertion(() =>
        {
            using var window = new CMUReconstructionWindow();
            window.OpenCentered();
            window.RestoreCached(saved, default);
            var next = new CMUReconSnapshotMessage(saved.Generation + 1, saved.Origin, saved.MinDepth, saved.Levels,
                [], [], true, saved.Width, saved.Height) { TotalChunks = 10 };
            window.Receive(next);
            var commander = new TacticalMapBlip { Indices = new(2, 3), Image = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "rmc_commander"), Color = Color.White };
            var leader = commander with { Indices = new(4, 5), Image = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "leader"), FireteamNumber = 2 };
            var contacts = new CMUReconContact[] { new(0, commander), new(0, leader) };
            window.Receive(new CMUReconContactsMessage(next.Generation, contacts));
            Assert.That(window.IsRefreshing, Is.True);
            Assert.That(window.SurveyView.TrackedContacts, Is.EqualTo(contacts), "Icons must be usable during terrain refresh.");
            window.Receive(new CMUReconContactsMessage(saved.Generation, []));
            Assert.That(window.SurveyView.TrackedContacts, Is.EqualTo(contacts), "Ignore old subscriptions.");
            window.Receive(new CMUReconPatchMessage(next.Generation, [], [], true) { LoadedChunks = 10, TotalChunks = 10 });
            Assert.That(window.SurveyView.TrackedContacts, Is.EqualTo(contacts), "Swapping atlases must not clear the current icon feed.");
            window.Close();
        });
    }
}
