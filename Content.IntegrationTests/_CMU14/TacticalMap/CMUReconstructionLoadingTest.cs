using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.TacticalMap;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Fixture inputs model the existing, separately published faction and squad feeds.
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

    [Test]
    public async Task WarmReopenReusesTerrainAndOnlyTransfersChangedChunks()
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        NetEntity console = default;
        CMUReconHandshakeTestSystem probe = null;
        CMUReconSnapshotMessage saved = null;
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
                TestContext.Out.WriteLine($"Cold fixture: {probe.Chunks} chunks, {probe.ChunkBytes} estimated geometry bytes.");
                window.Close();
            });
            await Pair.RunTicksSync(15);
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
                Assert.That(scene.Cells[CMUReconGeometry.Index(tile.X, tile.Y, 1, scene.Width, scene.Height)], Is.EqualTo((byte) CMUReconMaterial.Wall));
                Assert.That(probe.Snapshot.ReuseGeometry, Is.True, "An incomplete cached baseline must also be reusable.");
                Assert.That(scene.LoadedChunks, Is.EqualTo(scene.TotalChunks));
                Assert.That(probe.Chunks, Is.EqualTo(2), "Send the missing chunk and changed wall chunk, not another full baseline.");
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
