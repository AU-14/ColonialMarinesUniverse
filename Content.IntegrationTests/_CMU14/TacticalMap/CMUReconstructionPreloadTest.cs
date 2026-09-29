using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CCVar;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using System.Reflection;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
    [Test]
    public async Task PersonalPreloadOpensWithoutRetransmittingOrReuploadingTerrain()
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid marine = default;
        NetEntity netMarine = default;
        CMUReconRenderData render = null;
        CMUReconHandshakeTestSystem probe = null;
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                marine = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(1.5f)));
                Server.PlayerMan.SetAttachedEntity(session, marine);
                netMarine = SEntMan.GetNetEntity(marine);
            });
            await Pair.RunUntilSynced();
            // Includes the startup delay, shared extraction, network transfer and bounded client upload.
            await Pair.RunTicksSync(360);
            await Client.WaitAssertion(() =>
            {
                var windows = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>();
                Assert.That(windows, Is.Empty, "Preloading must not open a window.");
                var cache = CEntMan.System<CMUReconstructionCacheSystem>();
                var entry = typeof(CMUReconstructionCacheSystem).GetField("_entry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache);
                Assert.That(entry, Is.Not.Null, "The likely personal map should load before its first opening.");
                var scene = (CMUReconSnapshotMessage) entry!.GetType().GetProperty("Scene")!.GetValue(entry)!;
                render = (CMUReconRenderData) entry.GetType().GetProperty("Render")!.GetValue(entry)!;
                Assert.That(scene.LoadedChunks, Is.EqualTo(scene.TotalChunks));
                Assert.That(scene.CanOrder, Is.False);
                Assert.That(scene.Orders, Is.Empty);
                probe = CEntMan.System<CMUReconHandshakeTestSystem>();
                probe.Target = CEntMan.GetEntity(netMarine);
                probe.Chunks = probe.ChunkBytes = 0;
            });
            await Server.WaitAssertion(() =>
            {
                Assert.That(_ui.IsUiOpen(marine, TacticalMapUserUi.Key, marine), Is.False);
                Assert.That(_recon.BuildSnapshot(marine, marine), Is.Null, "Preloading must not authorize a drawing subscription.");
                SEntMan.EventBus.RaiseLocalEvent(marine, new OpenTacticalMapActionEvent { Performer = marine });
            });
            await Pair.RunTicksSync(30);
            await Client.WaitAssertion(() =>
            {
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                Assert.That(probe.Snapshot.ReuseGeometry, Is.True);
                Assert.That(probe.Chunks, Is.Zero);
                Assert.That(typeof(CMUReconstructionControl).GetField("_render", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window.SurveyView), Is.SameAs(render));
                Assert.That(window.SurveyView.ResourcesReady, Is.True);
                Assert.That(window.SurveyView.DrawingEnabled || window.SurveyView.TextEnabled, Is.False);
            });
            // Changing controlled bodies must release the retained textures.
            await Server.WaitPost(() => _ui.CloseUi(marine, TacticalMapUserUi.Key, marine));
            await Pair.RunTicksSync(15);
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, original));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => Assert.That(render.Disposed, Is.True));
        }
        finally
        {
            await Client.WaitPost(() => { if (probe != null) probe.Target = default; });
            await Server.WaitPost(() =>
            {
                if (marine.IsValid()) _ui.CloseUi(marine, TacticalMapUserUi.Key, marine);
                Server.PlayerMan.SetAttachedEntity(session, original);
                if (marine.IsValid()) SEntMan.DeleteEntity(marine);
            });
            await Pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task PreloadingHonorsClassicPreferenceAndRejectsActorsWithoutPersonalMapAccess()
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        System.Collections.IDictionary pending = null;
        await Server.WaitPost(() =>
        {
            pending = (System.Collections.IDictionary) typeof(Content.Server.CMU14.TacticalMap.Reconstruction.CMUTacticalReconstructionSystem)
                .GetField("_preloads", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_recon)!;
            // Pooled sessions can retain an idle rate-limit entry from an earlier authorized preload.
            pending.Remove(session);
            Server.PlayerMan.SetAttachedEntity(session, _actor);
        });
        await Pair.RunUntilSynced();
        try
        {
            await Client.WaitPost(() => CEntMan.System<CMUReconHandshakeTestSystem>().RequestPreload());
            await Pair.RunTicksSync(40);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<TacticalMapUserComponent>(_actor), Is.False);
                Assert.That(pending.Contains(session), Is.False);
            });
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, true));
            await Server.WaitPost(() => SEntMan.EnsureComponent<TacticalMapUserComponent>(_actor));
            await Pair.RunTicksSync(360);
            await Client.WaitAssertion(() =>
            {
                var cache = CEntMan.System<CMUReconstructionCacheSystem>();
                Assert.That(typeof(CMUReconstructionCacheSystem).GetField("_preloadRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache), Is.EqualTo(0));
                Assert.That(typeof(CMUReconstructionCacheSystem).GetField("_entry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache), Is.Null);
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, false));
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, original));
            await Pair.RunUntilSynced();
        }
    }
}
