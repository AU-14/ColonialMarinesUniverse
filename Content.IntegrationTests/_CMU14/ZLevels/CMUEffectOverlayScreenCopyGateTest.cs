using Content.Client._RMC14.Xenonids.Doom;
using Content.Client._RMC14.Xenonids.Screech;
using Content.Client.Viewport;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Doom;
using Content.Shared._RMC14.Xenonids.Screech;
using Moq;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Graphics;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUEffectOverlayScreenCopyGateTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task LocalEffectAndAttachmentChangesGateTheRegisteredOverlay(bool doom)
    {
        await Client.WaitAssertion(() =>
        {
            var player = Client.ResolveDependency<IPlayerManager>();
            var overlays = Client.ResolveDependency<IOverlayManager>();
            var session = player.LocalSession;
            Assert.That(session, Is.Not.Null);
            var original = player.LocalEntity;
            var maps = Client.System<SharedMapSystem>();
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            var local = CEntMan.SpawnEntity(null, new EntityCoordinates(mapUid, Vector2.Zero));
            var remote = CEntMan.SpawnEntity(null, new EntityCoordinates(mapUid, Vector2.One));
            var unaffected = CEntMan.SpawnEntity(null, new EntityCoordinates(mapUid, new Vector2(2f)));
            CEntMan.EnsureComponent<EyeComponent>(local);
            CEntMan.EnsureComponent<EyeComponent>(remote);
            CEntMan.EnsureComponent<EyeComponent>(unaffected);

            try
            {
                Assert.That(player.SetAttachedEntity(session, local), Is.True);
                Assert.That(HasOverlay(), Is.False);
                AddEffect(remote);
                // Doom is globally registered; Screech's owner checks the local entity at init.
                Assert.That(HasOverlay(), Is.EqualTo(doom));
                var viewport = new Mock<IClydeViewport>();
                viewport.SetupProperty(v => v.Eye, new Eye
                {
                    Position = new MapCoordinates(Vector2.Zero, mapId),
                });
                if (doom)
                    Assert.That(overlays.GetOverlay<DoomOverlay>().ShouldDrawForViewport(viewport.Object), Is.False);

                AddEffect(local);
                Assert.That(HasOverlay(), Is.True);
                Overlay overlay = doom
                    ? overlays.GetOverlay<DoomOverlay>()
                    : overlays.GetOverlay<ScreechBlindOverlay>();

                bool ShouldDraw() => overlay switch
                {
                    DoomOverlay doomed => doomed.ShouldDrawForViewport(viewport.Object),
                    ScreechBlindOverlay blind => blind.ShouldDrawForViewport(viewport.Object),
                    _ => throw new InvalidOperationException(),
                };

                // This seam is the live BeforeDraw implementation; a screen texture is only
                // supplied by Clyde after it returns true, so it must not be a prerequisite.
                Assert.That(overlay.ScreenTexture, Is.Null);
                Assert.That(overlay.RequestScreenTexture, Is.True);
                Assert.That(ShouldDraw(), Is.True);
                viewport.Object.Eye = null;
                Assert.That(ShouldDraw(), Is.False);
                viewport.Object.Eye = new Eye { Position = MapCoordinates.Nullspace };
                Assert.That(ShouldDraw(), Is.True, "Preserve the existing absence of map restrictions.");
                viewport.Object.Eye = new ScalingViewport.ZEye
                {
                    Depth = -1,
                    Position = new MapCoordinates(Vector2.Zero, mapId),
                };
                Assert.That(ShouldDraw(), Is.True, "The active effect still applies to lower viewport passes.");

                Assert.That(player.SetAttachedEntity(session, unaffected), Is.True);
                Assert.That(HasOverlay(), Is.True, "Attachment changes do not remove these overlays.");
                Assert.That(ShouldDraw(), Is.False, "An installed overlay must skip an unaffected local player.");
                Assert.That(player.SetAttachedEntity(session, null), Is.True);
                Assert.That(ShouldDraw(), Is.False);
                Assert.That(player.SetAttachedEntity(session, remote), Is.True);
                Assert.That(ShouldDraw(), Is.True, "The gate must resolve the newly attached entity's effect.");

                Assert.That(player.SetAttachedEntity(session, local), Is.True);
                if (doom)
                    CEntMan.RemoveComponent<MobDoomedComponent>(local);
                else
                    CEntMan.RemoveComponent<ScreechBlindComponent>(local);
                Assert.That(ShouldDraw(), Is.False);
                Assert.That(HasOverlay(), Is.False, "Preserve existing component-removal registration behavior.");
            }
            finally
            {
                player.SetAttachedEntity(session, original);
                CEntMan.DeleteEntity(mapUid);
                if (doom)
                    overlays.RemoveOverlay<DoomOverlay>();
                else
                    overlays.RemoveOverlay<ScreechBlindOverlay>();
            }

            bool HasOverlay() => doom ? overlays.HasOverlay<DoomOverlay>() : overlays.HasOverlay<ScreechBlindOverlay>();

            void AddEffect(EntityUid uid)
            {
                if (doom)
                    CEntMan.AddComponent<MobDoomedComponent>(uid);
                else
                    CEntMan.AddComponent<ScreechBlindComponent>(uid);
            }
        });
    }
}
